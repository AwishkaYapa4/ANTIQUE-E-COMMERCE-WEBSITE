using AntiqueStore.Application.Common;
using AntiqueStore.Domain.Entities;
using AntiqueStore.Domain.Enums;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace AntiqueStore.Application.Inventory;

/// <summary>
/// The only component allowed to change product stock counters.
///
/// Concurrency model: every change is a single conditional UPDATE, e.g.
///   UPDATE Products SET QuantityAvailable = QuantityAvailable - @q, QuantityReserved = QuantityReserved + @q
///   WHERE Id = @id AND Status = Published AND QuantityAvailable >= @q
/// The database evaluates the WHERE clause under a row lock, so when two shoppers race for the last
/// unit exactly one UPDATE affects a row and the other affects zero rows. Stock can therefore never
/// become negative (also enforced by CHECK constraints). Reservation state changes use the same pattern
/// (WHERE Status = Active), so a reservation is consumed or released exactly once.
/// Callers that combine several changes wrap them in a transaction.
/// </summary>
public sealed class InventoryService(IAppDbContext db, TimeProvider clock, ILogger<InventoryService> logger)
{
    private DateTime Now => clock.GetUtcNow().UtcDateTime;

    /// <summary>Atomically moves <paramref name="quantity"/> units from available to reserved.</summary>
    public async Task<bool> TryHoldStockAsync(int productId, int quantity, CancellationToken ct)
    {
        if (quantity <= 0) throw new ArgumentOutOfRangeException(nameof(quantity));
        var affected = await db.Products
            .Where(p => p.Id == productId && p.Status == ProductStatus.Published && p.QuantityAvailable >= quantity)
            .ExecuteUpdateAsync(s => s
                .SetProperty(p => p.QuantityAvailable, p => p.QuantityAvailable - quantity)
                .SetProperty(p => p.QuantityReserved, p => p.QuantityReserved + quantity), ct);
        return affected == 1;
    }

    /// <summary>Creates a reservation after successfully holding stock. Returns null if the stock is gone.</summary>
    public async Task<InventoryReservation?> TryReserveAsync(int productId, int userId, int quantity, string checkoutToken,
        DateTime expiresAt, CancellationToken ct)
    {
        if (!await TryHoldStockAsync(productId, quantity, ct)) return null;

        var reservation = new InventoryReservation
        {
            ProductId = productId, UserId = userId, Quantity = quantity, CheckoutToken = checkoutToken,
            ExpiresAt = expiresAt, Status = ReservationStatus.Active
        };
        db.InventoryReservations.Add(reservation);
        db.InventoryMovements.Add(new InventoryMovement
        {
            ProductId = productId, Type = InventoryMovementType.Reserved, AvailableChange = -quantity,
            ReservedChange = quantity, UserId = userId, Reason = "Checkout reservation", CreatedAt = Now
        });
        await db.SaveChangesAsync(ct);
        return reservation;
    }

