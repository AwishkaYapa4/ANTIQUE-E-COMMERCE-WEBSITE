using AntiqueStore.Application.Carts;
using AntiqueStore.Application.Payments;
using AntiqueStore.Domain.Enums;

namespace AntiqueStore.Application.Orders;

public sealed record CheckoutQuoteRequest(
    FulfillmentMethod FulfillmentMethod, string? District, string? DiscountCode, PaymentMethod? PaymentMethod);

public sealed record ShippingQuoteDto(bool IsAvailable, decimal Fee, string? RateName, string? EstimatedDelivery, string? UnavailableReason);

public sealed record CheckoutQuoteDto(
    IReadOnlyList<CartLineDto> Lines, decimal Subtotal, decimal Discount, string? DiscountCode, string? DiscountError,
    ShippingQuoteDto Shipping, decimal Total, string Currency, IReadOnlyList<PaymentMethodDto> PaymentMethods,
    bool CanPlaceOrder, IReadOnlyList<string> Issues);

public sealed record ReservationLineDto(int ProductId, string Name, int Quantity);

public sealed record CheckoutReservationDto(string CheckoutToken, DateTime ExpiresAt, IReadOnlyList<ReservationLineDto> Items);

public sealed record ContactInfo(string FullName, string Email, string Phone);

public sealed record DeliveryAddress(string Line1, string? Line2, string City, string District, string? PostalCode);

public sealed record PlaceOrderRequest(
    string IdempotencyKey, string? CheckoutToken, ContactInfo Contact, FulfillmentMethod FulfillmentMethod,
    DeliveryAddress? Address, PaymentMethod PaymentMethod, string? DiscountCode, string? Notes,
    bool SaveAddress, decimal? ExpectedTotal);

public sealed record OrderItemDto(int ProductId, string ProductName, string ProductSlug, string Sku, string? ImageUrl,
    decimal UnitPrice, int Quantity, decimal LineTotal);

public sealed record OrderHistoryDto(OrderStatus? Status, PaymentStatus? PaymentStatus, string? Note, DateTime CreatedAt);

public sealed record OrderSummaryDto(
    int Id, string OrderNumber, OrderStatus Status, PaymentStatus PaymentStatus, PaymentMethod PaymentMethod,
    decimal Total, string Currency, int ItemCount, string? FirstItemImage, DateTime PlacedAt);

public sealed record OrderDto(
    int Id, string OrderNumber, OrderStatus Status, PaymentStatus PaymentStatus, PaymentMethod PaymentMethod,
    FulfillmentMethod FulfillmentMethod, ContactInfo Contact, DeliveryAddress? Address, string? Notes,
    IReadOnlyList<OrderItemDto> Items, decimal Subtotal, decimal DiscountTotal, string? DiscountCode,
    decimal ShippingTotal, string? ShippingRateName, decimal Total, decimal RefundedTotal, string Currency,
    string? TrackingCarrier, string? TrackingNumber, string? TrackingUrl, DateTime PlacedAt,
    IReadOnlyList<OrderHistoryDto> History, PaymentInstructionsDto? PaymentInstructions,
    bool CanCancel, bool CanSubmitPayment, string? CancellationReason);

public sealed record PlaceOrderResult(OrderDto Order, bool WasDuplicate);

public sealed record SubmitPaymentReferenceRequest(string Reference, string? Notes);

public sealed record CancelOrderRequest(string? Reason);
