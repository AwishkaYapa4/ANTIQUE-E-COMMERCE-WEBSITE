using AntiqueStore.Application.Common;
using AntiqueStore.Application.Payments;
using AntiqueStore.Application.Settings;
using AntiqueStore.Domain.Entities;
using AntiqueStore.Domain.Enums;
using AntiqueStore.Domain.Rules;
using Microsoft.EntityFrameworkCore;

namespace AntiqueStore.Application.Orders;

public sealed class OrderQueries(IAppDbContext db, PaymentMethodRegistry payments, CommerceSettingsService settings)
{
    /// <summary>Loads an order only if it belongs to the caller (ownership enforced in the query itself).</summary>
    public async Task<OrderDto> GetForCustomerAsync(int userId, int orderId, CancellationToken ct)
    {
        var order = await db.Orders.AsNoTracking()
            .Include(o => o.Items)
            .Include(o => o.History.Where(h => h.VisibleToCustomer).OrderBy(h => h.CreatedAt))
            .AsSplitQuery()
            .FirstOrDefaultAsync(o => o.Id == orderId && o.UserId == userId, ct) ?? throw new NotFoundException("Order", orderId);
        return await ToDtoAsync(order, ct);
    }

    public async Task<OrderDto> GetForCustomerByNumberAsync(int userId, string orderNumber, CancellationToken ct)
    {
        var id = await db.Orders.AsNoTracking().Where(o => o.OrderNumber == orderNumber && o.UserId == userId)
            .Select(o => (int?)o.Id).FirstOrDefaultAsync(ct) ?? throw new NotFoundException("Order", orderNumber);
        return await GetForCustomerAsync(userId, id, ct);
    }

    public async Task<PagedResult<OrderSummaryDto>> ListForCustomerAsync(int userId, PageRequest page, CancellationToken ct) =>
        await db.Orders.AsNoTracking().Where(o => o.UserId == userId).OrderByDescending(o => o.PlacedAt)
            .ToPagedAsync(page, q => q.Select(o => new OrderSummaryDto(
                o.Id, o.OrderNumber, o.Status, o.PaymentStatus, o.PaymentMethod, o.Total, o.Currency,
                o.Items.Sum(i => i.Quantity), o.Items.Select(i => i.ImageUrl).FirstOrDefault(), o.PlacedAt)), ct);

    public async Task<OrderDto> ToDtoAsync(Order o, CancellationToken ct)
    {
        var commerce = await settings.GetAsync(ct);
        var instructions = o.Status == OrderStatus.Cancelled || o.PaymentStatus is PaymentStatus.Paid or PaymentStatus.Refunded
            ? null
            : payments.Get(o.PaymentMethod).GetInstructions(o, commerce);

        return new OrderDto(
            o.Id, o.OrderNumber, o.Status, o.PaymentStatus, o.PaymentMethod, o.FulfillmentMethod,
            new ContactInfo(o.CustomerName, o.CustomerEmail, o.CustomerPhone),
            o.FulfillmentMethod == FulfillmentMethod.Delivery && o.AddressLine1 is not null
                ? new DeliveryAddress(o.AddressLine1, o.AddressLine2, o.City ?? "", o.District ?? "", o.PostalCode)
                : null,
            o.CustomerNotes,
            o.Items.Select(i => new OrderItemDto(i.ProductId, i.ProductName, i.ProductSlug, i.Sku, i.ImageUrl, i.UnitPrice, i.Quantity, i.LineTotal)).ToList(),
            o.Subtotal, o.DiscountTotal, o.DiscountCodeSnapshot, o.ShippingTotal, o.ShippingRateName, o.Total, o.RefundedTotal, o.Currency,
            o.TrackingCarrier, o.TrackingNumber, o.TrackingUrl, o.PlacedAt,
            o.History.OrderBy(h => h.CreatedAt).Select(h => new OrderHistoryDto(h.ToStatus, h.ToPaymentStatus, h.Note, h.CreatedAt)).ToList(),
            instructions,
            OrderRules.CustomerCanCancel(o.Status, o.PaymentStatus),
            o.PaymentMethod == PaymentMethod.BankTransfer && o.Status != OrderStatus.Cancelled &&
            o.PaymentStatus is PaymentStatus.Unpaid or PaymentStatus.Failed,
            o.CancellationReason);
    }
}
