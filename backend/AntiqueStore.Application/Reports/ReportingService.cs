using AntiqueStore.Application.Common;
using AntiqueStore.Application.Orders;
using AntiqueStore.Domain.Enums;
using Microsoft.EntityFrameworkCore;

namespace AntiqueStore.Application.Reports;

/// <summary>
/// Revenue definitions (used consistently across dashboard and reports):
/// - Net paid revenue: Σ(Total − RefundedTotal) for orders whose payment is Paid or Refunded.
/// - Outstanding: Σ Total of non-cancelled orders not yet paid (Unpaid / PendingVerification / Failed).
/// - Cancelled: Σ Total of cancelled orders that were never paid.
/// - Refunded: Σ RefundedTotal.
/// </summary>
public sealed record RevenueBreakdownDto(decimal NetPaid, decimal Outstanding, decimal Cancelled, decimal Refunded, int PaidOrders);

public sealed record ProductCountsDto(int Total, int Available, int Reserved, int Sold, int Draft, int Archived);

public sealed record TrendPointDto(DateOnly Date, decimal PaidRevenue, int Orders);

public sealed record LowStockDto(int Id, string Name, string Sku, int QuantityAvailable, int QuantityReserved);

public sealed record DashboardDto(
    RevenueBreakdownDto Revenue, int TotalOrders, int PendingOrders, int AwaitingVerification, ProductCountsDto Products,
    IReadOnlyList<AdminOrderListItemDto> RecentOrders, IReadOnlyList<LowStockDto> LowStock, IReadOnlyList<TrendPointDto> SalesTrend,
    int NewEnquiries, int PendingReviews, int ActiveReservations, string Currency);

public sealed record BreakdownRowDto(string Label, int Orders, decimal Amount);

public sealed record TopProductDto(int ProductId, string Name, int Quantity, decimal Revenue);

public sealed record SalesReportDto(
    DateOnly From, DateOnly To, RevenueBreakdownDto Revenue, IReadOnlyList<TrendPointDto> Daily,
    IReadOnlyList<BreakdownRowDto> ByStatus, IReadOnlyList<BreakdownRowDto> ByPaymentMethod,
    IReadOnlyList<BreakdownRowDto> ByCategory, IReadOnlyList<TopProductDto> TopProducts, decimal AverageOrderValue);

public sealed class ReportingService(IAppDbContext db, TimeProvider clock)
{
    /// <summary>Business day boundaries follow Sri Lanka time (UTC+05:30).</summary>
    public static readonly TimeZoneInfo StoreTimeZone = ResolveTimeZone();

    private static TimeZoneInfo ResolveTimeZone()
    {
        try { return TimeZoneInfo.FindSystemTimeZoneById("Asia/Colombo"); }
        catch (Exception ex) when (ex is TimeZoneNotFoundException or InvalidTimeZoneException)
        {
            return TimeZoneInfo.CreateCustomTimeZone("Asia/Colombo", TimeSpan.FromMinutes(330), "Sri Lanka", "Sri Lanka");
        }
    }

    private sealed record OrderRow(int Id, DateTime PlacedAt, OrderStatus Status, PaymentStatus PaymentStatus, PaymentMethod Method, decimal Total, decimal Refunded);

    private static bool IsPaid(OrderRow o) => o.PaymentStatus is PaymentStatus.Paid or PaymentStatus.Refunded;

    private static RevenueBreakdownDto Breakdown(IReadOnlyCollection<OrderRow> rows) => new(
        rows.Where(IsPaid).Sum(o => o.Total - o.Refunded),
        rows.Where(o => o.Status != OrderStatus.Cancelled && !IsPaid(o)).Sum(o => o.Total),
        rows.Where(o => o.Status == OrderStatus.Cancelled && !IsPaid(o)).Sum(o => o.Total),
        rows.Sum(o => o.Refunded),
        rows.Count(IsPaid));

    private static DateOnly LocalDate(DateTime utc) => DateOnly.FromDateTime(TimeZoneInfo.ConvertTimeFromUtc(utc, StoreTimeZone));

    private static DateTime LocalMidnightUtc(DateOnly date) =>
        TimeZoneInfo.ConvertTimeToUtc(date.ToDateTime(TimeOnly.MinValue, DateTimeKind.Unspecified), StoreTimeZone);

    private async Task<List<OrderRow>> LoadOrdersAsync(DateTime fromUtc, DateTime toUtc, CancellationToken ct) =>
        await db.Orders.AsNoTracking().Where(o => o.PlacedAt >= fromUtc && o.PlacedAt < toUtc)
            .Select(o => new OrderRow(o.Id, o.PlacedAt, o.Status, o.PaymentStatus, o.PaymentMethod, o.Total, o.RefundedTotal))
            .ToListAsync(ct);

    private static List<TrendPointDto> Daily(IEnumerable<OrderRow> rows, DateOnly from, DateOnly to)
    {
        var byDay = rows.GroupBy(o => LocalDate(o.PlacedAt)).ToDictionary(g => g.Key, g => g.ToList());
        var points = new List<TrendPointDto>();
        for (var d = from; d <= to; d = d.AddDays(1))
        {
            var day = byDay.GetValueOrDefault(d) ?? [];
            points.Add(new TrendPointDto(d, day.Where(IsPaid).Sum(o => o.Total - o.Refunded), day.Count(o => o.Status != OrderStatus.Cancelled)));
        }
        return points;
    }

