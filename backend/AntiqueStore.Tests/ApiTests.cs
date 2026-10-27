using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using AntiqueStore.Application.Admin;
using AntiqueStore.Application.Carts;
using AntiqueStore.Application.Catalog;
using AntiqueStore.Application.Common;
using AntiqueStore.Application.Content;
using AntiqueStore.Application.Customization;
using AntiqueStore.Application.Orders;
using AntiqueStore.Application.Users;
using AntiqueStore.Domain.Entities;
using AntiqueStore.Domain.Enums;
using AntiqueStore.Tests.Support;
using Microsoft.EntityFrameworkCore;

namespace AntiqueStore.Tests;

public sealed class ApiTests : IClassFixture<ApiFactory>
{
    private readonly ApiFactory _factory;

    public ApiTests(ApiFactory factory) => _factory = factory;

    private static ProductUpsertRequest NewProduct(string sku, ProductStatus status = ProductStatus.Published) => new()
    {
        Name = $"Test Item {sku}", Sku = sku, Price = 25000m, Status = status, InitialQuantity = 1,
        Description = "<p>Nice</p><script>alert('x')</script><img src=x onerror=alert(1)>",
        Images = [new ProductImageInput(null, "/placeholders/product-box.svg", "Test image")], ConditionNotes = "Light wear"
    };

    private async Task<int> EnsureDeliveryAsync()
    {
        return await _factory.WithDbAsync(async db =>
        {
            if (!await db.ShippingZones.AnyAsync())
            {
                db.ShippingZones.Add(new ShippingZone
                {
                    Name = "Colombo", Districts = [new ShippingZoneDistrict { District = "Colombo" }],
                    Rates = [new ShippingRate { Name = "Standard", Fee = 1500m }]
                });
            }
            return await db.SaveChangesAsync();
        });
    }

