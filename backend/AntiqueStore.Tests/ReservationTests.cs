using AntiqueStore.Application.Carts;
using AntiqueStore.Application.Inventory;
using AntiqueStore.Domain.Enums;
using AntiqueStore.Tests.Support;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace AntiqueStore.Tests;

public sealed class ReservationTests
{
    [Fact]
    public async Task Expired_reservations_are_released_back_to_available_stock()
    {
        await using var host = await TestHost.CreateAsync();
        var productId = await host.CreateProductAsync("Writing Box", 41000m);
        var user = await host.CreateUserAsync("u1");
        await host.AddToCartAsync(user, productId);
        var reservation = await host.ReserveAsync(user);
        Assert.True(reservation.ExpiresAt > host.Clock.GetUtcNow().UtcDateTime);

        host.Clock.Advance(TimeSpan.FromMinutes(16));
        var expired = await host.RunAsync(null, sp => sp.GetRequiredService<InventoryService>().ExpireReservationsAsync(100, default));

        Assert.Equal(1, expired);
        var product = await host.GetProductAsync(productId);
        Assert.Equal(1, product.QuantityAvailable);
        Assert.Equal(0, product.QuantityReserved);
        var row = await host.DbAsync(db => db.InventoryReservations.SingleAsync());
        Assert.Equal(ReservationStatus.Expired, row.Status);
        Assert.Contains(await host.DbAsync(db => db.InventoryMovements.Select(m => m.Type).ToListAsync()), t => t == InventoryMovementType.ReservationExpired);
    }

    [Fact]
    public async Task Releasing_a_reservation_twice_only_restores_stock_once()
    {
        await using var host = await TestHost.CreateAsync();
        var productId = await host.CreateProductAsync("Vase", 24000m);
        var user = await host.CreateUserAsync("u2");
        await host.AddToCartAsync(user, productId);
        await host.ReserveAsync(user);
        var id = await host.DbAsync(db => db.InventoryReservations.Select(r => r.Id).SingleAsync());

        var results = await Task.WhenAll(Enumerable.Range(0, 5).Select(_ => host.RunAsync(null, sp =>
            sp.GetRequiredService<InventoryService>().ReleaseReservationAsync(id, ReservationStatus.Released, "test", default))));

        Assert.Equal(1, results.Count(r => r));
        var product = await host.GetProductAsync(productId);
        Assert.Equal(1, product.QuantityAvailable);
        Assert.Equal(0, product.QuantityReserved);
    }

    [Fact]
    public async Task Restarting_checkout_replaces_previous_holds_instead_of_stacking_them()
    {
        await using var host = await TestHost.CreateAsync();
        var productId = await host.CreateProductAsync("Lamp", 28500m);
        var user = await host.CreateUserAsync("u3");
        await host.AddToCartAsync(user, productId);

        await host.ReserveAsync(user);
        await host.ReserveAsync(user);

        Assert.Equal(1, await host.DbAsync(db => db.InventoryReservations.CountAsync(r => r.Status == ReservationStatus.Active)));
        var product = await host.GetProductAsync(productId);
        Assert.Equal(0, product.QuantityAvailable);
        Assert.Equal(1, product.QuantityReserved);
    }

    [Fact]
    public async Task Cart_shows_item_reserved_by_another_customer_as_not_purchasable()
    {
        await using var host = await TestHost.CreateAsync();
        var productId = await host.CreateProductAsync("Brooch", 15500m);
        var holder = await host.CreateUserAsync("holder");
        var browser = await host.CreateUserAsync("browser");
        await host.AddToCartAsync(holder, productId);
        await host.AddToCartAsync(browser, productId);
        await host.ReserveAsync(holder);

        var holderCart = await host.RunAsync(holder, sp => sp.GetRequiredService<CartService>().GetAsync(holder, default));
        var browserCart = await host.RunAsync(browser, sp => sp.GetRequiredService<CartService>().GetAsync(browser, default));

        Assert.True(holderCart.Items.Single().IsPurchasable);
        Assert.False(browserCart.Items.Single().IsPurchasable);
        Assert.Equal(InventoryState.Reserved, browserCart.Items.Single().InventoryState);
        Assert.Equal(0m, browserCart.Subtotal);
    }

    [Fact]
    public async Task Sold_item_cannot_be_added_to_a_cart()
    {
        await using var host = await TestHost.CreateAsync();
        var productId = await host.CreateProductAsync("Doorstop", 9500m, quantity: 1, p => { p.QuantityAvailable = 0; p.QuantitySold = 1; });
        var user = await host.CreateUserAsync("late");

        var error = await Assert.ThrowsAsync<AntiqueStore.Application.Common.BusinessRuleException>(() => host.AddToCartAsync(user, productId));
        Assert.Equal("sold", error.Code);
    }
}
