using AntiqueStore.Api.Auth;
using AntiqueStore.Application.Admin;
using AntiqueStore.Application.Catalog;
using AntiqueStore.Application.Common;
using AntiqueStore.Application.Content;
using AntiqueStore.Application.Customization;
using AntiqueStore.Application.Engagement;
using AntiqueStore.Application.Media;
using AntiqueStore.Application.Orders;
using AntiqueStore.Application.Reports;
using AntiqueStore.Application.Users;
using AntiqueStore.Domain.Common;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.AspNetCore.OutputCaching;

namespace AntiqueStore.Api.Controllers;

/// <summary>After any successful admin write, cached public responses (catalog, site settings) are evicted.</summary>
public sealed class EvictPublicCacheFilter(IOutputCacheStore cache) : IAsyncResultFilter
{
    public async Task OnResultExecutionAsync(ResultExecutingContext context, ResultExecutionDelegate next)
    {
        var executed = await next();
        if (HttpMethods.IsGet(context.HttpContext.Request.Method) || executed.Exception is not null) return;
        if (context.HttpContext.Response.StatusCode is >= 200 and < 300)
        {
            await cache.EvictByTagAsync("site", default);
            await cache.EvictByTagAsync("catalog", default);
        }
    }
}

[ApiController]
[Route("api/admin")]
[Authorize(Policy = AuthConstants.AdminPolicy)]
[TypeFilter<EvictPublicCacheFilter>]
public abstract class AdminControllerBase : ControllerBase;

public sealed class AdminDashboardController(ReportingService reports) : AdminControllerBase
{
    [HttpGet("dashboard")]
    [Authorize(Policy = Permissions.ViewReports)]
    public Task<DashboardDto> Dashboard(CancellationToken ct) => reports.GetDashboardAsync(ct);

    [HttpGet("reports/sales")]
    [Authorize(Policy = Permissions.ViewReports)]
    public Task<SalesReportDto> Sales([FromQuery] DateOnly? from, [FromQuery] DateOnly? to, CancellationToken ct) =>
        reports.GetSalesReportAsync(from, to, ct);
}

[Authorize(Policy = Permissions.ManageCatalog)]
public sealed class AdminProductsController(AdminProductService products) : AdminControllerBase
{
    [HttpGet("products")]
    public Task<PagedResult<AdminProductListItemDto>> List([FromQuery] AdminProductQuery query, CancellationToken ct) => products.ListAsync(query, ct);

    [HttpGet("products/{id:int}")]
    public Task<AdminProductDto> Get(int id, CancellationToken ct) => products.GetAsync(id, ct);

    [HttpPost("products")]
    public async Task<ActionResult<AdminProductDto>> Create(ProductUpsertRequest request, CancellationToken ct) =>
        StatusCode(201, await products.CreateAsync(request, ct));

    [HttpPut("products/{id:int}")]
    public Task<AdminProductDto> Update(int id, ProductUpsertRequest request, CancellationToken ct) => products.UpdateAsync(id, request, ct);

    [HttpDelete("products/{id:int}")]
    public Task<DeleteProductResult> Delete(int id, CancellationToken ct) => products.DeleteAsync(id, ct);

    [HttpPost("products/{id:int}/duplicate")]
    public async Task<ActionResult<AdminProductDto>> Duplicate(int id, CancellationToken ct) => StatusCode(201, await products.DuplicateAsync(id, ct));

    [HttpPatch("products/{id:int}/status")]
    public Task<AdminProductDto> SetStatus(int id, SetProductStatusRequest request, CancellationToken ct) => products.SetStatusAsync(id, request.Status, ct);

    [HttpPatch("products/{id:int}/featured")]
    public Task<AdminProductDto> SetFeatured(int id, SetFeaturedRequest request, CancellationToken ct) => products.SetFeaturedAsync(id, request.IsFeatured, ct);

