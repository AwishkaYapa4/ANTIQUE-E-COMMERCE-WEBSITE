using AntiqueStore.Application.Common;
using AntiqueStore.Domain.Entities;
using AntiqueStore.Domain.Enums;
using AntiqueStore.Domain.Rules;
using Microsoft.EntityFrameworkCore;

namespace AntiqueStore.Application.Orders;

public sealed record AdminOrderQuery : PageRequest
{
    public string? Q { get; init; }
    public OrderStatus? Status { get; init; }
    public PaymentStatus? PaymentStatus { get; init; }
    public PaymentMethod? PaymentMethod { get; init; }
    public DateTime? From { get; init; }
    public DateTime? To { get; init; }
}

public sealed record AdminOrderListItemDto(
    int Id, string OrderNumber, string CustomerName, string CustomerEmail, OrderStatus Status, PaymentStatus PaymentStatus,
    PaymentMethod PaymentMethod, FulfillmentMethod FulfillmentMethod, decimal Total, int ItemCount, DateTime PlacedAt);

public sealed record PaymentProofDto(int Id, string FileName, string ContentType, long SizeBytes, DateTime UploadedAt);

public sealed record PaymentRecordDto(
    int Id, PaymentKind Kind, PaymentMethod Method, PaymentRecordStatus Status, decimal Amount, string? Reference,
    string? Notes, DateTime? VerifiedAt, string? VerifiedBy, DateTime CreatedAt, IReadOnlyList<PaymentProofDto> Proofs);

public sealed record AdminHistoryDto(
    OrderStatus? FromStatus, OrderStatus? ToStatus, PaymentStatus? FromPaymentStatus, PaymentStatus? ToPaymentStatus,
    string? Note, string? ChangedBy, bool VisibleToCustomer, DateTime CreatedAt);

public sealed record AdminOrderDto(
    OrderDto Order, int UserId, string? CustomerAccountEmail, int CustomerOrderCount, string? AdminNotes, int Version,
    DateTime? PaymentDueAt, bool StockRestored,
    IReadOnlyList<PaymentRecordDto> Payments, IReadOnlyList<AdminHistoryDto> History,
    IReadOnlyList<OrderStatus> NextStatuses, IReadOnlyList<PaymentStatus> NextPaymentStatuses);

public sealed record UpdateOrderStatusRequest(
    OrderStatus Status, string? Note, bool Restock = true, string? TrackingCarrier = null, string? TrackingNumber = null,
    string? TrackingUrl = null, int? ExpectedVersion = null);

public sealed record UpdatePaymentStatusRequest(PaymentStatus Status, string? Note, string? Reference = null);

public sealed record RecordRefundRequest(decimal Amount, string? Note, string? Reference);

public sealed record UpdateOrderDetailsRequest(string? AdminNotes, string? TrackingCarrier, string? TrackingNumber, string? TrackingUrl);

public sealed record ProofFile(Stream Content, string ContentType, string FileName);

