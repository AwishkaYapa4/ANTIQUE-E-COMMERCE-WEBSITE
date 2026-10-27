using AntiqueStore.Domain.Common;
using AntiqueStore.Domain.Enums;

namespace AntiqueStore.Domain.Entities;

public class DiscountCode : Entity
{
    /// <summary>Stored upper-case; lookups are case-insensitive.</summary>
    public string Code { get; set; } = "";
    public string? Description { get; set; }
    public DiscountType Type { get; set; }
    public decimal Value { get; set; }
    public decimal? MinOrderSubtotal { get; set; }
    public decimal? MaxDiscountAmount { get; set; }
    public DateTime? StartsAt { get; set; }
    public DateTime? EndsAt { get; set; }
    public int? UsageLimit { get; set; }
    public int? UsageLimitPerCustomer { get; set; }
    public int UsedCount { get; set; }
    public bool IsActive { get; set; } = true;
}

public class DiscountUsage
{
    public long Id { get; set; }
    public int DiscountCodeId { get; set; }
    public DiscountCode? DiscountCode { get; set; }
    public int OrderId { get; set; }
    public Order? Order { get; set; }
    public int UserId { get; set; }
    public decimal Amount { get; set; }
    public DateTime CreatedAt { get; set; }
}

public class ShippingZone : Entity
{
    public string Name { get; set; } = "";
    public bool IsActive { get; set; } = true;
    public bool CashOnDeliveryAvailable { get; set; } = true;
    public string? EstimatedDelivery { get; set; }
    public int SortOrder { get; set; }
    public List<ShippingZoneDistrict> Districts { get; set; } = [];
    public List<ShippingRate> Rates { get; set; } = [];
}

public class ShippingZoneDistrict
{
    public int Id { get; set; }
    public int ShippingZoneId { get; set; }
    public ShippingZone? ShippingZone { get; set; }
    public string District { get; set; } = "";
}

public class ShippingRate : Entity
{
    public int ShippingZoneId { get; set; }
    public ShippingZone? ShippingZone { get; set; }
    public string Name { get; set; } = "";
    public decimal Fee { get; set; }
    public decimal? MinSubtotal { get; set; }
    public decimal? MaxSubtotal { get; set; }
    public bool IsActive { get; set; } = true;
}

/// <summary>Operational commerce settings. These apply immediately (no draft stage).</summary>
public class CommerceSettings : Entity
{
    public string Currency { get; set; } = "LKR";
    public string OrderNumberPrefix { get; set; } = "AH";
    public int ReservationMinutes { get; set; } = 15;
    public int BankTransferPaymentWindowHours { get; set; } = 48;
    public decimal? FreeShippingThreshold { get; set; }
    public bool LocalPickupEnabled { get; set; }
    public string? LocalPickupAddress { get; set; }
    public string? LocalPickupInstructions { get; set; }
    public string? DeliveryInstructions { get; set; }
    public bool CashOnDeliveryEnabled { get; set; } = true;
    public decimal? CashOnDeliveryMaxOrderValue { get; set; }
    public bool BankTransferEnabled { get; set; } = true;
    public string? BankName { get; set; }
    public string? BankAccountName { get; set; }
    public string? BankAccountNumber { get; set; }
    public string? BankBranch { get; set; }
    public string? BankTransferInstructions { get; set; }
    public bool ReviewsEnabled { get; set; } = true;
    public string? ReturnsSummary { get; set; }
}
