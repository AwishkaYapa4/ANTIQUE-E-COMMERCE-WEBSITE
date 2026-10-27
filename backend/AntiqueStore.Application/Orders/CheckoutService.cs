using System.Security.Cryptography;
using AntiqueStore.Application.Carts;
using AntiqueStore.Application.Catalog;
using AntiqueStore.Application.Common;
using AntiqueStore.Application.Inventory;
using AntiqueStore.Application.Payments;
using AntiqueStore.Application.Promotions;
using AntiqueStore.Application.Settings;
using AntiqueStore.Application.Shipping;
using AntiqueStore.Domain.Entities;
using AntiqueStore.Domain.Enums;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace AntiqueStore.Application.Orders;

/// <summary>
/// Checkout pipeline: quote â†’ reserve â†’ place order. All totals are recalculated on the server from
/// current database prices, shipping rates and discount rules on every step.
/// </summary>
public sealed class CheckoutService(
    IAppDbContext db,
    CartService carts,
    InventoryService inventory,
    ShippingService shipping,
    DiscountService discounts,
    PaymentMethodRegistry paymentMethods,
    CommerceSettingsService settingsService,
    OrderQueries orderQueries,
    TimeProvider clock,
    ILogger<CheckoutService> logger)
{
    private DateTime Now => clock.GetUtcNow().UtcDateTime;

    private sealed record Totals(
        CartDto Cart, decimal Subtotal, DiscountEvaluation Discount, ShippingQuote Shipping, decimal Total,
        IReadOnlyList<PaymentMethodDto> PaymentMethods, List<string> Issues);

    private async Task<Totals> CalculateAsync(int userId, FulfillmentMethod fulfillment, string? district, string? discountCode,
        CommerceSettings settings, CancellationToken ct)
    {
        var cart = await carts.GetAsync(userId, ct);
        var issues = new List<string>();
        if (cart.Items.Count == 0) issues.Add("Your cart is empty.");
        issues.AddRange(cart.Items.Where(l => !l.IsPurchasable).Select(l => $"{l.Name}: {l.Issue}"));
        issues.AddRange(cart.Items.Where(l => l.IsPurchasable && l.Quantity > l.MaxQuantity)
            .Select(l => $"{l.Name}: only {l.MaxQuantity} available."));

        var subtotal = cart.Subtotal;
        var discount = await discounts.EvaluateAsync(discountCode, subtotal, userId, ct);
        var merchandise = subtotal - discount.Amount;

        var shippingQuote = await shipping.QuoteAsync(fulfillment, district, merchandise, settings, ct);
        if (!shippingQuote.IsAvailable && shippingQuote.UnavailableReason is not null) issues.Add(shippingQuote.UnavailableReason);

        if (fulfillment == FulfillmentMethod.Delivery)
        {
            var productIds = cart.Items.Select(i => i.ProductId).ToList();
            var pickupOnly = await db.Products.AsNoTracking()
                .Where(p => productIds.Contains(p.Id) && !p.IsDeliveryAvailable).Select(p => p.Name).ToListAsync(ct);
            issues.AddRange(pickupOnly.Select(n => $"{n} is available for local pickup only."));
        }

        var total = merchandise + (shippingQuote.IsAvailable ? shippingQuote.Fee : 0m);
        var methods = paymentMethods.Describe(new PaymentAvailabilityContext(settings, fulfillment, shippingQuote.CashOnDeliveryAvailable, total));
        return new Totals(cart, subtotal, discount, shippingQuote, total, methods, issues);
    }

    public async Task<CheckoutQuoteDto> QuoteAsync(int userId, CheckoutQuoteRequest request, CancellationToken ct)
    {
        var settings = await settingsService.GetAsync(ct);
        var t = await CalculateAsync(userId, request.FulfillmentMethod, request.District, request.DiscountCode, settings, ct);
        if (request.PaymentMethod is { } pm && t.PaymentMethods.FirstOrDefault(m => m.Method == pm) is { IsAvailable: false } m)
            t.Issues.Add(m.UnavailableReason ?? "The selected payment method is unavailable.");

        return new CheckoutQuoteDto(
            t.Cart.Items, t.Subtotal, t.Discount.Amount, t.Discount.Code?.Code, t.Discount.Error,
            new ShippingQuoteDto(t.Shipping.IsAvailable, t.Shipping.Fee, t.Shipping.RateName, t.Shipping.EstimatedDelivery, t.Shipping.UnavailableReason),
            t.Total, CatalogService.Currency, t.PaymentMethods, t.Issues.Count == 0, t.Issues);
    }

    /// <summary>
    /// Holds every cart item for this customer for <see cref="CommerceSettings.ReservationMinutes"/>.
    /// All-or-nothing: if any item can't be held, nothing is held.
    /// </summary>
    public async Task<CheckoutReservationDto> ReserveAsync(int userId, CancellationToken ct)
    {
        var settings = await settingsService.GetAsync(ct);
        var cartItems = await db.CartItems.AsNoTracking().Where(i => i.Cart!.UserId == userId)
            .Select(i => new { i.ProductId, i.Quantity, i.Product!.Name, i.Product.MaxPerOrder }).ToListAsync(ct);
        if (cartItems.Count == 0) throw new BusinessRuleException("Your cart is empty.", "empty_cart");

        var token = Convert.ToHexString(RandomNumberGenerator.GetBytes(16)).ToLowerInvariant();
        var expiresAt = Now.AddMinutes(Math.Clamp(settings.ReservationMinutes, 5, 120));

        await using var tx = await db.Database.BeginTransactionAsync(ct);
        // Restarting checkout replaces earlier holds of the same customer (never stacks them).
        await inventory.ReleaseUserReservationsAsync(userId, ct);

        var unavailable = new List<string>();
        var lines = new List<ReservationLineDto>();
        foreach (var item in cartItems)
        {
            var quantity = Math.Min(item.Quantity, item.MaxPerOrder);
            var reservation = await inventory.TryReserveAsync(item.ProductId, userId, quantity, token, expiresAt, ct);
            if (reservation is null) unavailable.Add(item.Name);
            else lines.Add(new ReservationLineDto(item.ProductId, item.Name, quantity));
        }

        if (unavailable.Count > 0)
        {
            await tx.RollbackAsync(ct);
            db.ChangeTracker.Clear();
            throw new BusinessRuleException(
                $"Sorry, {string.Join(", ", unavailable)} {(unavailable.Count == 1 ? "is" : "are")} no longer available.",
                "items_unavailable");
        }

        await tx.CommitAsync(ct);
        return new CheckoutReservationDto(token, expiresAt, lines);
    }

    public async Task ReleaseAsync(int userId, CancellationToken ct)
    {
        await using var tx = await db.Database.BeginTransactionAsync(ct);
        await inventory.ReleaseUserReservationsAsync(userId, ct);
        await tx.CommitAsync(ct);
    }

    public async Task<PlaceOrderResult> PlaceOrderAsync(int userId, PlaceOrderRequest request, CancellationToken ct)
    {
        // Idempotency: a retry with the same key returns the original order.
        var existing = await FindByIdempotencyKeyAsync(userId, request.IdempotencyKey, ct);
        if (existing is not null) return new PlaceOrderResult(existing, true);

        var settings = await settingsService.GetAsync(ct);
        var user = await db.Users.AsNoTracking().FirstAsync(u => u.Id == userId, ct);

        await using var tx = await db.Database.BeginTransactionAsync(ct);
        try
        {
            var t = await CalculateAsync(userId, request.FulfillmentMethod, request.Address?.District, request.DiscountCode, settings, ct);
            if (t.Issues.Count > 0) throw new BusinessRuleException(string.Join(" ", t.Issues), "checkout_invalid");
            if (!string.IsNullOrWhiteSpace(request.DiscountCode) && !t.Discount.IsValid)
                throw new BusinessRuleException(t.Discount.Error ?? "Invalid discount code.", "invalid_discount");

            var method = t.PaymentMethods.FirstOrDefault(m => m.Method == request.PaymentMethod);
            if (method is not { IsAvailable: true })
                throw new BusinessRuleException(method?.UnavailableReason ?? "The selected payment method is unavailable.", "payment_unavailable");

            // Protects the customer from paying a total they did not see (e.g. a price changed mid-checkout).
            if (request.ExpectedTotal is { } expected && expected != t.Total)
                throw new BusinessRuleException("Your order total has changed. Please review your order again.", "total_changed");

            var order = BuildOrder(userId, user, request, t, settings);
            db.Orders.Add(order);
            await db.SaveChangesAsync(ct); // Unique (UserId, IdempotencyKey) is enforced here.

            await ConsumeInventoryAsync(userId, order, ct);

            if (t.Discount.Code is { } code)
            {
                if (!await discounts.TryClaimUsageAsync(code.Id, ct))
                    throw new BusinessRuleException("This discount code has just reached its usage limit.", "discount_exhausted");
                db.DiscountUsages.Add(new DiscountUsage
                {
                    DiscountCodeId = code.Id, OrderId = order.Id, UserId = userId, Amount = t.Discount.Amount, CreatedAt = Now
                });
            }

            if (request.SaveAddress && request.FulfillmentMethod == FulfillmentMethod.Delivery && request.Address is { } a)
                await SaveAddressAsync(userId, request.Contact, a, ct);

            await db.SaveChangesAsync(ct);
            await carts.ClearAsync(userId, ct);
            await tx.CommitAsync(ct);

            logger.LogInformation("Order {OrderNumber} placed by user {UserId} total {Total}", order.OrderNumber, userId, order.Total);
            return new PlaceOrderResult(await orderQueries.GetForCustomerAsync(userId, order.Id, ct), false);
        }
        catch (DbUpdateException ex)
        {
            await tx.RollbackAsync(ct);
            db.ChangeTracker.Clear();
            // A concurrent request with the same idempotency key won the race: return its order.
            var duplicate = await FindByIdempotencyKeyAsync(userId, request.IdempotencyKey, ct);
            if (duplicate is not null) return new PlaceOrderResult(duplicate, true);
            logger.LogError(ex, "Order placement failed for user {UserId}", userId);
            throw new ConflictException("We couldn't place your order because something changed. Please try again.");
        }
        catch
        {
            await tx.RollbackAsync(CancellationToken.None);
            db.ChangeTracker.Clear();
            throw;
        }
    }

    private Order BuildOrder(int userId, User user, PlaceOrderRequest r, Totals t, CommerceSettings settings)
    {
        var now = Now;
        var delivery = r.FulfillmentMethod == FulfillmentMethod.Delivery ? r.Address : null;
        var order = new Order
        {
            UserId = userId,
            OrderNumber = GenerateOrderNumber(settings.OrderNumberPrefix, now),
            IdempotencyKey = r.IdempotencyKey,
            Status = OrderStatus.Pending,
            PaymentStatus = PaymentStatus.Unpaid,
            PaymentMethod = r.PaymentMethod,
            FulfillmentMethod = r.FulfillmentMethod,
            CustomerName = r.Contact.FullName.Trim(),
            CustomerEmail = string.IsNullOrWhiteSpace(r.Contact.Email) ? user.Email : r.Contact.Email.Trim(),
            CustomerPhone = r.Contact.Phone.Trim(),
            AddressLine1 = delivery?.Line1.Trim(),
            AddressLine2 = Text.TrimToNull(delivery?.Line2),
            City = delivery?.City.Trim(),
            District = delivery?.District,
            PostalCode = Text.TrimToNull(delivery?.PostalCode),
            CustomerNotes = Text.TrimToNull(r.Notes),
            Currency = CatalogService.Currency,
            Subtotal = t.Subtotal,
            DiscountTotal = t.Discount.Amount,
            DiscountCodeId = t.Discount.Code?.Id,
            DiscountCodeSnapshot = t.Discount.Code?.Code,
            ShippingTotal = t.Shipping.Fee,
            ShippingRateName = t.Shipping.RateName,
            Total = t.Total,
            PlacedAt = now,
            PaymentDueAt = r.PaymentMethod == PaymentMethod.BankTransfer && settings.BankTransferPaymentWindowHours > 0
                ? now.AddHours(settings.BankTransferPaymentWindowHours)
                : null
        };
        order.Items = t.Cart.Items.Select(l => new OrderItem
        {
            ProductId = l.ProductId, ProductName = l.Name, ProductSlug = l.Slug, Sku = l.Sku, ImageUrl = l.Image?.Url,
            UnitPrice = l.UnitPrice, Quantity = l.Quantity, LineTotal = l.UnitPrice * l.Quantity
        }).ToList();
        order.Payments.Add(new Payment
        {
            Method = r.PaymentMethod, Amount = t.Total, Currency = order.Currency, Status = PaymentRecordStatus.Pending,
            ProviderName = r.PaymentMethod.ToString()
        });
        order.History.Add(new OrderStatusHistory
        {
            ToStatus = OrderStatus.Pending, ToPaymentStatus = PaymentStatus.Unpaid, Note = "Order placed",
            ChangedByUserId = userId, CreatedAt = now
        });
        return order;
    }

    /// <summary>
    /// Converts the customer's reservations into sold stock. If a reservation expired and was released,
    /// stock is re-acquired atomically; if someone else bought the item meanwhile, the whole order fails.
    /// </summary>
    private async Task ConsumeInventoryAsync(int userId, Order order, CancellationToken ct)
    {
        var reservations = await db.InventoryReservations.AsNoTracking()
            .Where(r => r.UserId == userId && r.Status == ReservationStatus.Active)
            .ToListAsync(ct);

        foreach (var item in order.Items)
        {
            var remaining = item.Quantity;
            foreach (var res in reservations.Where(r => r.ProductId == item.ProductId).ToList())
            {
                reservations.Remove(res);
                if (res.Quantity <= remaining && await inventory.TryCommitReservationAsync(res.Id, order.Id, ct))
                    remaining -= res.Quantity;
                else
                    await inventory.ReleaseReservationAsync(res.Id, ReservationStatus.Released, "Superseded by order", ct);
            }

            if (remaining > 0 && !await inventory.TryHoldStockAsync(item.ProductId, remaining, ct))
                throw new BusinessRuleException($"Sorry, \"{item.ProductName}\" was just purchased by another customer.", "items_unavailable");

            await inventory.MoveReservedToSoldAsync(item.ProductId, item.Quantity, order.Id, userId, ct);
        }

        // Holds on products that are no longer in the cart are given back.
        foreach (var leftover in reservations)
            await inventory.ReleaseReservationAsync(leftover.Id, ReservationStatus.Released, "Not part of the order", ct);
    }

    private async Task SaveAddressAsync(int userId, ContactInfo contact, DeliveryAddress a, CancellationToken ct)
    {
        var exists = await db.UserAddresses.AnyAsync(x => x.UserId == userId && x.Line1 == a.Line1 && x.City == a.City && x.District == a.District, ct);
        if (exists) return;
        var hasDefault = await db.UserAddresses.AnyAsync(x => x.UserId == userId && x.IsDefault, ct);
        db.UserAddresses.Add(new UserAddress
        {
            UserId = userId, Label = "Delivery", FullName = contact.FullName.Trim(), Phone = contact.Phone.Trim(),
            Line1 = a.Line1.Trim(), Line2 = Text.TrimToNull(a.Line2), City = a.City.Trim(), District = a.District,
            PostalCode = Text.TrimToNull(a.PostalCode), IsDefault = !hasDefault
        });
    }

    private async Task<OrderDto?> FindByIdempotencyKeyAsync(int userId, string key, CancellationToken ct)
    {
        var id = await db.Orders.AsNoTracking().Where(o => o.UserId == userId && o.IdempotencyKey == key)
            .Select(o => (int?)o.Id).FirstOrDefaultAsync(ct);
        return id is null ? null : await orderQueries.GetForCustomerAsync(userId, id.Value, ct);
    }

    private static string GenerateOrderNumber(string prefix, DateTime now)
    {
        const string alphabet = "ABCDEFGHJKLMNPQRSTUVWXYZ23456789";
        Span<char> suffix = stackalloc char[6];
        for (var i = 0; i < suffix.Length; i++) suffix[i] = alphabet[RandomNumberGenerator.GetInt32(alphabet.Length)];
        return $"{prefix}-{now:yyMMdd}-{new string(suffix)}";
    }
}
