using AntiqueStore.Application.Carts;
using AntiqueStore.Application.Orders;
using AntiqueStore.Domain.Enums;
using Microsoft.Extensions.DependencyInjection;

namespace AntiqueStore.Tests.Support;

public static class Builders
{
    public static PlaceOrderRequest Order(string? key = null, PaymentMethod payment = PaymentMethod.CashOnDelivery,
        FulfillmentMethod fulfillment = FulfillmentMethod.Delivery, string district = "Colombo", string? discount = null,
        decimal? expectedTotal = null) =>
        new(key ?? Guid.NewGuid().ToString("N"), null, new ContactInfo("Test Customer", "customer@example.test", "+94 77 123 4567"),
            fulfillment, fulfillment == FulfillmentMethod.Delivery ? new DeliveryAddress("12 Galle Road", null, "Colombo 03", district, "00300") : null,
            payment, discount, null, false, expectedTotal);

    public static Task AddToCartAsync(this TestHost host, int userId, int productId, int quantity = 1) =>
        host.RunAsync(userId, sp => sp.GetRequiredService<CartService>().AddAsync(userId, new AddCartItemRequest(productId, quantity), default));

    public static Task<PlaceOrderResult> PlaceOrderAsync(this TestHost host, int userId, PlaceOrderRequest request) =>
        host.RunAsync(userId, sp => sp.GetRequiredService<CheckoutService>().PlaceOrderAsync(userId, request, default));

    public static Task<CheckoutReservationDto> ReserveAsync(this TestHost host, int userId) =>
        host.RunAsync(userId, sp => sp.GetRequiredService<CheckoutService>().ReserveAsync(userId, default));
}