    public async Task<DashboardDto> GetDashboardAsync(CancellationToken ct)
    {
        var all = await db.Orders.AsNoTracking()
            .Select(o => new OrderRow(o.Id, o.PlacedAt, o.Status, o.PaymentStatus, o.PaymentMethod, o.Total, o.RefundedTotal))
            .ToListAsync(ct);

        var today = LocalDate(clock.GetUtcNow().UtcDateTime);
        var from = today.AddDays(-29);
        var trend = Daily(all.Where(o => LocalDate(o.PlacedAt) >= from), from, today);

        var products = await db.Products.AsNoTracking()
            .Select(p => new { p.Status, p.QuantityAvailable, p.QuantityReserved }).ToListAsync(ct);
        var published = products.Where(p => p.Status == ProductStatus.Published).ToList();
        var counts = new ProductCountsDto(
            products.Count(p => p.Status != ProductStatus.Archived),
            published.Count(p => p.QuantityAvailable > 0),
            published.Count(p => p.QuantityAvailable == 0 && p.QuantityReserved > 0),
            published.Count(p => p.QuantityAvailable == 0 && p.QuantityReserved == 0),
            products.Count(p => p.Status == ProductStatus.Draft),
            products.Count(p => p.Status == ProductStatus.Archived));

        var recent = await db.Orders.AsNoTracking().OrderByDescending(o => o.PlacedAt).Take(6)
            .Select(o => new AdminOrderListItemDto(o.Id, o.OrderNumber, o.CustomerName, o.CustomerEmail, o.Status, o.PaymentStatus,
                o.PaymentMethod, o.FulfillmentMethod, o.Total, o.Items.Sum(i => i.Quantity), o.PlacedAt))
            .ToListAsync(ct);

        // Low stock only makes sense for multi-unit items (one-of-a-kind pieces are simply available or sold).
        var lowStock = await db.Products.AsNoTracking()
            .Where(p => p.Status == ProductStatus.Published && p.QuantityAvailable <= 1 &&
                        p.QuantityAvailable + p.QuantityReserved + p.QuantitySold > 1 && p.QuantityAvailable + p.QuantityReserved > 0)
            .OrderBy(p => p.QuantityAvailable).Take(10)
            .Select(p => new LowStockDto(p.Id, p.Name, p.Sku, p.QuantityAvailable, p.QuantityReserved))
            .ToListAsync(ct);

        return new DashboardDto(
            Breakdown(all), all.Count(o => o.Status != OrderStatus.Cancelled), all.Count(o => o.Status == OrderStatus.Pending),
            all.Count(o => o.PaymentStatus == PaymentStatus.PendingVerification && o.Status != OrderStatus.Cancelled),
            counts, recent, lowStock, trend,
            await db.Enquiries.CountAsync(e => e.Status == EnquiryStatus.New, ct),
            await db.Reviews.CountAsync(r => r.Status == ReviewStatus.Pending, ct),
            await db.InventoryReservations.CountAsync(r => r.Status == ReservationStatus.Active, ct),
            "LKR");
    }

    public async Task<SalesReportDto> GetSalesReportAsync(DateOnly? fromDate, DateOnly? toDate, CancellationToken ct)
    {
        var today = LocalDate(clock.GetUtcNow().UtcDateTime);
        var to = toDate ?? today;
        var from = fromDate ?? to.AddDays(-29);
        if (from > to) (from, to) = (to, from);
        if (to.DayNumber - from.DayNumber > 366) throw RequestValidationException.For("from", "Reports can cover at most one year.");

        var rows = await LoadOrdersAsync(LocalMidnightUtc(from), LocalMidnightUtc(to.AddDays(1)), ct);
        var ids = rows.Select(r => r.Id).ToList();
        var paidIds = rows.Where(IsPaid).Select(r => r.Id).ToHashSet();

        var items = await db.OrderItems.AsNoTracking().Where(i => ids.Contains(i.OrderId))
            .Select(i => new { i.OrderId, i.ProductId, i.ProductName, i.Quantity, i.LineTotal, Category = i.Product!.Category != null ? i.Product.Category.Name : "Uncategorised" })
            .ToListAsync(ct);
        var paidItems = items.Where(i => paidIds.Contains(i.OrderId)).ToList();

        var paidCount = rows.Count(IsPaid);
        return new SalesReportDto(
            from, to, Breakdown(rows), Daily(rows, from, to),
            rows.GroupBy(r => r.Status).Select(g => new BreakdownRowDto(g.Key.ToString(), g.Count(), g.Sum(o => o.Total))).OrderBy(r => r.Label).ToList(),
            rows.Where(IsPaid).GroupBy(r => r.Method).Select(g => new BreakdownRowDto(g.Key.ToString(), g.Count(), g.Sum(o => o.Total - o.Refunded))).ToList(),
            paidItems.GroupBy(i => i.Category).Select(g => new BreakdownRowDto(g.Key, g.Select(i => i.OrderId).Distinct().Count(), g.Sum(i => i.LineTotal)))
                .OrderByDescending(r => r.Amount).ToList(),
            paidItems.GroupBy(i => new { i.ProductId, i.ProductName })
                .Select(g => new TopProductDto(g.Key.ProductId, g.Key.ProductName, g.Sum(i => i.Quantity), g.Sum(i => i.LineTotal)))
                .OrderByDescending(p => p.Revenue).Take(10).ToList(),
            paidCount == 0 ? 0 : Math.Round(rows.Where(IsPaid).Sum(o => o.Total) / paidCount, 2));
    }
}
