using System.Text.Json;
using AntiqueStore.Application.Common;
using AntiqueStore.Domain.Enums;

namespace AntiqueStore.Application.Catalog;

public sealed record ImageDto(string Url, string? Alt, int? Width, int? Height, string? SrcSet)
{
    public static ImageDto? From(string? url, string? alt, int? width, int? height, string? variantsJson)
    {
        if (string.IsNullOrEmpty(url)) return null;
        return new ImageDto(url, alt, width, height, BuildSrcSet(variantsJson));
    }

    public static string? BuildSrcSet(string? variantsJson)
    {
        if (string.IsNullOrWhiteSpace(variantsJson) || variantsJson == "{}") return null;
        try
        {
            var variants = JsonSerializer.Deserialize<Dictionary<string, string>>(variantsJson);
            if (variants is null || variants.Count == 0) return null;
            return string.Join(", ", variants
                .Select(kv => (Width: int.TryParse(kv.Key, out var w) ? w : 0, Url: kv.Value))
                .Where(v => v.Width > 0 && Text.IsSafeImageUrl(v.Url))
                .OrderBy(v => v.Width)
                .Select(v => $"{v.Url} {v.Width}w"));
        }
        catch (JsonException)
        {
            return null;
        }
    }
}

public sealed record ProductCardDto(
    int Id, string Slug, string Name, decimal Price, decimal? CompareAtPrice, string Currency,
    string? CategoryName, string? CategorySlug, ProductCondition Condition, string? Era,
    ImageDto? Image, ImageDto? HoverImage, InventoryState InventoryState, bool IsFeatured, int MaxPerOrder);

public sealed record ProductAttributeDto(string Name, string Value);

public sealed record ConditionReportDto(
    ProductCondition Overall, string? Notes, string? SignsOfWear, string? Scratches, string? Damage,
    string? Restoration, string? MissingParts, string? FunctionalStatus);

public sealed record ProductDetailDto(
    int Id, string Slug, string Name, string Sku, decimal Price, decimal? CompareAtPrice, string Currency,
    string? ShortDescription, string? Description,
    CategoryRefDto? Category, IReadOnlyList<CategoryRefDto> Collections,
    ConditionReportDto ConditionReport,
    string? Material, string? Color, string? Dimensions, decimal? WeightKg, string? Era, string? ApproximateAge,
    string? Provenance, bool HasProvenanceEvidence, string? ProvenanceEvidence,
    InventoryState InventoryState, int MaxPerOrder, bool IsDeliveryAvailable,
    IReadOnlyList<ImageDto> Images, IReadOnlyList<ProductAttributeDto> Attributes,
    string? SeoTitle, string? SeoDescription, DateTime? PublishedAt,
    ReviewSummaryDto Reviews);

public sealed record ReviewSummaryDto(int Count, double? AverageRating);

public sealed record CategoryRefDto(int Id, string Name, string Slug);

public sealed record CategoryDto(
    int Id, string Name, string Slug, string? Description, string? ImageUrl, int? ParentId,
    int SortOrder, bool IsVisible, bool IsFeatured, int ProductCount, string? SeoTitle, string? SeoDescription);

public sealed record CollectionDto(
    int Id, string Name, string Slug, string? Description, string? ImageUrl, int SortOrder, bool IsVisible,
    int ProductCount, string? SeoTitle, string? SeoDescription);

public enum ProductSort
{
    Featured,
    Newest,
    PriceAsc,
    PriceDesc,
    RecentlyAdded
}

public enum AvailabilityFilter
{
    All,
    Available,
    Sold
}

public sealed record ProductQuery : PageRequest
{
    public string? Q { get; init; }
    public string? Category { get; init; }
    public string? Collection { get; init; }
    public decimal? MinPrice { get; init; }
    public decimal? MaxPrice { get; init; }
    public ProductCondition[]? Condition { get; init; }
    public string[]? Era { get; init; }
    public string[]? Material { get; init; }
    /// <summary>Restrict to specific products (e.g. a guest's locally saved wishlist). Max 100.</summary>
    public int[]? Ids { get; init; }
    public AvailabilityFilter Availability { get; init; } = AvailabilityFilter.All;
    public ProductSort Sort { get; init; } = ProductSort.Featured;
}

public sealed record FacetValueDto(string Value, string Label, int Count);

public sealed record ProductFacetsDto(
    IReadOnlyList<FacetValueDto> Categories, IReadOnlyList<FacetValueDto> Conditions,
    IReadOnlyList<FacetValueDto> Eras, IReadOnlyList<FacetValueDto> Materials,
    decimal? MinPrice, decimal? MaxPrice);
