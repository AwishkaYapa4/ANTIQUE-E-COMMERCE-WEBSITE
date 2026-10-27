using AntiqueStore.Domain.Common;
using AntiqueStore.Domain.Enums;

namespace AntiqueStore.Domain.Entities;

/// <summary>
/// Temporary hold on stock during checkout. Created only by the backend after an atomic
/// conditional decrement of <see cref="Product.QuantityAvailable"/>.
/// </summary>
public class InventoryReservation : Entity
{
    public int ProductId { get; set; }
    public Product? Product { get; set; }
    public int UserId { get; set; }
    public User? User { get; set; }
    /// <summary>Groups the reservations created for one checkout session.</summary>
    public string CheckoutToken { get; set; } = "";
    public int Quantity { get; set; }
    public ReservationStatus Status { get; set; } = ReservationStatus.Active;
    public DateTime ExpiresAt { get; set; }
    public DateTime? ReleasedAt { get; set; }
    public int? OrderId { get; set; }
    public Order? Order { get; set; }
}

/// <summary>Immutable ledger of every stock change, for auditing and corrections.</summary>
public class InventoryMovement
{
    public long Id { get; set; }
    public int ProductId { get; set; }
    public Product? Product { get; set; }
    public InventoryMovementType Type { get; set; }
    public int AvailableChange { get; set; }
    public int ReservedChange { get; set; }
    public int SoldChange { get; set; }
    public string? Reason { get; set; }
    public int? OrderId { get; set; }
    public int? ReservationId { get; set; }
    public int? UserId { get; set; }
    public DateTime CreatedAt { get; set; }
}