    [HttpPost("products/{id:int}/stock")]
    public Task<AdminProductDto> AdjustStock(int id, AdjustStockRequest request, CancellationToken ct) => products.AdjustStockAsync(id, request, ct);

    [HttpPost("products/{id:int}/mark-sold")]
    public Task<AdminProductDto> MarkSold(int id, MarkSoldRequest request, CancellationToken ct) => products.MarkSoldAsync(id, request, ct);

    [HttpGet("products/{id:int}/movements")]
    public Task<IReadOnlyList<InventoryMovementDto>> Movements(int id, CancellationToken ct) => products.GetMovementsAsync(id, ct);

    [HttpGet("inventory/reservations")]
    public Task<IReadOnlyList<ActiveReservationDto>> Reservations(CancellationToken ct) => products.GetActiveReservationsAsync(ct);
}

[Authorize(Policy = Permissions.ManageCatalog)]
public sealed class AdminTaxonomyController(AdminTaxonomyService taxonomy) : AdminControllerBase
{
    [HttpGet("categories")]
    public Task<IReadOnlyList<CategoryDto>> Categories(CancellationToken ct) => taxonomy.ListCategoriesAsync(ct);

    [HttpPost("categories")]
    public async Task<ActionResult<CategoryDto>> CreateCategory(CategoryUpsertRequest request, CancellationToken ct) =>
        StatusCode(201, await taxonomy.CreateCategoryAsync(request, ct));

    [HttpPut("categories/{id:int}")]
    public Task<CategoryDto> UpdateCategory(int id, CategoryUpsertRequest request, CancellationToken ct) => taxonomy.UpdateCategoryAsync(id, request, ct);

    [HttpDelete("categories/{id:int}")]
    public async Task<IActionResult> DeleteCategory(int id, CancellationToken ct)
    {
        await taxonomy.DeleteCategoryAsync(id, ct);
        return NoContent();
    }

    [HttpPut("categories/order")]
    public async Task<IActionResult> ReorderCategories(ReorderRequest request, CancellationToken ct)
    {
        await taxonomy.ReorderCategoriesAsync(request, ct);
        return NoContent();
    }

    [HttpGet("collections")]
    public Task<IReadOnlyList<CollectionDto>> Collections(CancellationToken ct) => taxonomy.ListCollectionsAsync(ct);

    [HttpPost("collections")]
    public async Task<ActionResult<CollectionDto>> CreateCollection(CollectionUpsertRequest request, CancellationToken ct) =>
        StatusCode(201, await taxonomy.CreateCollectionAsync(request, ct));

    [HttpPut("collections/{id:int}")]
    public Task<CollectionDto> UpdateCollection(int id, CollectionUpsertRequest request, CancellationToken ct) => taxonomy.UpdateCollectionAsync(id, request, ct);

    [HttpDelete("collections/{id:int}")]
    public async Task<IActionResult> DeleteCollection(int id, CancellationToken ct)
    {
        await taxonomy.DeleteCollectionAsync(id, ct);
        return NoContent();
    }
}

[Authorize(Policy = Permissions.ManageOrders)]
public sealed class AdminOrdersController(AdminOrderService orders) : AdminControllerBase
{
    [HttpGet("orders")]
    public Task<PagedResult<AdminOrderListItemDto>> List([FromQuery] AdminOrderQuery query, CancellationToken ct) => orders.ListAsync(query, ct);

    [HttpGet("orders/{id:int}")]
    public Task<AdminOrderDto> Get(int id, CancellationToken ct) => orders.GetAsync(id, ct);

    [HttpPatch("orders/{id:int}/status")]
    public Task<AdminOrderDto> UpdateStatus(int id, UpdateOrderStatusRequest request, CancellationToken ct) => orders.UpdateStatusAsync(id, request, ct);

    [HttpPatch("orders/{id:int}/payment")]
    public Task<AdminOrderDto> UpdatePayment(int id, UpdatePaymentStatusRequest request, CancellationToken ct) => orders.UpdatePaymentStatusAsync(id, request, ct);

