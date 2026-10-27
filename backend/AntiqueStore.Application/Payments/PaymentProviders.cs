using AntiqueStore.Domain.Entities;
using AntiqueStore.Domain.Enums;

namespace AntiqueStore.Application.Payments;

public sealed record PaymentAvailabilityContext(
    CommerceSettings Settings, FulfillmentMethod Fulfillment, bool ZoneAllowsCashOnDelivery, decimal OrderTotal);

public sealed record PaymentMethodDto(PaymentMethod Method, string Name, string Description, bool IsAvailable, string? UnavailableReason);

public sealed record BankDetailsDto(string? BankName, string? AccountName, string? AccountNumber, string? Branch);

/// <summary>What the customer must do next after placing the order.</summary>
public sealed record PaymentInstructionsDto(
    PaymentMethod Method, string Summary, BankDetailsDto? BankDetails, string? Instructions, DateTime? PaymentDueAt,
    bool RequiresProof, string? RedirectUrl);

/// <summary>
/// Abstraction for payment methods. Offline methods (COD, bank transfer) are implemented now; an online
/// gateway would implement this interface plus a webhook endpoint that confirms payment server-to-server
/// (never from a browser redirect) and records it idempotently by provider transaction id.
/// </summary>
public interface IPaymentMethodProvider
{
    PaymentMethod Method { get; }
    string Name { get; }
    string Description { get; }
    string? GetUnavailableReason(PaymentAvailabilityContext context);
    PaymentInstructionsDto GetInstructions(Order order, CommerceSettings settings);
}

public sealed class CashOnDeliveryProvider : IPaymentMethodProvider
{
    public PaymentMethod Method => PaymentMethod.CashOnDelivery;
    public string Name => "Cash on Delivery";
    public string Description => "Pay in cash when your item is delivered or collected.";

    public string? GetUnavailableReason(PaymentAvailabilityContext c)
    {
        if (!c.Settings.CashOnDeliveryEnabled) return "Cash on delivery is not currently offered.";
        if (c.Fulfillment == FulfillmentMethod.Delivery && !c.ZoneAllowsCashOnDelivery)
            return "Cash on delivery is not available for this delivery area.";
        if (c.Settings.CashOnDeliveryMaxOrderValue is { } max && c.OrderTotal > max)
            return $"Cash on delivery is available for orders up to LKR {max:N2}.";
        return null;
    }

    public PaymentInstructionsDto GetInstructions(Order order, CommerceSettings settings) =>
        new(Method, order.FulfillmentMethod == FulfillmentMethod.LocalPickup
                ? $"Please pay {order.Currency} {order.Total:N2} in cash when you collect your order."
                : $"Please have {order.Currency} {order.Total:N2} ready in cash on delivery.",
            null, null, null, false, null);
}

public sealed class BankTransferProvider : IPaymentMethodProvider
{
    public PaymentMethod Method => PaymentMethod.BankTransfer;
    public string Name => "Bank Transfer";
    public string Description => "Transfer the total to our bank account. We confirm your order once payment is verified.";

    public string? GetUnavailableReason(PaymentAvailabilityContext c)
    {
        if (!c.Settings.BankTransferEnabled) return "Bank transfer is not currently offered.";
        if (string.IsNullOrWhiteSpace(c.Settings.BankAccountNumber)) return "Bank transfer details have not been configured yet.";
        return null;
    }

    public PaymentInstructionsDto GetInstructions(Order order, CommerceSettings settings) =>
        new(Method,
            $"Transfer {order.Currency} {order.Total:N2} and use your order number {order.OrderNumber} as the payment reference.",
            new BankDetailsDto(settings.BankName, settings.BankAccountName, settings.BankAccountNumber, settings.BankBranch),
            settings.BankTransferInstructions, order.PaymentDueAt, true, null);
}

public sealed class PaymentMethodRegistry(IEnumerable<IPaymentMethodProvider> providers)
{
    private readonly Dictionary<PaymentMethod, IPaymentMethodProvider> _providers = providers.ToDictionary(p => p.Method);

    public IPaymentMethodProvider Get(PaymentMethod method) =>
        _providers.TryGetValue(method, out var p) ? p : throw new InvalidOperationException($"No provider for {method}.");

    public IReadOnlyList<PaymentMethodDto> Describe(PaymentAvailabilityContext context) =>
        _providers.Values.OrderBy(p => p.Method).Select(p =>
        {
            var reason = p.GetUnavailableReason(context);
            return new PaymentMethodDto(p.Method, p.Name, p.Description, reason is null, reason);
        }).ToList();
}
