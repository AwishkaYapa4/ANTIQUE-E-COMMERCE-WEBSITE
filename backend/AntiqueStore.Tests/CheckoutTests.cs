using AntiqueStore.Application.Common;
using AntiqueStore.Application.Orders;
using AntiqueStore.Domain.Entities;
using AntiqueStore.Domain.Enums;
using AntiqueStore.Tests.Support;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace AntiqueStore.Tests;

public sealed class CheckoutTests
{
    [Fact]
    public async Task Order_totals_are_calculated_on_the_server_with_price_snapshots()
    {
        await using var host = await TestHost.CreateAsync();
        await host.AddDeliveryZoneAsync("Colombo", 1500m);
        var productId = await host.CreateProductAsync("Planter's Chair", 185000m);
        var user = await host.CreateUserAsync("buyer");
        await host.AddToCartAsync(user, productId);

        var result = await host.PlaceOrderAsync(user, Builders.Order());

        var order = result.Order;
        Assert.Equal(185000m, order.Subtotal);
        Assert.Equal(1500m, order.ShippingTotal);
        Assert.Equal(186500m, order.Total);
        Assert.Equal(OrderStatus.Pending, order.Status);
        Assert.Equal(PaymentStatus.Unpaid, order.PaymentStatus);

        // Later price edits must not change the historical order.
        await host.DbAsync(async db =>
        {
            var p = await db.Products.FirstAsync(x => x.Id == productId);
            p.Price = 1m;
            return await db.SaveChangesAsync();
        });
        var reloaded = await host.RunAsync(user, sp => sp.GetRequiredService<OrderQueries>().GetForCustomerAsync(user, order.Id, default));
        Assert.Equal(185000m, reloaded.Items.Single().UnitPrice);
        Assert.Equal(186500m, reloaded.Total);
    }

    [Fact]
    public async Task Placing_the_same_order_twice_with_one_idempotency_key_creates_one_order()
    {
        await using var host = await TestHost.CreateAsync();
        await host.AddDeliveryZoneAsync();
        var productId = await host.CreateProductAsync("Clock", 68000m);
        var user = await host.CreateUserAsync("retry");
        await host.AddToCartAsync(user, productId);
        var request = Builders.Order(key: "retry-key-0000000001");

        var first = await host.PlaceOrderAsync(user, request);
        var second = await host.PlaceOrderAsync(user, request);

        Assert.False(first.WasDuplicate);
        Assert.True(second.WasDuplicate);
        Assert.Equal(first.Order.Id, second.Order.Id);
        Assert.Equal(1, await host.DbAsync(db => db.Orders.CountAsync()));
        Assert.Equal(1, (await host.GetProductAsync(productId)).QuantitySold);
    }

    [Fact]
    public async Task Concurrent_duplicate_submissions_still_produce_a_single_order()
    {
        await using var host = await TestHost.CreateAsync();
        await host.AddDeliveryZoneAsync();
        var productId = await host.CreateProductAsync("Painting", 95000m);
        var user = await host.CreateUserAsync("doubleclick");
        await host.AddToCartAsync(user, productId);
        var request = Builders.Order(key: "double-click-key-0001");

        var results = await Task.WhenAll(Enumerable.Range(0, 4).Select(async _ =>
        {
            try { return (await host.PlaceOrderAsync(user, request)).Order.Id; }
            catch (BusinessRuleException) { return -1; } // a racer may find the cart already cleared
        }));

        Assert.Equal(1, await host.DbAsync(db => db.Orders.CountAsync()));
        var orderId = await host.DbAsync(db => db.Orders.Select(o => o.Id).SingleAsync());
        Assert.All(results.Where(r => r != -1), r => Assert.Equal(orderId, r));
    }

    [Fact]
    public async Task Order_is_rejected_when_the_total_changed_since_the_customer_reviewed_it()
    {
        await using var host = await TestHost.CreateAsync();
        await host.AddDeliveryZoneAsync("Colombo", 1500m);
        var productId = await host.CreateProductAsync("Lantern", 30000m);
        var user = await host.CreateUserAsync("careful");
        await host.AddToCartAsync(user, productId);

        var error = await Assert.ThrowsAsync<BusinessRuleException>(() =>
            host.PlaceOrderAsync(user, Builders.Order(expectedTotal: 25000m)));

        Assert.Equal("total_changed", error.Code);
        Assert.Equal(1, (await host.GetProductAsync(productId)).QuantityAvailable);
        Assert.Equal(0, await host.DbAsync(db => db.Orders.CountAsync()));
    }