    /// <summary>
    /// Releases an active reservation back to available stock. Safe to call concurrently with expiry or
    /// order placement: only the caller that flips the status from Active wins.
    /// </summary>
    public async Task<bool> ReleaseReservationAsync(int reservationId, ReservationStatus finalStatus, string reason, CancellationToken ct)
    {
        if (finalStatus is not (ReservationStatus.Released or ReservationStatus.Expired))
            throw new ArgumentOutOfRangeException(nameof(finalStatus));

        var reservation = await db.InventoryReservations.AsNoTracking()
            .Where(r => r.Id == reservationId).Select(r => new { r.ProductId, r.Quantity, r.UserId }).FirstOrDefaultAsync(ct);
        if (reservation is null) return false;

        var now = Now;
        var flipped = await db.InventoryReservations
            .Where(r => r.Id == reservationId && r.Status == ReservationStatus.Active)
            .ExecuteUpdateAsync(s => s.SetProperty(r => r.Status, finalStatus).SetProperty(r => r.ReleasedAt, now), ct);
        if (flipped == 0) return false;

        var restored = await db.Products
            .Where(p => p.Id == reservation.ProductId && p.QuantityReserved >= reservation.Quantity)
            .ExecuteUpdateAsync(s => s
                .SetProperty(p => p.QuantityReserved, p => p.QuantityReserved - reservation.Quantity)
                .SetProperty(p => p.QuantityAvailable, p => p.QuantityAvailable + reservation.Quantity), ct);
        if (restored == 0)
        {
            // Should be impossible given the counters' invariants; surface loudly instead of hiding it.
            logger.LogError("Inventory invariant violated releasing reservation {ReservationId} for product {ProductId}",
                reservationId, reservation.ProductId);
            throw new ConflictException("Inventory could not be released consistently. Please retry.");
        }

        db.InventoryMovements.Add(new InventoryMovement
        {
            ProductId = reservation.ProductId, ReservationId = reservationId,
            Type = finalStatus == ReservationStatus.Expired ? InventoryMovementType.ReservationExpired : InventoryMovementType.ReservationReleased,
            AvailableChange = reservation.Quantity, ReservedChange = -reservation.Quantity,
            UserId = reservation.UserId, Reason = reason, CreatedAt = now
        });
        await db.SaveChangesAsync(ct);
        return true;
    }

    /// <summary>Marks an active reservation as consumed by an order. Returns false if it was already released.</summary>
    public async Task<bool> TryCommitReservationAsync(int reservationId, int orderId, CancellationToken ct)
    {
        var affected = await db.InventoryReservations
            .Where(r => r.Id == reservationId && r.Status == ReservationStatus.Active)
            .ExecuteUpdateAsync(s => s.SetProperty(r => r.Status, ReservationStatus.Committed).SetProperty(r => r.OrderId, orderId), ct);
        return affected == 1;
    }

    /// <summary>Moves held units from reserved to sold once an order is placed.</summary>
    public async Task MoveReservedToSoldAsync(int productId, int quantity, int orderId, int? userId, CancellationToken ct)
    {
        var now = Now;
        var affected = await db.Products
            .Where(p => p.Id == productId && p.QuantityReserved >= quantity)
            .ExecuteUpdateAsync(s => s
                .SetProperty(p => p.QuantityReserved, p => p.QuantityReserved - quantity)
                .SetProperty(p => p.QuantitySold, p => p.QuantitySold + quantity)
                .SetProperty(p => p.SoldAt, now), ct);
        if (affected == 0) throw new ConflictException("Inventory changed while placing the order. Please try again.");

        db.InventoryMovements.Add(new InventoryMovement
        {
            ProductId = productId, OrderId = orderId, Type = InventoryMovementType.Sold,
            ReservedChange = -quantity, SoldChange = quantity, UserId = userId, Reason = "Order placed", CreatedAt = now
        });
    }

    /// <summary>Returns sold units to available stock (e.g. order cancelled before shipping).</summary>
    public async Task RestockSoldAsync(int productId, int quantity, int orderId, int? userId, string reason, CancellationToken ct)
    {
        var affected = await db.Products
            .Where(p => p.Id == productId && p.QuantitySold >= quantity)
            .ExecuteUpdateAsync(s => s
                .SetProperty(p => p.QuantitySold, p => p.QuantitySold - quantity)
                .SetProperty(p => p.QuantityAvailable, p => p.QuantityAvailable + quantity)
                .SetProperty(p => p.SoldAt, (DateTime?)null), ct);
        if (affected == 0) throw new ConflictException("Stock could not be restored because the sold quantity changed.");

        db.InventoryMovements.Add(new InventoryMovement
        {
            ProductId = productId, OrderId = orderId, Type = InventoryMovementType.OrderCancelledRestock,
            AvailableChange = quantity, SoldChange = -quantity, UserId = userId, Reason = reason, CreatedAt = Now
        });
    }