    [HttpPost("orders/{id:int}/refunds")]
    public Task<AdminOrderDto> Refund(int id, RecordRefundRequest request, CancellationToken ct) => orders.RecordRefundAsync(id, request, ct);

    [HttpPatch("orders/{id:int}")]
    public Task<AdminOrderDto> UpdateDetails(int id, UpdateOrderDetailsRequest request, CancellationToken ct) => orders.UpdateDetailsAsync(id, request, ct);

    /// <summary>Streams a private payment proof to an authorized admin (never publicly addressable).</summary>
    [HttpGet("orders/{id:int}/proofs/{proofId:int}")]
    public async Task<IActionResult> Proof(int id, int proofId, CancellationToken ct)
    {
        var file = await orders.OpenProofAsync(id, proofId, ct);
        Response.Headers.CacheControl = "private, no-store";
        Response.Headers["Content-Security-Policy"] = "default-src 'none'; img-src 'self'; style-src 'unsafe-inline'; sandbox";
        return File(file.Content, file.ContentType, file.FileName, enableRangeProcessing: false);
    }
}

[Authorize(Policy = Permissions.ManageCustomers)]
public sealed class AdminCustomersController(AdminCustomerService customers) : AdminControllerBase
{
    [HttpGet("customers")]
    public Task<PagedResult<AdminCustomerDto>> List([FromQuery] AdminCustomerQuery query, CancellationToken ct) => customers.ListAsync(query, ct);

    [HttpGet("customers/{id:int}")]
    public Task<AdminCustomerDetailDto> Get(int id, CancellationToken ct) => customers.GetAsync(id, ct);
}

[Authorize(Policy = Permissions.ManageContent)]
public sealed class AdminMediaController(MediaService media) : AdminControllerBase
{
    [HttpGet("media")]
    public Task<PagedResult<MediaAssetDto>> List([FromQuery] MediaQuery query, CancellationToken ct) => media.ListAsync(query, ct);

    [HttpPost("media")]
    [RequestSizeLimit(MediaService.MaxUploadBytes + 1024 * 1024)]
    [Microsoft.AspNetCore.RateLimiting.EnableRateLimiting("uploads")]
    public async Task<ActionResult<MediaAssetDto>> Upload(IFormFile file, [FromForm] string? folder, [FromForm] string? altText, CancellationToken ct)
    {
        if (file is null) throw RequestValidationException.For("file", "Choose an image to upload.");
        await using var stream = file.OpenReadStream();
        return StatusCode(201, await media.UploadAsync(new MediaUpload(stream, file.FileName, file.ContentType, file.Length), folder, altText, ct));
    }

    [HttpPatch("media/{id:int}")]
    public Task<MediaAssetDto> Update(int id, UpdateMediaRequest request, CancellationToken ct) => media.UpdateAsync(id, request, ct);

    [HttpDelete("media/{id:int}")]
    public async Task<IActionResult> Delete(int id, CancellationToken ct)
    {
        await media.DeleteAsync(id, ct);
        return NoContent();
    }
}

[Authorize(Policy = Permissions.ManageContent)]
public sealed class AdminCustomizationController(CustomizationService customization) : AdminControllerBase
{
    [HttpGet("customization")]
    public Task<AdminCustomizationDto> Get(CancellationToken ct) => customization.GetDraftAsync(ct);

    [HttpPut("customization/brand")]
    public Task<AdminCustomizationDto> SaveBrand(BrandSettingsDto request, CancellationToken ct) => customization.SaveBrandAsync(request, ct);

    [HttpPut("customization/theme")]
    public Task<AdminCustomizationDto> SaveTheme(ThemeDto request, CancellationToken ct) => customization.SaveThemeAsync(request, ct);

    [HttpPut("customization/navigation")]
    public Task<AdminCustomizationDto> SaveNavigation(SaveNavigationRequest request, CancellationToken ct) => customization.SaveNavigationAsync(request, ct);

