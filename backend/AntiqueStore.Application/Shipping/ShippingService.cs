using AntiqueStore.Application.Common;
using AntiqueStore.Domain.Entities;
using AntiqueStore.Domain.Enums;
using AntiqueStore.Domain.Rules;
using Microsoft.EntityFrameworkCore;

namespace AntiqueStore.Application.Shipping;

public sealed record ShippingQuote(
    bool IsAvailable, decimal Fee, string? RateName, string? EstimatedDelivery, bool CashOnDeliveryAvailable,
    string? UnavailableReason, int? ZoneId);

public sealed record ShippingOptionsDto(
    IReadOnlyList<string> DeliverableDistricts, bool LocalPickupEnabled, string? LocalPickupAddress,
    string? LocalPickupInstructions, string? DeliveryInstructions, decimal? FreeShippingThreshold);

/// <summary>Server-side shipping calculation. Fees come only from admin-configured zones and rates.</summary>
public sealed class ShippingService(IAppDbContext db)
{
    public async Task<ShippingQuote> QuoteAsync(FulfillmentMethod method, string? district, decimal merchandiseTotal,
        CommerceSettings settings, CancellationToken ct)
    {
        if (method == FulfillmentMethod.LocalPickup)
        {
            return settings.LocalPickupEnabled
                ? new ShippingQuote(true, 0m, "Local pickup", settings.LocalPickupInstructions, settings.CashOnDeliveryEnabled, null, null)
                : new ShippingQuote(false, 0m, null, null, false, "Local pickup is not currently offered.", null);
        }

        if (string.IsNullOrWhiteSpace(district))
            return new ShippingQuote(false, 0m, null, null, false, "Select a delivery district.", null);

        var zone = await db.ShippingZones.AsNoTracking()
            .Include(z => z.Rates.Where(r => r.IsActive))
            .Where(z => z.IsActive && z.Districts.Any(d => d.District == district))
            .FirstOrDefaultAsync(ct);
        if (zone is null)
            return new ShippingQuote(false, 0m, null, null, false, $"We don't currently deliver to {district}.", null);

        var rate = zone.Rates
            .Where(r => (r.MinSubtotal is null || merchandiseTotal >= r.MinSubtotal) &&
                        (r.MaxSubtotal is null || merchandiseTotal <= r.MaxSubtotal))
            .OrderBy(r => r.Fee)
            .FirstOrDefault();
        if (rate is null)
            return new ShippingQuote(false, 0m, null, null, false, $"No delivery rate is configured for {district}.", zone.Id);

        var free = settings.FreeShippingThreshold is { } threshold && merchandiseTotal >= threshold;
        return new ShippingQuote(true, free ? 0m : rate.Fee, free ? "Free delivery" : rate.Name, zone.EstimatedDelivery,
            settings.CashOnDeliveryEnabled && zone.CashOnDeliveryAvailable, null, zone.Id);
    }

    public async Task<ShippingOptionsDto> GetOptionsAsync(CommerceSettings settings, CancellationToken ct)
    {
        var districts = await db.ShippingZoneDistricts.AsNoTracking()
            .Where(d => d.ShippingZone!.IsActive && d.ShippingZone.Rates.Any(r => r.IsActive))
            .Select(d => d.District).ToListAsync(ct);
        return new ShippingOptionsDto(
            SriLanka.Districts.Where(districts.Contains).ToList(),
            settings.LocalPickupEnabled, settings.LocalPickupAddress, settings.LocalPickupInstructions,
            settings.DeliveryInstructions, settings.FreeShippingThreshold);
    }
}