    [Fact]
    public async Task Pickup_only_items_cannot_be_delivered()
    {
        await using var host = await TestHost.CreateAsync();
        await host.AddDeliveryZoneAsync();
        var productId = await host.CreateProductAsync("Sideboard", 145000m, 1, p => p.IsDeliveryAvailable = false);
        var user = await host.CreateUserAsync("bigitem");
        await host.AddToCartAsync(user, productId);

        var error = await Assert.ThrowsAsync<BusinessRuleException>(() => host.PlaceOrderAsync(user, Builders.Order()));
        Assert.Contains("local pickup only", error.Message);
    }

    [Fact]
    public async Task Cash_on_delivery_is_refused_where_the_zone_does_not_allow_it()
    {
        await using var host = await TestHost.CreateAsync();
        await host.AddDeliveryZoneAsync("Jaffna", 3500m, cod: false);
        var productId = await host.CreateProductAsync("Betel Box", 18500m);
        var user = await host.CreateUserAsync("north");
        await host.AddToCartAsync(user, productId);

        var error = await Assert.ThrowsAsync<BusinessRuleException>(() => host.PlaceOrderAsync(user, Builders.Order(district: "Jaffna")));
        Assert.Equal("payment_unavailable", error.Code);
    }

    [Fact]
    public async Task Bank_transfer_order_requires_admin_verification_before_it_is_paid()
    {
        await using var host = await TestHost.CreateAsync();
        await host.AddDeliveryZoneAsync();
        await host.DbAsync(async db =>
        {
            var s = await db.CommerceSettings.FirstAsync();
            s.BankAccountNumber = "1234567890";
            s.BankName = "Test Bank";
            return await db.SaveChangesAsync();
        });
        var productId = await host.CreateProductAsync("Wall Hanging", 22000m);
        var user = await host.CreateUserAsync("transfer");
        var admin = await host.CreateUserAsync("owner", UserRole.Admin);
        await host.AddToCartAsync(user, productId);

        var order = (await host.PlaceOrderAsync(user, Builders.Order(payment: PaymentMethod.BankTransfer))).Order;
        Assert.NotNull(order.PaymentInstructions?.BankDetails);
        Assert.True(order.CanSubmitPayment);

        var pdf = new MemoryStream("%PDF-1.4 test"u8.ToArray());
        var submitted = await host.RunAsync(user, sp => sp.GetRequiredService<CustomerOrderService>().SubmitBankTransferAsync(
            user, order.Id, new SubmitPaymentReferenceRequest("TRX-998877", null), new UploadedFile(pdf, "receipt.pdf", "application/pdf", pdf.Length), default));
        Assert.Equal(PaymentStatus.PendingVerification, submitted.PaymentStatus);

        var verified = await host.RunAsync(admin, sp => sp.GetRequiredService<AdminOrderService>().UpdatePaymentStatusAsync(
            order.Id, new UpdatePaymentStatusRequest(PaymentStatus.Paid, "Matched bank statement"), default), UserRole.Admin);
        Assert.Equal(PaymentStatus.Paid, verified.Order.PaymentStatus);
        Assert.Contains(verified.Payments, p => p.Status == PaymentRecordStatus.Succeeded && p.Reference == "TRX-998877" && p.Proofs.Count == 1);
    }

    [Fact]
    public async Task Proof_upload_rejects_files_whose_content_does_not_match_the_declared_type()
    {
        await using var host = await TestHost.CreateAsync();
        await host.AddDeliveryZoneAsync();
        await host.DbAsync(async db => { (await db.CommerceSettings.FirstAsync()).BankAccountNumber = "1"; return await db.SaveChangesAsync(); });
        var productId = await host.CreateProductAsync("Mirror", 10000m);
        var user = await host.CreateUserAsync("sneaky");
        await host.AddToCartAsync(user, productId);
        var order = (await host.PlaceOrderAsync(user, Builders.Order(payment: PaymentMethod.BankTransfer))).Order;

        var fake = new MemoryStream("MZ this is an exe"u8.ToArray());
        await Assert.ThrowsAsync<RequestValidationException>(() => host.RunAsync(user, sp =>
            sp.GetRequiredService<CustomerOrderService>().SubmitBankTransferAsync(user, order.Id,
                new SubmitPaymentReferenceRequest("X", null), new UploadedFile(fake, "receipt.png", "image/png", fake.Length), default)));
    }

    [Fact]
    public async Task Customers_cannot_read_other_customers_orders()
    {
        await using var host = await TestHost.CreateAsync();
        await host.AddDeliveryZoneAsync();
        var productId = await host.CreateProductAsync("Tumblers", 16000m);
        var owner = await host.CreateUserAsync("owner1");
        var intruder = await host.CreateUserAsync("intruder");
        await host.AddToCartAsync(owner, productId);
        var order = (await host.PlaceOrderAsync(owner, Builders.Order())).Order;

        await Assert.ThrowsAsync<NotFoundException>(() =>
            host.RunAsync(intruder, sp => sp.GetRequiredService<OrderQueries>().GetForCustomerAsync(intruder, order.Id, default)));
    }
}
