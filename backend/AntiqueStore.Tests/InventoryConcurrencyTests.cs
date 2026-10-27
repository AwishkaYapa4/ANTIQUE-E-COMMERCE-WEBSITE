using AntiqueStore.Application.Common;
using AntiqueStore.Application.Inventory;
using AntiqueStore.Application.Orders;
using AntiqueStore.Domain.Enums;
using AntiqueStore.Tests.Support;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace AntiqueStore.Tests;

/// <summary>The core guarantee: a one-of-a-kind item can never be sold twice, even under concurrent checkouts.</summary>
public sealed class InventoryConcurrencyTests
{
    [Fact]
    public async Task Parallel_stock_holds_on_unique_item_succeed_exactly_once()
    {
        await using var host = await TestHost.CreateAsync();
        var productId = await host.CreateProductAsync("Teak Chair", 50000m, quantity: 1);

        var attempts = Enumerable.Range(0, 20).Select(_ =>
            host.RunAsync(null, sp => sp.GetRequiredService<InventoryService>().TryHoldStockAsync(productId, 1, default)));
        var results = await Task.WhenAll(attempts);

        Assert.Equal(1, results.Count(r => r));
        var product = await host.GetProductAsync(productId);
        Assert.Equal(0, product.QuantityAvailable);
        Assert.Equal(1, product.QuantityReserved);
    }

    [Fact]
    public async Task Concurrent_checkout_reservations_for_the_same_unique_item_only_one_customer_wins()
    {
        await using var host = await TestHost.CreateAsync();
        var productId = await host.CreateProductAsync("Victorian Clock", 68000m, quantity: 1);
        var users = new List<int>();
        for (var i = 0; i < 8; i++)
        {
            var id = await host.CreateUserAsync($"shopper{i}");
            await host.AddToCartAsync(id, productId);
            users.Add(id);
        }

        var outcomes = await Task.WhenAll(users.Select(async u =>
        {
            try { await host.ReserveAsync(u); return "reserved"; }
            catch (BusinessRuleException e) { return e.Code; }
        }));

        Assert.Equal(1, outcomes.Count(o => o == "reserved"));
        Assert.Equal(7, outcomes.Count(o => o == "items_unavailable"));
        var product = await host.GetProductAsync(productId);
        Assert.Equal(0, product.QuantityAvailable);
        Assert.Equal(1, product.QuantityReserved);
        Assert.Equal(1, await host.DbAsync(db => db.InventoryReservations.CountAsync(r => r.Status == ReservationStatus.Active)));
    }

    [Fact]
    public async Task Two_customers_placing_orders_simultaneously_for_the_same_item_create_exactly_one_order()
    {
        await using var host = await TestHost.CreateAsync();
        await host.AddDeliveryZoneAsync();
        var productId = await host.CreateProductAsync("Brass Lantern", 32500m, quantity: 1);
        var alice = await host.CreateUserAsync("alice");
        var bob = await host.CreateUserAsync("bob");
        await host.AddToCartAsync(alice, productId);
        await host.AddToCartAsync(bob, productId);

        var outcomes = await Task.WhenAll(new[] { alice, bob }.Select(async u =>
        {
            try { await host.PlaceOrderAsync(u, Builders.Order()); return "ordered"; }
            catch (BusinessRuleException e) { return e.Code; }
        }));

        Assert.Single(outcomes, o => o == "ordered");
        Assert.Single(outcomes, o => o is "items_unavailable" or "checkout_invalid");
        Assert.Equal(1, await host.DbAsync(db => db.Orders.CountAsync()));
        var product = await host.GetProductAsync(productId);
        Assert.Equal(0, product.QuantityAvailable);
        Assert.Equal(0, product.QuantityReserved);
        Assert.Equal(1, product.QuantitySold);
        Assert.Equal(InventoryState.Sold, product.InventoryState);
    }

    [Fact]
    public async Task Multi_unit_item_never_oversells_under_parallel_orders()
    {
        await using var host = await TestHost.CreateAsync();
        await host.AddDeliveryZoneAsync();
        var productId = await host.CreateProductAsync("Cut-Glass Tumbler", 4000m, quantity: 3);
        var users = new List<int>();
        for (var i = 0; i < 6; i++)
        {
            var id = await host.CreateUserAsync($"buyer{i}");
            await host.AddToCartAsync(id, productId);
            users.Add(id);
        }

        var outcomes = await Task.WhenAll(users.Select(async u =>
        {
            try { await host.PlaceOrderAsync(u, Builders.Order()); return true; }
            catch (BusinessRuleException) { return false; }
        }));

        Assert.Equal(3, outcomes.Count(o => o));
        var product = await host.GetProductAsync(productId);
        Assert.Equal(0, product.QuantityAvailable);
        Assert.Equal(3, product.QuantitySold);
    }

    [Fact]
    public async Task Customer_holding_a_reservation_can_complete_the_order_while_others_are_blocked()
    {
        await using var host = await TestHost.CreateAsync();
        await host.AddDeliveryZoneAsync();
        var productId = await host.CreateProductAsync("Gilt Mirror", 54000m);
        var holder = await host.CreateUserAsync("holder");
        var other = await host.CreateUserAsync("other");
        await host.AddToCartAsync(holder, productId);
        await host.AddToCartAsync(other, productId);

        await host.ReserveAsync(holder);
        var blocked = await Assert.ThrowsAsync<BusinessRuleException>(() => host.PlaceOrderAsync(other, Builders.Order()));
        Assert.Equal("checkout_invalid", blocked.Code);

        var result = await host.PlaceOrderAsync(holder, Builders.Order());
        Assert.False(result.WasDuplicate);
        var reservation = await host.DbAsync(db => db.InventoryReservations.SingleAsync());
        Assert.Equal(ReservationStatus.Committed, reservation.Status);
        Assert.Equal(result.Order.Id, reservation.OrderId);
    }
}
