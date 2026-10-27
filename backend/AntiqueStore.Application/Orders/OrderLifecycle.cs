using AntiqueStore.Application.Common;
using AntiqueStore.Application.Inventory;
using AntiqueStore.Application.Promotions;
using AntiqueStore.Domain.Entities;
using AntiqueStore.Domain.Enums;
using AntiqueStore.Domain.Rules;

namespace AntiqueStore.Application.Orders;

/// <summary>Shared order state transitions used by customer, admin and background flows.</summary>
public sealed class OrderLifecycle(IAppDbContext db, InventoryService inventory, DiscountService discounts, TimeProvider clock)
{
    public void AddHistory(Order order, OrderStatus? toStatus, PaymentStatus? toPayment, string? note, int? userId, bool visibleToCustomer = true)
    {
        order.History.Add(new OrderStatusHistory
        {
            FromStatus = toStatus is null ? null : order.Status,
            ToStatus = toStatus,
            FromPaymentStatus = toPayment is null ? null : order.PaymentStatus,
            ToPaymentStatus = toPayment,
            Note = note,
            ChangedByUserId = userId,
            VisibleToCustomer = visibleToCustomer,
            CreatedAt = clock.GetUtcNow().UtcDateTime
        });
    }

    public void SetStatus(Order order, OrderStatus to, string? note, int? userId)
    {
        if (order.Status == to) return;
        if (!OrderRules.CanTransition(order.Status, to))
            throw new BusinessRuleException($"An order cannot move from {order.Status} to {to}.", "invalid_transition");
        AddHistory(order, to, null, note, userId);
        var now = clock.GetUtcNow().UtcDateTime;
        order.Status = to;
        switch (to)
        {
            case OrderStatus.Confirmed: order.ConfirmedAt = now; break;
            case OrderStatus.Shipped: order.ShippedAt = now; break;
            case OrderStatus.Delivered: order.DeliveredAt = now; break;
            case OrderStatus.Cancelled: order.CancelledAt = now; break;
        }
    }

    public void SetPaymentStatus(Order order, PaymentStatus to, string? note, int? userId)
    {
        if (order.PaymentStatus == to) return;
        if (!OrderRules.CanTransition(order.PaymentStatus, to))
            throw new BusinessRuleException($"Payment cannot move from {order.PaymentStatus} to {to}.", "invalid_payment_transition");
        AddHistory(order, null, to, note, userId);
        order.PaymentStatus = to;
        if (to == PaymentStatus.Paid) order.PaidAt = clock.GetUtcNow().UtcDateTime;
    }

    /// <summary>
    /// Cancels an order. Stock returns to sale only while the item is still with the shop, and only once
    /// (guarded by <see cref="Order.StockRestored"/>). Must run inside a transaction.
    /// </summary>
    public async Task CancelAsync(Order order, string? reason, int? userId, bool restock, CancellationToken ct)
    {
        if (order.Status == OrderStatus.Cancelled) throw new BusinessRuleException("This order is already cancelled.", "already_cancelled");
        var canRestock = OrderRules.CanRestockOnCancel(order.Status);
        SetStatus(order, OrderStatus.Cancelled, reason is null ? "Order cancelled" : $"Order cancelled: {reason}", userId);
        order.CancellationReason = reason;

        if (restock && canRestock && !order.StockRestored)
        {
            foreach (var item in order.Items)
                await inventory.RestockSoldAsync(item.ProductId, item.Quantity, order.Id, userId, $"Order {order.OrderNumber} cancelled", ct);
            order.StockRestored = true;
        }

        await discounts.ReleaseUsageAsync(order.Id, ct);

        // Pending, unverified payments are void once the order is cancelled. Verified payments stay and
        // must be refunded explicitly by an admin.
        if (order.PaymentStatus is PaymentStatus.PendingVerification)
            SetPaymentStatus(order, PaymentStatus.Failed, "Payment voided because the order was cancelled", userId);
    }
}
