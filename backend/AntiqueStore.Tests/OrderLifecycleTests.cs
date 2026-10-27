using AntiqueStore.Application.Common;
using AntiqueStore.Application.Orders;
using AntiqueStore.Domain.Enums;
using AntiqueStore.Domain.Rules;
using AntiqueStore.Tests.Support;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace AntiqueStore.Tests;

public sealed class OrderLifecycleTests
{
    [Theory]
    [InlineData(OrderStatus.Pending, OrderStatus.Confirmed, true)]
    [InlineData(OrderStatus.Confirmed, OrderStatus.Shipped, true)]
    [InlineData(OrderStatus.Shipped, OrderStatus.Delivered, true)]
    [InlineData(OrderStatus.Delivered, OrderStatus.Cancelled, false)]
    [InlineData(OrderStatus.Shipped, OrderStatus.Cancelled, false)]
    [InlineData(OrderStatus.Cancelled, OrderStatus.Pending, false)]
    [InlineData(OrderStatus.Pending, OrderStatus.Delivered, false)]
    public void Order_status_transitions_follow_the_state_machine(OrderStatus from, OrderStatus to, bool allowed) =>
        Assert.Equal(allowed, OrderRules.CanTransition(from, to));

    [Theory]
    [InlineData(PaymentStatus.Unpaid, PaymentStatus.Paid, true)]
    [InlineData(PaymentStatus.PendingVerification, PaymentStatus.Paid, true)]
    [InlineData(PaymentStatus.Paid, PaymentStatus.Refunded, true)]
    [InlineData(PaymentStatus.Paid, PaymentStatus.Unpaid, false)]
    [InlineData(PaymentStatus.Refunded, PaymentStatus.Paid, false)]
    [InlineData(PaymentStatus.Unpaid, PaymentStatus.Refunded, false)]
    public void Payment_status_transitions_are_independent_and_validated(PaymentStatus from, PaymentStatus to, bool allowed) =>
        Assert.Equal(allowed, OrderRules.CanTransition(from, to));

    private static async Task<(TestHost Host, int Admin, int Customer, int ProductId, OrderDto Order)> PlacedOrderAsync()
    {
        var host = await TestHost.CreateAsync();
        await host.AddDeliveryZoneAsync();
        var productId = await host.CreateProductAsync("Teak Chair", 50000m);
        var customer = await host.CreateUserAsync("c");
        var admin = await host.CreateUserAsync("a", UserRole.Admin);
        await host.AddToCartAsync(customer, productId);
        var order = (await host.PlaceOrderAsync(customer, Builders.Order())).Order;
        return (host, admin, customer, productId, order);
    }

    private static Task<AdminOrderDto> SetStatus(TestHost host, int admin, int orderId, OrderStatus status, bool restock = true) =>
        host.RunAsync(admin, sp => sp.GetRequiredService<AdminOrderService>()
            .UpdateStatusAsync(orderId, new UpdateOrderStatusRequest(status, "test", restock), default), UserRole.Admin);

    [Fact]
    public async Task Cancelling_before_shipping_returns_the_item_to_sale_once()
    {
        var (host, admin, _, productId, order) = await PlacedOrderAsync();
        await using var _h = host;
        Assert.Equal(InventoryState.Sold, (await host.GetProductAsync(productId)).InventoryState);

        await SetStatus(host, admin, order.Id, OrderStatus.Confirmed);
        var cancelled = await SetStatus(host, admin, order.Id, OrderStatus.Cancelled);

        Assert.Equal(OrderStatus.Cancelled, cancelled.Order.Status);
        Assert.True(cancelled.StockRestored);
        var product = await host.GetProductAsync(productId);
        Assert.Equal(1, product.QuantityAvailable);
        Assert.Equal(0, product.QuantitySold);
        await Assert.ThrowsAsync<BusinessRuleException>(() => SetStatus(host, admin, order.Id, OrderStatus.Cancelled));
        Assert.Equal(1, (await host.GetProductAsync(productId)).QuantityAvailable);
    }

    [Fact]
    public async Task Shipped_orders_cannot_be_cancelled_and_stock_stays_sold()
    {
        var (host, admin, _, productId, order) = await PlacedOrderAsync();
        await using var _h = host;
        await SetStatus(host, admin, order.Id, OrderStatus.Confirmed);
        await SetStatus(host, admin, order.Id, OrderStatus.Shipped);

        await Assert.ThrowsAsync<BusinessRuleException>(() => SetStatus(host, admin, order.Id, OrderStatus.Cancelled));
        Assert.Equal(InventoryState.Sold, (await host.GetProductAsync(productId)).InventoryState);
    }