    [Fact]
    public async Task Health_and_public_catalog_endpoints_work_anonymously()
    {
        var client = _factory.CreateClient();
        Assert.Equal(HttpStatusCode.OK, (await client.GetAsync("/health/live")).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await client.GetAsync("/health/ready")).StatusCode);
        var products = await (await client.GetAsync("/api/products?pageSize=5")).ReadAsync<PagedResult<ProductCardDto>>();
        Assert.NotNull(products);
        var site = await (await client.GetAsync("/api/store/settings")).ReadAsync<SiteConfigDto>();
        Assert.False(site.IsPreview);
        Assert.Equal("#B08D57", site.Theme.AccentColor);
        Assert.Contains("Disallow: /admin", await client.GetStringAsync("/robots.txt"));
    }

    [Fact]
    public async Task Protected_endpoints_require_a_valid_token()
    {
        var anonymous = _factory.CreateClient();
        Assert.Equal(HttpStatusCode.Unauthorized, (await anonymous.GetAsync("/api/me")).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await anonymous.GetAsync("/api/admin/dashboard")).StatusCode);

        var forged = _factory.CreateClient();
        forged.DefaultRequestHeaders.Authorization = new("Bearer", "not-a-real-token");
        Assert.Equal(HttpStatusCode.Unauthorized, (await forged.GetAsync("/api/me")).StatusCode);
    }

    [Fact]
    public async Task Customers_are_forbidden_from_admin_endpoints_and_cannot_elevate_themselves()
    {
        var customer = _factory.ClientFor("mallory");
        var me = await (await customer.GetAsync("/api/me")).ReadAsync<MeDto>();
        Assert.Equal(UserRole.Customer, me.Role);

        Assert.Equal(HttpStatusCode.Forbidden, (await customer.GetAsync("/api/admin/products")).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await customer.PostJsonAsync("/api/admin/products", NewProduct("HACK-1"))).StatusCode);

        // Overposting attempt: role/email in the body must be ignored.
        var response = await customer.PutAsync("/api/me", JsonContent.Create(new { displayName = "Mallory", role = "Admin", email = "boss@example.test" }));
        var updated = await response.ReadAsync<MeDto>();
        Assert.Equal(UserRole.Customer, updated.Role);
        Assert.Equal("mallory@example.test", updated.Email);
        Assert.Equal("Mallory", updated.DisplayName);
        Assert.Equal(HttpStatusCode.Forbidden, (await customer.GetAsync("/api/admin/products")).StatusCode);
    }

    [Fact]
    public async Task Admin_product_crud_sanitizes_html_and_archives_products_with_orders()
    {
        var admin = await _factory.AdminClientAsync("admin-crud");
        var created = await (await admin.PostJsonAsync("/api/admin/products", NewProduct("CRUD-1"))).ReadAsync<AdminProductDto>();
        Assert.DoesNotContain("<script", created.Description);
        Assert.DoesNotContain("onerror", created.Description);
        Assert.Equal(1, created.QuantityAvailable);
        Assert.Equal("CRUD-1", created.Sku);

        var duplicateSku = await admin.PostJsonAsync("/api/admin/products", NewProduct("CRUD-1"));
        Assert.Equal(HttpStatusCode.BadRequest, duplicateSku.StatusCode);

        var update = NewProduct("CRUD-1") with { Name = "Renamed Item", Price = 30000m, Version = created.Version };
        var updated = await (await admin.PutJsonAsync($"/api/admin/products/{created.Id}", update)).ReadAsync<AdminProductDto>();
        Assert.Equal("Renamed Item", updated.Name);
        Assert.Equal(30000m, updated.Price);

        var stale = await admin.PutJsonAsync($"/api/admin/products/{created.Id}", update with { Version = created.Version });
        Assert.Equal(HttpStatusCode.Conflict, stale.StatusCode);

        var publicView = await (await _factory.CreateClient().GetAsync($"/api/products/{updated.Slug}")).ReadAsync<ProductDetailDto>();
        Assert.Equal(InventoryState.Available, publicView.InventoryState);

        // Never-ordered product is hard deleted.
        var deleted = await (await admin.DeleteAsync($"/api/admin/products/{created.Id}")).ReadAsync<DeleteProductResult>();
        Assert.True(deleted.Deleted);
        Assert.Equal(HttpStatusCode.NotFound, (await _factory.CreateClient().GetAsync($"/api/products/{updated.Slug}")).StatusCode);
    }

    [Fact]
    public async Task Invalid_requests_return_field_level_validation_errors()
    {
        var admin = await _factory.AdminClientAsync("admin-validation");
        var response = await admin.PostJsonAsync("/api/admin/products", NewProduct("") with { Name = "", Price = -5 });
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        using var doc = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        var errors = doc.RootElement.GetProperty("errors");
        Assert.True(errors.TryGetProperty("name", out _));
        Assert.True(errors.TryGetProperty("sku", out _));
        Assert.True(errors.TryGetProperty("price", out _));
        Assert.Equal("validation", doc.RootElement.GetProperty("code").GetString());
    }

    [Fact]
    public async Task Full_purchase_flow_with_admin_fulfilment()
    {
        await EnsureDeliveryAsync();
        var admin = await _factory.AdminClientAsync("admin-flow");
        var product = await (await admin.PostJsonAsync("/api/admin/products", NewProduct("FLOW-1"))).ReadAsync<AdminProductDto>();

        var customer = _factory.ClientFor("flow-customer");
        var cart = await (await customer.PostJsonAsync("/api/cart/items", new AddCartItemRequest(product.Id))).ReadAsync<CartDto>();
        Assert.Single(cart.Items);

        var quote = await (await customer.PostJsonAsync("/api/checkout/quote",
            new CheckoutQuoteRequest(FulfillmentMethod.Delivery, "Colombo", null, PaymentMethod.CashOnDelivery))).ReadAsync<CheckoutQuoteDto>();
        Assert.True(quote.CanPlaceOrder);
        Assert.Equal(26500m, quote.Total);

        var reservation = await (await customer.PostAsync("/api/checkout/reservations", null)).ReadAsync<CheckoutReservationDto>();
        Assert.Single(reservation.Items);

        var request = Builders.Order(key: "flow-key-000000000001", expectedTotal: quote.Total) with { CheckoutToken = reservation.CheckoutToken };
        var placed = await customer.PostJsonAsync("/api/orders", request);
        Assert.Equal(HttpStatusCode.Created, placed.StatusCode);
        var order = await placed.ReadAsync<OrderDto>();

        var retry = await customer.PostJsonAsync("/api/orders", request);
        Assert.Equal(HttpStatusCode.OK, retry.StatusCode);
        Assert.Equal(order.Id, (await retry.ReadAsync<OrderDto>()).Id);

        var mine = await (await customer.GetAsync("/api/orders/my")).ReadAsync<PagedResult<OrderSummaryDto>>();
        Assert.Contains(mine.Items, o => o.Id == order.Id);
        Assert.Equal(HttpStatusCode.NotFound, (await _factory.ClientFor("someone-else").GetAsync($"/api/orders/{order.Id}")).StatusCode);

        var list = await (await admin.GetAsync($"/api/admin/orders?q={order.OrderNumber}")).ReadAsync<PagedResult<AdminOrderListItemDto>>();
        Assert.Single(list.Items);

        var confirmed = await (await admin.PatchAsync($"/api/admin/orders/{order.Id}/status",
            JsonContent.Create(new UpdateOrderStatusRequest(OrderStatus.Confirmed, "Confirmed by phone"), options: ApiFactory.Json))).ReadAsync<AdminOrderDto>();
        Assert.Equal(OrderStatus.Confirmed, confirmed.Order.Status);
        var shipped = await (await admin.PatchAsync($"/api/admin/orders/{order.Id}/status",
            JsonContent.Create(new UpdateOrderStatusRequest(OrderStatus.Shipped, null, true, "Courier", "TRK123"), options: ApiFactory.Json))).ReadAsync<AdminOrderDto>();
        Assert.Equal("TRK123", shipped.Order.TrackingNumber);

        var invalid = await admin.PatchAsync($"/api/admin/orders/{order.Id}/status",
            JsonContent.Create(new UpdateOrderStatusRequest(OrderStatus.Pending, null), options: ApiFactory.Json));
        Assert.Equal(HttpStatusCode.UnprocessableEntity, invalid.StatusCode);

        var customerView = await (await customer.GetAsync($"/api/orders/{order.Id}")).ReadAsync<OrderDto>();
        Assert.Equal(OrderStatus.Shipped, customerView.Status);
        Assert.Equal(PaymentStatus.Unpaid, customerView.PaymentStatus);

        var publicProduct = await (await _factory.CreateClient().GetAsync($"/api/products/{product.Slug}")).ReadAsync<ProductDetailDto>();
        Assert.Equal(InventoryState.Sold, publicProduct.InventoryState);
    }

    [Fact]
    public async Task Unverified_email_cannot_place_orders()
    {
        await EnsureDeliveryAsync();
        var admin = await _factory.AdminClientAsync("admin-verify");
        var product = await (await admin.PostJsonAsync("/api/admin/products", NewProduct("VERIFY-1"))).ReadAsync<AdminProductDto>();
        var unverified = _factory.ClientFor("unverified-user", verified: false);
        (await unverified.PostJsonAsync("/api/cart/items", new AddCartItemRequest(product.Id))).EnsureSuccessStatusCode();

        Assert.Equal(HttpStatusCode.Forbidden, (await unverified.PostJsonAsync("/api/orders", Builders.Order())).StatusCode);
    }

    [Fact]
    public async Task Two_customers_racing_over_http_for_one_item_only_one_order_succeeds()
    {
        await EnsureDeliveryAsync();
        var admin = await _factory.AdminClientAsync("admin-race");
        var product = await (await admin.PostJsonAsync("/api/admin/products", NewProduct("RACE-1"))).ReadAsync<AdminProductDto>();
        var clients = Enumerable.Range(0, 5).Select(i => _factory.ClientFor($"racer-{i}")).ToList();
        foreach (var c in clients) (await c.PostJsonAsync("/api/cart/items", new AddCartItemRequest(product.Id))).EnsureSuccessStatusCode();

        var responses = await Task.WhenAll(clients.Select(c => c.PostJsonAsync("/api/orders", Builders.Order())));

        Assert.Single(responses, r => r.StatusCode == HttpStatusCode.Created);
        Assert.All(responses.Where(r => r.StatusCode != HttpStatusCode.Created), r => Assert.Equal(HttpStatusCode.UnprocessableEntity, r.StatusCode));
        Assert.Equal(1, await _factory.WithDbAsync(db => db.OrderItems.CountAsync(i => i.ProductId == product.Id)));
    }

    [Fact]
    public async Task Homepage_draft_is_invisible_to_customers_until_published()
    {
        var admin = await _factory.AdminClientAsync("admin-homepage");
        var draft = await (await admin.GetAsync("/api/admin/customization")).ReadAsync<AdminCustomizationDto>();
        var hero = draft.Sections.First(s => s.Type == SectionTypes.Hero);
        var newConfig = JsonSerializer.SerializeToElement(new HeroConfig(null, "Spring Collection Arrives", "Fresh finds", null,
            new CtaLink("Shop now", "/shop"), null), SectionConfigValidator.Json);
        var sections = draft.Sections.Select(s => new HomepageSectionInput(s.Key, s.Type, s.IsVisible, s.Key == hero.Key ? newConfig : s.Config)).ToList();

        var saved = await (await admin.PutJsonAsync("/api/admin/homepage/draft", new SaveHomepageRequest(sections))).ReadAsync<AdminCustomizationDto>();
        Assert.True(saved.HasUnpublishedChanges);

        string HeroTitle(HomepageDto h) => h.Sections.First(s => s.Type == SectionTypes.Hero).Config.GetProperty("title").GetString()!;
        var publicBefore = await (await _factory.CreateClient().GetAsync("/api/homepage")).ReadAsync<HomepageDto>();
        Assert.NotEqual("Spring Collection Arrives", HeroTitle(publicBefore));
        Assert.Equal(HttpStatusCode.Unauthorized, (await _factory.CreateClient().GetAsync("/api/admin/preview/homepage")).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await _factory.ClientFor("nosy-customer").GetAsync("/api/admin/preview/homepage")).StatusCode);
        var preview = await (await admin.GetAsync("/api/admin/preview/homepage")).ReadAsync<HomepageDto>();
        Assert.Equal("Spring Collection Arrives", HeroTitle(preview));

        var published = await (await admin.PostAsync("/api/admin/homepage/publish", null)).ReadAsync<AdminCustomizationDto>();
        Assert.False(published.HasUnpublishedChanges);
        var publicAfter = await (await _factory.CreateClient().GetAsync("/api/homepage")).ReadAsync<HomepageDto>();
        Assert.Equal("Spring Collection Arrives", HeroTitle(publicAfter));
    }

    [Fact]
    public async Task Homepage_sections_reject_unsafe_links_and_unknown_types()
    {
        var admin = await _factory.AdminClientAsync("admin-unsafe");
        var badHero = JsonSerializer.SerializeToElement(new HeroConfig(null, "Hi", null, null, new CtaLink("Click", "javascript:alert(1)"), null), SectionConfigValidator.Json);
        var r1 = await admin.PutJsonAsync("/api/admin/homepage/draft", new SaveHomepageRequest([new HomepageSectionInput(null, SectionTypes.Hero, true, badHero)]));
        Assert.Equal(HttpStatusCode.BadRequest, r1.StatusCode);

        var r2 = await admin.PutJsonAsync("/api/admin/homepage/draft", new SaveHomepageRequest([new HomepageSectionInput(null, "script", true, badHero)]));
        Assert.Equal(HttpStatusCode.BadRequest, r2.StatusCode);
    }

    [Fact]
    public async Task Theme_changes_require_publishing_and_reject_invalid_colors()
    {
        var admin = await _factory.AdminClientAsync("admin-theme");
        var draft = await (await admin.GetAsync("/api/admin/customization")).ReadAsync<AdminCustomizationDto>();

        var bad = await admin.PutJsonAsync("/api/admin/customization/theme", draft.Theme with { AccentColor = "red;}</style><script>" });
        Assert.Equal(HttpStatusCode.BadRequest, bad.StatusCode);
        var badFont = await admin.PutJsonAsync("/api/admin/customization/theme", draft.Theme with { HeadingFont = "Comic Sans" });
        Assert.Equal(HttpStatusCode.BadRequest, badFont.StatusCode);

        (await admin.PutJsonAsync("/api/admin/customization/theme", draft.Theme with { AccentColor = "#123456" })).EnsureSuccessStatusCode();
        await admin.PostAsync("/api/admin/customization/publish", null);
        var site = await (await _factory.CreateClient().GetAsync("/api/store/settings")).ReadAsync<SiteConfigDto>();
        Assert.Equal("#123456", site.Theme.AccentColor);

        // restore default for other tests
        await admin.PutJsonAsync("/api/admin/customization/theme", draft.Theme);
        await admin.PostAsync("/api/admin/customization/publish", null);
    }

    [Fact]
    public async Task Content_pages_are_sanitized_and_drafts_stay_private()
    {
        var admin = await _factory.AdminClientAsync("admin-pages");
        var pages = await (await admin.GetAsync("/api/admin/pages")).ReadAsync<List<AdminContentPageDto>>();
        var about = pages.First(p => p.Slug == "about");

        var saved = await (await admin.PutJsonAsync($"/api/admin/pages/{about.Id}",
            new ContentPageUpsertRequest(null, "About Us (draft)", "<p>Hello</p><script>steal()</script><a href=\"javascript:x\">x</a>", null, null)))
            .ReadAsync<AdminContentPageDto>();
        Assert.DoesNotContain("<script", saved.DraftBody);
        Assert.DoesNotContain("javascript:", saved.DraftBody);

        var live = await (await _factory.CreateClient().GetAsync("/api/pages/about")).ReadAsync<ContentPageDto>();
        Assert.NotEqual("About Us (draft)", live.Title);

        await admin.PostAsync($"/api/admin/pages/{about.Id}/publish", null);
        live = await (await _factory.CreateClient().GetAsync("/api/pages/about")).ReadAsync<ContentPageDto>();
        Assert.Equal("About Us (draft)", live.Title);
    }

    [Fact]
    public async Task Product_enquiries_are_stored_and_honeypot_spam_is_flagged()
    {
        var admin = await _factory.AdminClientAsync("admin-enquiry");
        var product = await (await admin.PostJsonAsync("/api/admin/products", NewProduct("ENQ-1"))).ReadAsync<AdminProductDto>();
        var anon = _factory.CreateClient();

        var ok = await anon.PostJsonAsync("/api/enquiries", new { productId = product.Id, name = "Nimal", email = "nimal@example.test", message = "Is this still available?" });
        ok.EnsureSuccessStatusCode();
        var spam = await anon.PostJsonAsync("/api/enquiries", new { productId = product.Id, name = "Bot", email = "bot@example.test", message = "Buy cheap watches now", website = "http://spam" });
        spam.EnsureSuccessStatusCode();

        var statuses = await _factory.WithDbAsync(db => db.Enquiries.Where(e => e.ProductId == product.Id).Select(e => e.Status).ToListAsync());
        Assert.Contains(EnquiryStatus.New, statuses);
        Assert.Contains(EnquiryStatus.Spam, statuses);
    }
}