    [HttpGet("homepage")]
    public Task<AdminCustomizationDto> Homepage(CancellationToken ct) => customization.GetDraftAsync(ct);

    [HttpPut("homepage/draft")]
    public Task<AdminCustomizationDto> SaveHomepage(SaveHomepageRequest request, CancellationToken ct) => customization.SaveHomepageAsync(request, ct);

    /// <summary>Publishes all draft customization (brand, theme, navigation and homepage) atomically.</summary>
    [HttpPost("homepage/publish")]
    public Task<AdminCustomizationDto> PublishHomepage(CancellationToken ct) => customization.PublishAsync(ct);

    [HttpPost("customization/publish")]
    public Task<AdminCustomizationDto> Publish(CancellationToken ct) => customization.PublishAsync(ct);

    [HttpPost("customization/discard")]
    public Task<AdminCustomizationDto> Discard(CancellationToken ct) => customization.DiscardDraftAsync(ct);
}

[Authorize(Policy = Permissions.ManageSettings)]
public sealed class AdminSettingsController(AdminSettingsService settings, AdminShippingService shipping) : AdminControllerBase
{
    [HttpGet("settings")]
    public Task<CommerceSettingsDto> Get(CancellationToken ct) => settings.GetAsync(ct);

    [HttpPut("settings")]
    public Task<CommerceSettingsDto> Update(CommerceSettingsDto request, CancellationToken ct) => settings.UpdateAsync(request, ct);

    [HttpGet("shipping/zones")]
    public Task<IReadOnlyList<ShippingZoneDto>> Zones(CancellationToken ct) => shipping.ListAsync(ct);

    [HttpPost("shipping/zones")]
    public async Task<ActionResult<ShippingZoneDto>> CreateZone(ShippingZoneDto request, CancellationToken ct) =>
        StatusCode(201, await shipping.UpsertAsync(null, request, ct));

    [HttpPut("shipping/zones/{id:int}")]
    public Task<ShippingZoneDto> UpdateZone(int id, ShippingZoneDto request, CancellationToken ct) => shipping.UpsertAsync(id, request, ct);

    [HttpDelete("shipping/zones/{id:int}")]
    public async Task<IActionResult> DeleteZone(int id, CancellationToken ct)
    {
        await shipping.DeleteAsync(id, ct);
        return NoContent();
    }
}

[Authorize(Policy = Permissions.ManageContent)]
public sealed class AdminPagesController(ContentPageService pages) : AdminControllerBase
{
    [HttpGet("pages")]
    public Task<IReadOnlyList<AdminContentPageDto>> List(CancellationToken ct) => pages.ListAsync(ct);

    [HttpGet("pages/{id:int}")]
    public Task<AdminContentPageDto> Get(int id, CancellationToken ct) => pages.GetAsync(id, ct);

    [HttpPost("pages")]
    public async Task<ActionResult<AdminContentPageDto>> Create(ContentPageUpsertRequest request, CancellationToken ct) =>
        StatusCode(201, await pages.CreateAsync(request, ct));

    [HttpPut("pages/{id:int}")]
    public Task<AdminContentPageDto> SaveDraft(int id, ContentPageUpsertRequest request, CancellationToken ct) => pages.SaveDraftAsync(id, request, ct);

    [HttpPost("pages/{id:int}/publish")]
    public Task<AdminContentPageDto> Publish(int id, CancellationToken ct) => pages.PublishAsync(id, ct);

    [HttpPost("pages/{id:int}/unpublish")]
    public Task<AdminContentPageDto> Unpublish(int id, CancellationToken ct) => pages.UnpublishAsync(id, ct);

    [HttpPost("pages/{id:int}/discard")]
    public Task<AdminContentPageDto> Discard(int id, CancellationToken ct) => pages.DiscardDraftAsync(id, ct);

    [HttpDelete("pages/{id:int}")]
    public async Task<IActionResult> Delete(int id, CancellationToken ct)
    {
        await pages.DeleteAsync(id, ct);
        return NoContent();
    }
}