    /// <summary>
    /// Administrative correction of available stock. Uses compare-and-set on the previous value so a
    /// concurrent checkout can't be overwritten; reserved units are never touched.
    /// </summary>
    public async Task<int> AdjustAvailableAsync(int productId, int newAvailable, int adminUserId, string reason, CancellationToken ct)
    {
        if (newAvailable < 0) throw RequestValidationException.For("quantityAvailable", "Quantity cannot be negative.");
        var current = await db.Products.AsNoTracking().Where(p => p.Id == productId)
            .Select(p => (int?)p.QuantityAvailable).FirstOrDefaultAsync(ct) ?? throw new NotFoundException("Product", productId);

        var affected = await db.Products
            .Where(p => p.Id == productId && p.QuantityAvailable == current)
            .ExecuteUpdateAsync(s => s.SetProperty(p => p.QuantityAvailable, newAvailable), ct);
        if (affected == 0) throw new ConflictException("Stock changed while you were editing. Please reload and try again.");

        db.InventoryMovements.Add(new InventoryMovement
        {
            ProductId = productId, Type = InventoryMovementType.ManualAdjustment, AvailableChange = newAvailable - current,
            UserId = adminUserId, Reason = reason, CreatedAt = Now
        });
        return newAvailable - current;
    }

    /// <summary>Marks all currently available units as sold outside the website (e.g. sold in the shop).</summary>
    public async Task<int> MarkSoldOfflineAsync(int productId, int adminUserId, string reason, CancellationToken ct)
    {
        var current = await db.Products.AsNoTracking().Where(p => p.Id == productId)
            .Select(p => (int?)p.QuantityAvailable).FirstOrDefaultAsync(ct) ?? throw new NotFoundException("Product", productId);
        if (current == 0) return 0;

        var now = Now;
        var affected = await db.Products
            .Where(p => p.Id == productId && p.QuantityAvailable == current)
            .ExecuteUpdateAsync(s => s
                .SetProperty(p => p.QuantityAvailable, 0)
                .SetProperty(p => p.QuantitySold, p => p.QuantitySold + current)
                .SetProperty(p => p.SoldAt, now), ct);
        if (affected == 0) throw new ConflictException("Stock changed while you were editing. Please reload and try again.");

        db.InventoryMovements.Add(new InventoryMovement
        {
            ProductId = productId, Type = InventoryMovementType.MarkedSold, AvailableChange = -current, SoldChange = current,
            UserId = adminUserId, Reason = reason, CreatedAt = now
        });
        return current;
    }

    /// <summary>Releases all active reservations of a user (e.g. when they restart checkout).</summary>
    public async Task ReleaseUserReservationsAsync(int userId, CancellationToken ct)
    {
        var ids = await db.InventoryReservations.AsNoTracking()
            .Where(r => r.UserId == userId && r.Status == ReservationStatus.Active)
            .Select(r => r.Id).ToListAsync(ct);
        foreach (var id in ids)
            await ReleaseReservationAsync(id, ReservationStatus.Released, "Checkout restarted", ct);
    }

    /// <summary>Expires reservations past their deadline. Called by the background sweeper.</summary>
    public async Task<int> ExpireReservationsAsync(int batchSize, CancellationToken ct)
    {
        var now = Now;
        var expired = await db.InventoryReservations.AsNoTracking()
            .Where(r => r.Status == ReservationStatus.Active && r.ExpiresAt <= now)
            .OrderBy(r => r.ExpiresAt).Select(r => r.Id).Take(batchSize).ToListAsync(ct);

        var count = 0;
        foreach (var id in expired)
        {
            await using var tx = await db.Database.BeginTransactionAsync(ct);
            if (await ReleaseReservationAsync(id, ReservationStatus.Expired, "Reservation expired", ct))
            {
                count++;
            }
            await tx.CommitAsync(ct);
        }
        return count;
    }
}
