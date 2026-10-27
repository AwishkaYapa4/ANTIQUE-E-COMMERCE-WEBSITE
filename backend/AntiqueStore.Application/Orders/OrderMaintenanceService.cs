using AntiqueStore.Application.Common;
using AntiqueStore.Application.Settings;
using AntiqueStore.Domain.Enums;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace AntiqueStore.Application.Orders;

/// <summary>
/// Releases stock held by bank-transfer orders the customer never paid for, after the configured
/// payment window. Orders with a submitted (pending verification) payment are never auto-cancelled.
/// </summary>
public sealed class OrderMaintenanceService(
    IAppDbContext db, OrderLifecycle lifecycle, CommerceSettingsService settings, TimeProvider clock,
    ILogger<OrderMaintenanceService> logger)
{
    public async Task<int> CancelOverdueBankTransfersAsync(int batchSize, CancellationToken ct)
    {
        var commerce = await settings.GetAsync(ct);
        if (commerce.BankTransferPaymentWindowHours <= 0) return 0;

        var now = clock.GetUtcNow().UtcDateTime;
        var overdue = await db.Orders.AsNoTracking()
            .Where(o => o.PaymentMethod == PaymentMethod.BankTransfer && o.Status == OrderStatus.Pending &&
                        o.PaymentStatus == PaymentStatus.Unpaid && o.PaymentDueAt != null && o.PaymentDueAt <= now)
            .OrderBy(o => o.PaymentDueAt).Select(o => o.Id).Take(batchSize).ToListAsync(ct);

        var cancelled = 0;
        foreach (var id in overdue)
        {
            await using var tx = await db.Database.BeginTransactionAsync(ct);
            var order = await db.Orders.Include(o => o.Items).FirstAsync(o => o.Id == id, ct);
            // Re-check inside the transaction: the customer may have just submitted payment.
            if (order.Status != OrderStatus.Pending || order.PaymentStatus != PaymentStatus.Unpaid)
            {
                await tx.RollbackAsync(ct);
                continue;
            }
            await lifecycle.CancelAsync(order, "Payment was not received within the payment window", null, restock: true, ct);
            try
            {
                await db.SaveChangesAsync(ct);
                await tx.CommitAsync(ct);
                cancelled++;
                logger.LogInformation("Auto-cancelled unpaid bank transfer order {OrderNumber}", order.OrderNumber);
            }
            catch (DbUpdateConcurrencyException)
            {
                await tx.RollbackAsync(ct);
                logger.LogInformation("Skipped auto-cancel of order {OrderId}: modified concurrently", id);
            }
            db.ChangeTracker.Clear();
        }
        return cancelled;
    }
}
