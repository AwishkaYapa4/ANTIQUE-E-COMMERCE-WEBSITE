using AntiqueStore.Application.Promotions;
using AntiqueStore.Application.Settings;
using AntiqueStore.Application.Shipping;
using AntiqueStore.Domain.Entities;
using AntiqueStore.Domain.Enums;
using AntiqueStore.Tests.Support;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace AntiqueStore.Tests;

public sealed class DiscountAndShippingTests
{
    private static async Task<int> AddCodeAsync(TestHost host, Action<DiscountCode> configure) =>
        await host.DbAsync(async db =>
        {
            var code = new DiscountCode { Code = "CODE" + Guid.NewGuid().ToString("N")[..6].ToUpperInvariant(), Type = DiscountType.Percentage, Value = 10, IsActive = true };
            configure(code);
            db.DiscountCodes.Add(code);
            await db.SaveChangesAsync();
            return code.Id;
        });

    [Theory]
    [InlineData(DiscountType.Percentage, 10, null, 50000, 5000)]
    [InlineData(DiscountType.Percentage, 50, 10000, 50000, 10000)]
    [InlineData(DiscountType.FixedAmount, 2500, null, 50000, 2500)]
    [InlineData(DiscountType.FixedAmount, 99999, null, 5000, 5000)]
    public void Discount_amounts_are_capped_and_never_exceed_the_subtotal(DiscountType type, decimal value, int? cap, decimal subtotal, decimal expected)
    {
        var code = new DiscountCode { Type = type, Value = value, MaxDiscountAmount = cap };
        Assert.Equal(expected, DiscountService.Calculate(code, subtotal));
    }

    [Fact]
    public async Task Discount_validation_enforces_dates_minimum_and_activity()
    {
        await using var host = await TestHost.CreateAsync();
        var now = host.Clock.GetUtcNow().UtcDateTime;
        await AddCodeAsync(host, c => { c.Code = "EXPIRED"; c.EndsAt = now.AddDays(-1); });
        await AddCodeAsync(host, c => { c.Code = "FUTURE"; c.StartsAt = now.AddDays(1); });
        await AddCodeAsync(host, c => { c.Code = "MIN"; c.MinOrderSubtotal = 100000; });
        await AddCodeAsync(host, c => { c.Code = "OFF"; c.IsActive = false; });
        await AddCodeAsync(host, c => { c.Code = "GOOD"; });

        async Task<DiscountEvaluation> Eval(string code) =>
            await host.RunAsync(1, sp => sp.GetRequiredService<DiscountService>().EvaluateAsync(code, 50000m, 1, default));

        Assert.False((await Eval("expired")).IsValid);
        Assert.False((await Eval("FUTURE")).IsValid);
        Assert.False((await Eval("MIN")).IsValid);
        Assert.False((await Eval("OFF")).IsValid);
        Assert.False((await Eval("NOPE")).IsValid);
        var good = await Eval(" good ");
        Assert.True(good.IsValid);
        Assert.Equal(5000m, good.Amount);
    }

    [Fact]
    public async Task Usage_limit_cannot_be_exceeded_by_parallel_claims()
    {
        await using var host = await TestHost.CreateAsync();
        var id = await AddCodeAsync(host, c => c.UsageLimit = 2);

        var claims = await Task.WhenAll(Enumerable.Range(0, 10).Select(_ =>
            host.RunAsync(null, sp => sp.GetRequiredService<DiscountService>().TryClaimUsageAsync(id, default))));

        Assert.Equal(2, claims.Count(c => c));
        Assert.Equal(2, await host.DbAsync(db => db.DiscountCodes.Where(d => d.Id == id).Select(d => d.UsedCount).SingleAsync()));
    }

    [Fact]
    public async Task Order_with_discount_records_usage_and_cancellation_releases_it()
    {
        await using var host = await TestHost.CreateAsync();
        await host.AddDeliveryZoneAsync("Colombo", 1000m);
        await AddCodeAsync(host, c => { c.Code = "TENOFF"; c.UsageLimit = 1; });
        var productId = await host.CreateProductAsync("Vase", 20000m);
        var user = await host.CreateUserAsync("saver");
        await host.AddToCartAsync(user, productId);

        var order = (await host.PlaceOrderAsync(user, Builders.Order(discount: "tenoff"))).Order;
        Assert.Equal(2000m, order.DiscountTotal);
        Assert.Equal(20000m - 2000m + 1000m, order.Total);
        Assert.Equal(1, await host.DbAsync(db => db.DiscountCodes.Where(d => d.Code == "TENOFF").Select(d => d.UsedCount).SingleAsync()));

        await host.RunAsync(user, sp => sp.GetRequiredService<AntiqueStore.Application.Orders.CustomerOrderService>()
            .CancelAsync(user, order.Id, new AntiqueStore.Application.Orders.CancelOrderRequest("changed mind"), default));
        Assert.Equal(0, await host.DbAsync(db => db.DiscountCodes.Where(d => d.Code == "TENOFF").Select(d => d.UsedCount).SingleAsync()));
        Assert.Equal(0, await host.DbAsync(db => db.DiscountUsages.CountAsync()));
    }

    [Fact]
    public async Task Shipping_uses_zone_rates_free_threshold_and_rejects_unconfigured_districts()
    {
        await using var host = await TestHost.CreateAsync();
        await host.AddDeliveryZoneAsync("Colombo", 1500m);

        async Task<ShippingQuote> Quote(FulfillmentMethod m, string? district, decimal subtotal, Action<CommerceSettings>? s = null) =>
            await host.RunAsync(null, async sp =>
            {
                var settings = await sp.GetRequiredService<CommerceSettingsService>().GetAsync(default);
                s?.Invoke(settings);
                return await sp.GetRequiredService<ShippingService>().QuoteAsync(m, district, subtotal, settings, default);
            });

        var standard = await Quote(FulfillmentMethod.Delivery, "Colombo", 20000m);
        Assert.True(standard.IsAvailable);
        Assert.Equal(1500m, standard.Fee);

        var free = await Quote(FulfillmentMethod.Delivery, "Colombo", 200000m, s => s.FreeShippingThreshold = 100000m);
        Assert.Equal(0m, free.Fee);

        var unknown = await Quote(FulfillmentMethod.Delivery, "Mannar", 20000m);
        Assert.False(unknown.IsAvailable);

        var pickupOff = await Quote(FulfillmentMethod.LocalPickup, null, 20000m, s => s.LocalPickupEnabled = false);
        Assert.False(pickupOff.IsAvailable);
        var pickupOn = await Quote(FulfillmentMethod.LocalPickup, null, 20000m, s => s.LocalPickupEnabled = true);
        Assert.True(pickupOn.IsAvailable);
        Assert.Equal(0m, pickupOn.Fee);
    }
}