    [Fact]
    public async Task Admin_may_cancel_without_restocking_when_the_item_was_damaged()
    {
        var (host, admin, _, productId, order) = await PlacedOrderAsync();
        await using var _h = host;
        await SetStatus(host, admin, order.Id, OrderStatus.Cancelled, restock: false);
        var product = await host.GetProductAsync(productId);
        Assert.Equal(0, product.QuantityAvailable);
        Assert.Equal(1, product.QuantitySold);
    }

    [Fact]
    public async Task Customers_can_cancel_only_pending_unpaid_orders()
    {
        var (host, admin, customer, _, order) = await PlacedOrderAsync();
        await using var _h = host;
        await SetStatus(host, admin, order.Id, OrderStatus.Confirmed);

        var error = await Assert.ThrowsAsync<BusinessRuleException>(() => host.RunAsync(customer, sp =>
            sp.GetRequiredService<CustomerOrderService>().CancelAsync(customer, order.Id, new CancelOrderRequest(null), default)));
        Assert.Equal("cannot_cancel", error.Code);
    }

    [Fact]
    public async Task Refunds_are_recorded_partially_then_fully()
    {
        var (host, admin, _, _, order) = await PlacedOrderAsync();
        await using var _h = host;
        var svc = (IServiceProvider sp) => sp.GetRequiredService<AdminOrderService>();

        await Assert.ThrowsAsync<BusinessRuleException>(() => host.RunAsync(admin, sp =>
            svc(sp).RecordRefundAsync(order.Id, new RecordRefundRequest(100m, null, null), default), UserRole.Admin));

        await host.RunAsync(admin, sp => svc(sp).UpdatePaymentStatusAsync(order.Id, new UpdatePaymentStatusRequest(PaymentStatus.Paid, "cash collected"), default), UserRole.Admin);
        var partial = await host.RunAsync(admin, sp => svc(sp).RecordRefundAsync(order.Id, new RecordRefundRequest(1000m, "goodwill", null), default), UserRole.Admin);
        Assert.Equal(PaymentStatus.Paid, partial.Order.PaymentStatus);
        Assert.Equal(1000m, partial.Order.RefundedTotal);

        await Assert.ThrowsAsync<RequestValidationException>(() => host.RunAsync(admin, sp =>
            svc(sp).RecordRefundAsync(order.Id, new RecordRefundRequest(order.Total, null, null), default), UserRole.Admin));

        var full = await host.RunAsync(admin, sp => svc(sp).RecordRefundAsync(order.Id, new RecordRefundRequest(order.Total - 1000m, null, null), default), UserRole.Admin);
        Assert.Equal(PaymentStatus.Refunded, full.Order.PaymentStatus);
        Assert.Equal(3, await host.DbAsync(db => db.Payments.CountAsync(p => p.OrderId == order.Id)));
    }

    [Fact]
    public async Task Unpaid_bank_transfers_are_auto_cancelled_after_the_payment_window_and_restocked()
    {
        await using var host = await TestHost.CreateAsync();
        await host.AddDeliveryZoneAsync();
        await host.DbAsync(async db =>
        {
            var s = await db.CommerceSettings.FirstAsync();
            s.BankAccountNumber = "1";
            s.BankTransferPaymentWindowHours = 24;
            return await db.SaveChangesAsync();
        });
        var productId = await host.CreateProductAsync("Clock", 30000m);
        var user = await host.CreateUserAsync("forgetful");
        await host.AddToCartAsync(user, productId);
        var order = (await host.PlaceOrderAsync(user, Builders.Order(payment: PaymentMethod.BankTransfer))).Order;

        var early = await host.RunAsync(null, sp => sp.GetRequiredService<OrderMaintenanceService>().CancelOverdueBankTransfersAsync(10, default));
        Assert.Equal(0, early);

        host.Clock.Advance(TimeSpan.FromHours(25));
        var cancelled = await host.RunAsync(null, sp => sp.GetRequiredService<OrderMaintenanceService>().CancelOverdueBankTransfersAsync(10, default));
        Assert.Equal(1, cancelled);
        Assert.Equal(OrderStatus.Cancelled, await host.DbAsync(db => db.Orders.Where(o => o.Id == order.Id).Select(o => o.Status).SingleAsync()));
        Assert.Equal(1, (await host.GetProductAsync(productId)).QuantityAvailable);
    }
}
