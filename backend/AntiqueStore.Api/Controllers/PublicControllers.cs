using AntiqueStore.Api.Auth;
using AntiqueStore.Application.Catalog;
using AntiqueStore.Application.Common;
using AntiqueStore.Application.Content;
using AntiqueStore.Application.Customization;
using AntiqueStore.Application.Engagement;
using AntiqueStore.Application.Settings;
using AntiqueStore.Application.Shipping;
using AntiqueStore.Domain.Common;
using AntiqueStore.Domain.Enums;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.OutputCaching;
using Microsoft.AspNetCore.RateLimiting;

namespace AntiqueStore.Api.Controllers;

[ApiController]
[Route("api")]
[AllowAnonymous]
public sealed class CatalogController(CatalogService catalog, ReviewService reviews, ICurrentUser currentUser) : ControllerBase
{
    [HttpGet("products")]
    public Task<PagedResult<ProductCardDto>> Search([FromQuery] ProductQuery query, CancellationToken ct) => catalog.SearchAsync(query, ct);

    [HttpGet("products/facets")]
    [OutputCache(PolicyName = "catalog")]
    public Task<ProductFacetsDto> Facets([FromQuery] string? category, CancellationToken ct) => catalog.GetFacetsAsync(category, ct);

    [HttpGet("products/{slug}")]
    public Task<ProductDetailDto> Get(string slug, CancellationToken ct) => catalog.GetBySlugAsync(slug, ct);

    [HttpGet("products/{slug}/related")]
    public Task<IReadOnlyList<ProductCardDto>> Related(string slug, CancellationToken ct) => catalog.GetRelatedAsync(slug, 4, ct);

    [HttpGet("products/{slug}/reviews")]
    public Task<IReadOnlyList<ReviewDto>> Reviews(string slug, CancellationToken ct) => reviews.ListApprovedAsync(slug, ct);

    [HttpPost("products/{slug}/reviews")]
    [Authorize]
    [EnableRateLimiting("forms")]
    public async Task<ActionResult<ReviewDto>> CreateReview(string slug, CreateReviewRequest request, [FromServices] CommerceSettingsService settings, CancellationToken ct)
    {
        if (!(await settings.GetAsync(ct)).ReviewsEnabled) throw new BusinessRuleException("Reviews are currently disabled.", "reviews_disabled");
        return StatusCode(201, await reviews.CreateAsync(currentUser.RequireUserId(), slug, request, ct));
    }

    [HttpGet("categories")]
    [OutputCache(PolicyName = "catalog")]
    public Task<IReadOnlyList<CategoryDto>> Categories(CancellationToken ct) => catalog.GetCategoriesAsync(false, ct);

    [HttpGet("categories/{slug}")]
    public Task<CategoryDto> Category(string slug, CancellationToken ct) => catalog.GetCategoryAsync(slug, ct);

    [HttpGet("collections")]
    [OutputCache(PolicyName = "catalog")]
    public Task<IReadOnlyList<CollectionDto>> Collections(CancellationToken ct) => catalog.GetCollectionsAsync(false, ct);

    [HttpGet("collections/{slug}")]
    public Task<CollectionDto> Collection(string slug, CancellationToken ct) => catalog.GetCollectionAsync(slug, ct);
}

[ApiController]
[Route("api")]
[AllowAnonymous]
public sealed class StoreController(CustomizationService customization, ContentPageService pages, ShippingService shipping,
    CommerceSettingsService settings) : ControllerBase
{
    /// <summary>Published brand, theme, navigation and public commerce settings.</summary>
    [HttpGet("store/settings")]
    [OutputCache(PolicyName = "site")]
    public Task<SiteConfigDto> Settings(CancellationToken ct) => customization.GetSiteConfigAsync(ContentStage.Published, ct);

    [HttpGet("homepage")]
    [OutputCache(PolicyName = "site")]
    public Task<HomepageDto> Homepage(CancellationToken ct) => customization.GetHomepageAsync(ContentStage.Published, ct);

    [HttpGet("pages/{slug}")]
    public Task<ContentPageDto> Page(string slug, CancellationToken ct) => pages.GetPublishedAsync(slug, ct);

    [HttpGet("shipping/options")]
    public async Task<ShippingOptionsDto> ShippingOptions(CancellationToken ct) => await shipping.GetOptionsAsync(await settings.GetAsync(ct), ct);
}

[ApiController]
[Route("api")]
[AllowAnonymous]
[EnableRateLimiting("forms")]
public sealed class EnquiriesController(EnquiryService enquiries, NewsletterService newsletter) : ControllerBase
{
    [HttpPost("contact")]
    public Task<EnquirySubmittedDto> Contact(EnquiryRequest request, CancellationToken ct) =>
        enquiries.SubmitAsync(request with { ProductId = null }, EnquiryType.Contact, ct);

    [HttpPost("enquiries")]
    public Task<EnquirySubmittedDto> ProductEnquiry(EnquiryRequest request, CancellationToken ct) =>
        enquiries.SubmitAsync(request, EnquiryType.Product, ct);

    [HttpPost("newsletter")]
    public async Task<IActionResult> Subscribe(NewsletterRequest request, CancellationToken ct)
    {
        await newsletter.SubscribeAsync(request, ct);
        return Ok(new { subscribed = true });
    }

    [HttpPost("newsletter/unsubscribe")]
    public async Task<IActionResult> Unsubscribe([FromQuery] string token, CancellationToken ct) =>
        await newsletter.UnsubscribeAsync(token, ct) ? Ok(new { unsubscribed = true }) : NotFound();
}

/// <summary>Draft previews. Admin-only, so unpublished content can never reach customers.</summary>
[ApiController]
[Route("api/admin/preview")]
[Authorize(Policy = AuthConstants.AdminPolicy)]
public sealed class PreviewController(CustomizationService customization, ContentPageService pages) : ControllerBase
{
    [HttpGet("site")]
    public Task<SiteConfigDto> Site(CancellationToken ct) => customization.GetSiteConfigAsync(ContentStage.Draft, ct);

    [HttpGet("homepage")]
    public Task<HomepageDto> Homepage(CancellationToken ct) => customization.GetHomepageAsync(ContentStage.Draft, ct);

    [HttpGet("pages/{slug}")]
    public Task<ContentPageDto> Page(string slug, CancellationToken ct) => pages.GetDraftPreviewAsync(slug, ct);
}