public sealed class AdminOrderService(
    IAppDbContext db, OrderLifecycle lifecycle, OrderQueries queries, IAuditLogger audit, ICurrentUser currentUser,
    IPrivateFileStorage privateFiles, TimeProvider clock)
{
    public async Task<PagedResult<AdminOrderListItemDto>> ListAsync(AdminOrderQuery q, CancellationToken ct)
    {
        var orders = db.Orders.AsNoTracking();
        if (!string.IsNullOrWhiteSpace(q.Q))
        {
            var term = q.Q.Trim();
            orders = orders.Where(o => o.OrderNumber.Contains(term) || o.CustomerEmail.Contains(term) ||
                                       o.CustomerName.Contains(term) || o.CustomerPhone.Contains(term));
        }
        if (q.Status is { } s) orders = orders.Where(o => o.Status == s);
        if (q.PaymentStatus is { } ps) orders = orders.Where(o => o.PaymentStatus == ps);
        if (q.PaymentMethod is { } pm) orders = orders.Where(o => o.PaymentMethod == pm);
        if (q.From is { } from) orders = orders.Where(o => o.PlacedAt >= from);
        if (q.To is { } to) orders = orders.Where(o => o.PlacedAt < to.Date.AddDays(1));

        return await orders.OrderByDescending(o => o.PlacedAt).ToPagedAsync(q, x => x.Select(o => new AdminOrderListItemDto(
            o.Id, o.OrderNumber, o.CustomerName, o.CustomerEmail, o.Status, o.PaymentStatus, o.PaymentMethod,
            o.FulfillmentMethod, o.Total, o.Items.Sum(i => i.Quantity), o.PlacedAt)), ct);
    }

    public async Task<AdminOrderDto> GetAsync(int id, CancellationToken ct)
    {
        var order = await db.Orders.AsNoTracking()
            .Include(o => o.Items)
            .Include(o => o.History)
            .Include(o => o.Payments).ThenInclude(p => p.Proofs)
            .Include(o => o.User)
            .AsSplitQuery()
            .FirstOrDefaultAsync(o => o.Id == id, ct) ?? throw new NotFoundException("Order", id);

        var userIds = order.History.Select(h => h.ChangedByUserId).Concat(order.Payments.Select(p => p.VerifiedByUserId))
            .Where(x => x.HasValue).Select(x => x!.Value).Distinct().ToList();
        var names = await db.Users.AsNoTracking().Where(u => userIds.Contains(u.Id))
            .ToDictionaryAsync(u => u.Id, u => u.DisplayName ?? u.Email, ct);
        var orderCount = await db.Orders.CountAsync(o => o.UserId == order.UserId, ct);

        var customerView = await queries.ToDtoAsync(order, ct);
        return new AdminOrderDto(
            customerView, order.UserId, order.User?.Email, orderCount, order.AdminNotes, order.Version, order.PaymentDueAt, order.StockRestored,
            order.Payments.OrderBy(p => p.CreatedAt).Select(p => new PaymentRecordDto(
                p.Id, p.Kind, p.Method, p.Status, p.Amount, p.Reference, p.Notes, p.VerifiedAt,
                p.VerifiedByUserId is { } v && names.TryGetValue(v, out var n) ? n : null, p.CreatedAt,
                p.Proofs.Select(f => new PaymentProofDto(f.Id, f.OriginalFileName, f.ContentType, f.SizeBytes, f.CreatedAt)).ToList())).ToList(),
            order.History.OrderBy(h => h.CreatedAt).Select(h => new AdminHistoryDto(
                h.FromStatus, h.ToStatus, h.FromPaymentStatus, h.ToPaymentStatus, h.Note,
                h.ChangedByUserId is { } c && names.TryGetValue(c, out var cn) ? cn : null, h.VisibleToCustomer, h.CreatedAt)).ToList(),
            OrderRules.NextStatuses(order.Status), OrderRules.NextPaymentStatuses(order.PaymentStatus));
    }

    private async Task<Order> LoadAsync(int id, CancellationToken ct) =>
        await db.Orders.Include(o => o.Items).Include(o => o.Payments).FirstOrDefaultAsync(o => o.Id == id, ct)
        ?? throw new NotFoundException("Order", id);

    public async Task<AdminOrderDto> UpdateStatusAsync(int id, UpdateOrderStatusRequest r, CancellationToken ct)
    {
        var adminId = currentUser.RequireUserId();
        await using var tx = await db.Database.BeginTransactionAsync(ct);
        var order = await LoadAsync(id, ct);
        if (r.ExpectedVersion is { } v && v != order.Version)
            throw new ConflictException("This order was updated by someone else. Please reload.");

        var from = order.Status;
        if (r.Status == OrderStatus.Cancelled)
        {
            await lifecycle.CancelAsync(order, Text.TrimToNull(r.Note) ?? "Cancelled by store", adminId, r.Restock, ct);
        }
        else
        {
            if (r.Status == OrderStatus.Shipped && order.FulfillmentMethod == FulfillmentMethod.Delivery)
            {
                order.TrackingCarrier = Text.TrimToNull(r.TrackingCarrier) ?? order.TrackingCarrier;
                order.TrackingNumber = Text.TrimToNull(r.TrackingNumber) ?? order.TrackingNumber;
                order.TrackingUrl = Text.TrimToNull(r.TrackingUrl) ?? order.TrackingUrl;
            }
            lifecycle.SetStatus(order, r.Status, Text.TrimToNull(r.Note), adminId);
            // Cash on delivery is collected on delivery; the admin still records it explicitly via the payment status.
        }

        audit.Log("order.status", "Order", order.Id, $"{order.OrderNumber}: {from} → {r.Status}", new { r.Note, r.Restock });
        await SaveAsync(ct);
        await tx.CommitAsync(ct);
        return await GetAsync(id, ct);
    }

    public async Task<AdminOrderDto> UpdatePaymentStatusAsync(int id, UpdatePaymentStatusRequest r, CancellationToken ct)
    {
        var adminId = currentUser.RequireUserId();
        if (r.Status == PaymentStatus.Refunded)
            throw new BusinessRuleException("Use 'Record refund' to refund an order.", "use_refund");

        await using var tx = await db.Database.BeginTransactionAsync(ct);
        var order = await LoadAsync(id, ct);
        if (r.Status == PaymentStatus.Paid && order.Status == OrderStatus.Cancelled)
            throw new BusinessRuleException("A cancelled order cannot be marked as paid.", "order_cancelled");

        var from = order.PaymentStatus;
        lifecycle.SetPaymentStatus(order, r.Status, Text.TrimToNull(r.Note), adminId);

        var charge = order.Payments.Where(p => p.Kind == PaymentKind.Charge).OrderByDescending(p => p.Id).FirstOrDefault();
        if (charge is null)
        {
            charge = new Payment { Method = order.PaymentMethod, Amount = order.Total, Currency = order.Currency, ProviderName = order.PaymentMethod.ToString() };
            order.Payments.Add(charge);
        }
        if (Text.TrimToNull(r.Reference) is { } reference) charge.Reference = reference;
        if (r.Status is PaymentStatus.Paid or PaymentStatus.Failed)
        {
            charge.Status = r.Status == PaymentStatus.Paid ? PaymentRecordStatus.Succeeded : PaymentRecordStatus.Failed;
            charge.VerifiedByUserId = adminId;
            charge.VerifiedAt = clock.GetUtcNow().UtcDateTime;
            charge.Notes = Text.TrimToNull(r.Note) ?? charge.Notes;
        }
        else
        {
            charge.Status = PaymentRecordStatus.Pending;
        }

        audit.Log("order.payment", "Order", order.Id, $"{order.OrderNumber}: payment {from} → {r.Status}", new { r.Note, r.Reference });
        await SaveAsync(ct);
        await tx.CommitAsync(ct);
        return await GetAsync(id, ct);
    }

    public async Task<AdminOrderDto> RecordRefundAsync(int id, RecordRefundRequest r, CancellationToken ct)
    {
        var adminId = currentUser.RequireUserId();
        await using var tx = await db.Database.BeginTransactionAsync(ct);
        var order = await LoadAsync(id, ct);
        if (order.PaymentStatus != PaymentStatus.Paid)
            throw new BusinessRuleException("Only paid orders can be refunded.", "not_paid");
        var refundable = order.Total - order.RefundedTotal;
        if (r.Amount <= 0 || r.Amount > refundable)
            throw RequestValidationException.For("amount", $"Refund must be between 0.01 and {refundable:N2}.");

        order.Payments.Add(new Payment
        {
            Kind = PaymentKind.Refund, Method = order.PaymentMethod, Status = PaymentRecordStatus.Succeeded, Amount = r.Amount,
            Currency = order.Currency, Reference = Text.TrimToNull(r.Reference), Notes = Text.TrimToNull(r.Note),
            VerifiedByUserId = adminId, VerifiedAt = clock.GetUtcNow().UtcDateTime, ProviderName = "Manual"
        });
        order.RefundedTotal += r.Amount;
        if (order.RefundedTotal >= order.Total)
            lifecycle.SetPaymentStatus(order, PaymentStatus.Refunded, Text.TrimToNull(r.Note) ?? "Order refunded", adminId);
        else
            lifecycle.AddHistory(order, null, null, $"Partial refund of {order.Currency} {r.Amount:N2} recorded", adminId);

        audit.Log("order.refund", "Order", order.Id, $"{order.OrderNumber}: refund {r.Amount:N2}", new { r.Note, r.Reference });
        await SaveAsync(ct);
        await tx.CommitAsync(ct);
        return await GetAsync(id, ct);
    }

    public async Task<AdminOrderDto> UpdateDetailsAsync(int id, UpdateOrderDetailsRequest r, CancellationToken ct)
    {
        var order = await LoadAsync(id, ct);
        order.AdminNotes = Text.TrimToNull(r.AdminNotes);
        order.TrackingCarrier = Text.TrimToNull(r.TrackingCarrier);
        order.TrackingNumber = Text.TrimToNull(r.TrackingNumber);
        order.TrackingUrl = Text.TrimToNull(r.TrackingUrl);
        audit.Log("order.details", "Order", order.Id, $"{order.OrderNumber}: notes/tracking updated");
        await SaveAsync(ct);
        return await GetAsync(id, ct);
    }

    public async Task<ProofFile> OpenProofAsync(int orderId, int proofId, CancellationToken ct)
    {
        var proof = await db.PaymentProofs.AsNoTracking()
            .FirstOrDefaultAsync(p => p.Id == proofId && p.Payment!.OrderId == orderId, ct) ?? throw new NotFoundException("Payment proof", proofId);
        var stream = await privateFiles.OpenReadAsync(proof.StorageKey, ct) ?? throw new NotFoundException("Payment proof file", proofId);
        return new ProofFile(stream, proof.ContentType, proof.OriginalFileName);
    }

    private async Task SaveAsync(CancellationToken ct)
    {
        try
        {
            await db.SaveChangesAsync(ct);
        }
        catch (DbUpdateConcurrencyException)
        {
            throw new ConflictException("This order was updated by someone else. Please reload.");
        }
    }
}
