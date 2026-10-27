using AntiqueStore.Domain.Enums;

namespace AntiqueStore.Domain.Rules;

/// <summary>Order and payment state machines. Order and payment status are deliberately independent.</summary>
public static class OrderRules
{
    private static readonly Dictionary<OrderStatus, OrderStatus[]> OrderTransitions = new()
    {
        [OrderStatus.Pending] = [OrderStatus.Confirmed, OrderStatus.Cancelled],
        [OrderStatus.Confirmed] = [OrderStatus.Processing, OrderStatus.Shipped, OrderStatus.Cancelled],
        [OrderStatus.Processing] = [OrderStatus.Shipped, OrderStatus.Cancelled],
        [OrderStatus.Shipped] = [OrderStatus.Delivered],
        [OrderStatus.Delivered] = [],
        [OrderStatus.Cancelled] = []
    };

    private static readonly Dictionary<PaymentStatus, PaymentStatus[]> PaymentTransitions = new()
    {
        [PaymentStatus.Unpaid] = [PaymentStatus.PendingVerification, PaymentStatus.Paid, PaymentStatus.Failed],
        [PaymentStatus.PendingVerification] = [PaymentStatus.Paid, PaymentStatus.Failed, PaymentStatus.Unpaid],
        [PaymentStatus.Failed] = [PaymentStatus.PendingVerification, PaymentStatus.Paid, PaymentStatus.Unpaid],
        [PaymentStatus.Paid] = [PaymentStatus.Refunded],
        [PaymentStatus.Refunded] = []
    };

    public static bool CanTransition(OrderStatus from, OrderStatus to) =>
        OrderTransitions.TryGetValue(from, out var allowed) && allowed.Contains(to);

    public static bool CanTransition(PaymentStatus from, PaymentStatus to) =>
        PaymentTransitions.TryGetValue(from, out var allowed) && allowed.Contains(to);

    public static IReadOnlyList<OrderStatus> NextStatuses(OrderStatus from) => OrderTransitions[from];

    public static IReadOnlyList<PaymentStatus> NextPaymentStatuses(PaymentStatus from) => PaymentTransitions[from];

    /// <summary>Orders that have left the shop cannot have their stock automatically restored.</summary>
    public static bool CanRestockOnCancel(OrderStatus status) =>
        status is OrderStatus.Pending or OrderStatus.Confirmed or OrderStatus.Processing;

    /// <summary>Customers can cancel only before the shop starts processing.</summary>
    public static bool CustomerCanCancel(OrderStatus status, PaymentStatus payment) =>
        status == OrderStatus.Pending && payment is not PaymentStatus.Paid;
}

public static class SriLanka
{
    public static readonly IReadOnlyList<string> Districts =
    [
        "Ampara", "Anuradhapura", "Badulla", "Batticaloa", "Colombo", "Galle", "Gampaha",
        "Hambantota", "Jaffna", "Kalutara", "Kandy", "Kegalle", "Kilinochchi", "Kurunegala",
        "Mannar", "Matale", "Matara", "Monaragala", "Mullaitivu", "Nuwara Eliya", "Polonnaruwa",
        "Puttalam", "Ratnapura", "Trincomalee", "Vavuniya"
    ];
}
