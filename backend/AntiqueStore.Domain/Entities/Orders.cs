using AntiqueStore.Domain.Common;
using AntiqueStore.Domain.Enums;

namespace AntiqueStore.Domain.Entities;

public class Order : Entity
{
    public string OrderNumber { get; set; } = "";
    public int UserId { get; set; }
    public User? User { get; set; }
    /// <summary>Client-generated key; unique per user so retries never create duplicate orders.</summary>
    public string IdempotencyKey { get; set; } = "";

    public OrderStatus Status { get; set; } = OrderStatus.Pending;
    public PaymentStatus PaymentStatus { get; set; } = PaymentStatus.Unpaid;
    public PaymentMethod PaymentMethod { get; set; }
    public FulfillmentMethod FulfillmentMethod { get; set; }

    // Contact + delivery snapshot (kept even if the customer later edits their profile)
    public string CustomerName { get; set; } = "";
    public string CustomerEmail { get; set; } = "";
    public string CustomerPhone { get; set; } = "";
    public string? AddressLine1 { get; set; }
    public string? AddressLine2 { get; set; }
    public string? City { get; set; }
    public string? District { get; set; }
    public string? PostalCode { get; set; }
    public string Country { get; set; } = "Sri Lanka";
    public string? CustomerNotes { get; set; }

    // Money (server-calculated)
    public string Currency { get; set; } = "LKR";
    public decimal Subtotal { get; set; }
    public decimal DiscountTotal { get; set; }
    public decimal ShippingTotal { get; set; }
    public decimal Total { get; set; }
    public decimal RefundedTotal { get; set; }
    public int? DiscountCodeId { get; set; }
    public DiscountCode? DiscountCode { get; set; }
    public string? DiscountCodeSnapshot { get; set; }
    public string? ShippingRateName { get; set; }

    // Fulfilment
    public string? TrackingCarrier { get; set; }
    public string? TrackingNumber { get; set; }
    public string? TrackingUrl { get; set; }
    public string? AdminNotes { get; set; }
    public string? CancellationReason { get; set; }
    public DateTime PlacedAt { get; set; }
    public DateTime? ConfirmedAt { get; set; }
    public DateTime? ShippedAt { get; set; }
    public DateTime? DeliveredAt { get; set; }
    public DateTime? CancelledAt { get; set; }
    public DateTime? PaidAt { get; set; }
    /// <summary>For bank transfers: when the reserved items may be released if unpaid.</summary>
    public DateTime? PaymentDueAt { get; set; }
    public bool StockRestored { get; set; }

    public int Version { get; set; }

    public List<OrderItem> Items { get; set; } = [];
    public List<Payment> Payments { get; set; } = [];
    public List<OrderStatusHistory> History { get; set; } = [];
}

/// <summary>Price snapshot so historical orders stay accurate after product edits.</summary>
public class OrderItem : Entity
{
    public int OrderId { get; set; }
    public Order? Order { get; set; }
    public int ProductId { get; set; }
    public Product? Product { get; set; }
    public string ProductName { get; set; } = "";
    public string ProductSlug { get; set; } = "";
    public string Sku { get; set; } = "";
    public string? ImageUrl { get; set; }
    public decimal UnitPrice { get; set; }
    public int Quantity { get; set; }
    public decimal LineTotal { get; set; }
}

public class OrderStatusHistory
{
    public long Id { get; set; }
    public int OrderId { get; set; }
    public Order? Order { get; set; }
    public OrderStatus? FromStatus { get; set; }
    public OrderStatus? ToStatus { get; set; }
    public PaymentStatus? FromPaymentStatus { get; set; }
    public PaymentStatus? ToPaymentStatus { get; set; }
    public string? Note { get; set; }
    public int? ChangedByUserId { get; set; }
    public bool VisibleToCustomer { get; set; } = true;
    public DateTime CreatedAt { get; set; }
}

public class Payment : Entity
{
    public int OrderId { get; set; }
    public Order? Order { get; set; }
    public PaymentKind Kind { get; set; } = PaymentKind.Charge;
    public PaymentMethod Method { get; set; }
    public PaymentRecordStatus Status { get; set; } = PaymentRecordStatus.Pending;
    public decimal Amount { get; set; }
    public string Currency { get; set; } = "LKR";
    /// <summary>Customer-supplied bank reference, or provider transaction id for future gateways.</summary>
    public string? Reference { get; set; }
    public string? ProviderName { get; set; }
    public string? ProviderTransactionId { get; set; }
    public string? Notes { get; set; }
    public int? VerifiedByUserId { get; set; }
    public DateTime? VerifiedAt { get; set; }
    public List<PaymentProof> Proofs { get; set; } = [];
}

public class PaymentProof : Entity
{
    public int PaymentId { get; set; }
    public Payment? Payment { get; set; }
    /// <summary>Key in private (non-public) file storage. Served only through an authorized endpoint.</summary>
    public string StorageKey { get; set; } = "";
    public string OriginalFileName { get; set; } = "";
    public string ContentType { get; set; } = "";
    public long SizeBytes { get; set; }
    public int UploadedByUserId { get; set; }
}