[Authorize(Policy = Permissions.ManageCatalog)]
public sealed class AdminPromotionsController(AdminDiscountService discounts) : AdminControllerBase
{
    [HttpGet("discounts")]
    public Task<IReadOnlyList<DiscountCodeDto>> List(CancellationToken ct) => discounts.ListAsync(ct);

    [HttpPost("discounts")]
    public async Task<ActionResult<DiscountCodeDto>> Create(DiscountCodeUpsertRequest request, CancellationToken ct) =>
        StatusCode(201, await discounts.UpsertAsync(null, request, ct));

    [HttpPut("discounts/{id:int}")]
    public Task<DiscountCodeDto> Update(int id, DiscountCodeUpsertRequest request, CancellationToken ct) => discounts.UpsertAsync(id, request, ct);

    [HttpDelete("discounts/{id:int}")]
    public async Task<IActionResult> Delete(int id, CancellationToken ct) =>
        Ok(new { deleted = await discounts.DeleteAsync(id, ct) });
}

[Authorize(Policy = Permissions.ManageContent)]
public sealed class AdminEngagementController(
    ReviewService reviews, EnquiryService enquiries, TestimonialService testimonials, NewsletterService newsletter) : AdminControllerBase
{
    [HttpGet("reviews")]
    public Task<PagedResult<AdminReviewDto>> Reviews([FromQuery] AdminReviewQuery query, CancellationToken ct) => reviews.ListAsync(query, ct);

    [HttpPatch("reviews/{id:int}")]
    public async Task<IActionResult> Moderate(int id, ModerateReviewRequest request, CancellationToken ct)
    {
        await reviews.ModerateAsync(id, request, ct);
        return NoContent();
    }

    [HttpDelete("reviews/{id:int}")]
    public async Task<IActionResult> DeleteReview(int id, CancellationToken ct)
    {
        await reviews.DeleteAsync(id, ct);
        return NoContent();
    }

    [HttpGet("enquiries")]
    public Task<PagedResult<EnquiryDto>> Enquiries([FromQuery] AdminEnquiryQuery query, CancellationToken ct) => enquiries.ListAsync(query, ct);

    [HttpPatch("enquiries/{id:int}")]
    public Task<EnquiryDto> UpdateEnquiry(int id, UpdateEnquiryRequest request, CancellationToken ct) => enquiries.UpdateAsync(id, request, ct);

    [HttpGet("testimonials")]
    public Task<IReadOnlyList<AdminTestimonialDto>> Testimonials(CancellationToken ct) => testimonials.ListAsync(ct);

    [HttpPost("testimonials")]
    public async Task<ActionResult<AdminTestimonialDto>> CreateTestimonial(TestimonialUpsertRequest request, CancellationToken ct) =>
        StatusCode(201, await testimonials.UpsertAsync(null, request, ct));

    [HttpPut("testimonials/{id:int}")]
    public Task<AdminTestimonialDto> UpdateTestimonial(int id, TestimonialUpsertRequest request, CancellationToken ct) => testimonials.UpsertAsync(id, request, ct);

    [HttpDelete("testimonials/{id:int}")]
    public async Task<IActionResult> DeleteTestimonial(int id, CancellationToken ct)
    {
        await testimonials.DeleteAsync(id, ct);
        return NoContent();
    }

    [HttpGet("newsletter")]
    public Task<PagedResult<NewsletterSubscriberDto>> Subscribers([FromQuery] PageRequest page, CancellationToken ct) => newsletter.ListAsync(page, ct);
}

public sealed class AdminAuditController(AuditLogQueryService audit) : AdminControllerBase
{
    [HttpGet("audit-logs")]
    [Authorize(Policy = Permissions.ManageSettings)]
    public Task<PagedResult<AuditLogDto>> List([FromQuery] AuditLogQuery query, CancellationToken ct) => audit.ListAsync(query, ct);
}
