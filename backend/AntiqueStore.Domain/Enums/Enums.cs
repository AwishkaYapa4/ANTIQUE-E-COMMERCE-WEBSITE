namespace AntiqueStore.Domain.Enums;

public enum UserRole
{
    Customer = 0,
    Admin = 1
}

/// <summary>Publication state of a product (independent from sellable inventory).</summary>
public enum ProductStatus
{
    Draft = 0,
    Published = 1,
    Archived = 2
}

/// <summary>
/// Customer-facing inventory state. Derived from publication status and stock counters,
/// never stored directly, so it can never drift out of sync with the counters.
/// </summary>
public enum InventoryState
{
    Draft = 0,
    Available = 1,
    Reserved = 2,
    Sold = 3,
    Archived = 4
}

public enum ProductCondition
{
    Excellent = 0,
    VeryGood = 1,
    Good = 2,
    Fair = 3,
    Restored = 4,
    ForRestoration = 5
}

public enum ReservationStatus
{
    Active = 0,
    Committed = 1,
    Released = 2,
    Expired = 3
}

public enum InventoryMovementType
{
    InitialStock = 0,
    Reserved = 1,
    ReservationReleased = 2,
    ReservationExpired = 3,
    Sold = 4,
    OrderCancelledRestock = 5,
    ManualAdjustment = 6,
    MarkedSold = 7
}

public enum OrderStatus
{
    Pending = 0,
    Confirmed = 1,
    Processing = 2,
    Shipped = 3,
    Delivered = 4,
    Cancelled = 5
}

public enum PaymentStatus
{
    Unpaid = 0,
    PendingVerification = 1,
    Paid = 2,
    Failed = 3,
    Refunded = 4
}

public enum PaymentMethod
{
    CashOnDelivery = 0,
    BankTransfer = 1
}

public enum PaymentKind
{
    Charge = 0,
    Refund = 1
}

public enum PaymentRecordStatus
{
    Pending = 0,
    Succeeded = 1,
    Failed = 2
}

public enum FulfillmentMethod
{
    Delivery = 0,
    LocalPickup = 1
}

public enum ReviewStatus
{
    Pending = 0,
    Approved = 1,
    Rejected = 2
}

public enum EnquiryType
{
    Product = 0,
    Contact = 1
}

public enum EnquiryStatus
{
    New = 0,
    InProgress = 1,
    Resolved = 2,
    Spam = 3
}

public enum DiscountType
{
    Percentage = 0,
    FixedAmount = 1
}

public enum NewsletterStatus
{
    Subscribed = 0,
    Unsubscribed = 1
}
