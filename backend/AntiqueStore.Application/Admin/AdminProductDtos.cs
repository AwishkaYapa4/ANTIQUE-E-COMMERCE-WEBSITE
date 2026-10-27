using AntiqueStore.Application.Common;
using AntiqueStore.Domain.Enums;

namespace AntiqueStore.Application.Admin;

public sealed record AdminProductQuery : PageRequest
{
    public string? Q { get; init; }
    public ProductStatus? Status { get; init; }
    public InventoryState? Inventory { get; init; }
    public int? CategoryId { get; init; }
    public int? CollectionId { get; init; }
    public bool? Featured { get; init; }
    public string Sort { get; init; } = "updated";
}

public sealed record AdminProductListItemDto(
    int Id, string Name, string Sku, string Slug, decimal Price, decimal? CompareAtPrice, ProductStatus Status,
    InventoryState InventoryState, int QuantityAvailable, int QuantityReserved, int QuantitySold, string? CategoryName,
    string? ImageUrl, bool IsFeatured, DateTime UpdatedAt);

public sealed record ProductImageInput(int? MediaAssetId, string? Url, string? AltText);

public sealed record ProductAttributeInput(string Name, string Value);

public sealed record ProductUpsertRequest
{
    public string Name { get; init; } = "";
    public string? Slug { get; init; }
    public string Sku { get; init; } = "";
    public string? ShortDescription { get; init; }
    public string? Description { get; init; }
    public decimal Price { get; init; }
    public decimal? CompareAtPrice { get; init; }
    public int? CategoryId { get; init; }
    public IReadOnlyList<int> CollectionIds { get; init; } = [];
    public ProductCondition Condition { get; init; } = ProductCondition.Good;
    public string? ConditionNotes { get; init; }
    public string? SignsOfWear { get; init; }
    public string? Scratches { get; init; }
    public string? Damage { get; init; }
    public string? RestorationHistory { get; init; }
    public string? MissingParts { get; init; }
    public string? FunctionalStatus { get; init; }
    public string? Material { get; init; }
    public string? Color { get; init; }
    public string? Dimensions { get; init; }
    public decimal? WeightKg { get; init; }
    public string? Era { get; init; }
    public string? ApproximateAge { get; init; }
    public string? Provenance { get; init; }
    public bool HasProvenanceEvidence { get; init; }
    public string? ProvenanceEvidence { get; init; }
    /// <summary>Only used when creating. Later changes go through inventory adjustments (audited).</summary>
    public int InitialQuantity { get; init; } = 1;
    public int MaxPerOrder { get; init; } = 1;
    public ProductStatus Status { get; init; } = ProductStatus.Draft;
    public bool IsFeatured { get; init; }
    public bool IsDeliveryAvailable { get; init; } = true;
    public bool ShowWhenSold { get; init; } = true;
    public string? SeoTitle { get; init; }
    public string? SeoDescription { get; init; }
    public IReadOnlyList<ProductImageInput> Images { get; init; } = [];
    public IReadOnlyList<ProductAttributeInput> Attributes { get; init; } = [];
    /// <summary>Optimistic concurrency: the version the admin loaded.</summary>
    public int? Version { get; init; }
}

public sealed record AdminProductImageDto(int Id, int? MediaAssetId, string Url, string? AltText, int SortOrder, int? Width, int? Height);

public sealed record AdminProductDto(
    int Id, string Name, string Slug, string Sku, string? ShortDescription, string? Description, decimal Price,
    decimal? CompareAtPrice, int? CategoryId, IReadOnlyList<int> CollectionIds, ProductCondition Condition,
    string? ConditionNotes, string? SignsOfWear, string? Scratches, string? Damage, string? RestorationHistory,
    string? MissingParts, string? FunctionalStatus, string? Material, string? Color, string? Dimensions, decimal? WeightKg,
    string? Era, string? ApproximateAge, string? Provenance, bool HasProvenanceEvidence, string? ProvenanceEvidence,
    int QuantityAvailable, int QuantityReserved, int QuantitySold, int MaxPerOrder, ProductStatus Status,
    InventoryState InventoryState, bool IsFeatured, bool IsDeliveryAvailable, bool ShowWhenSold, string? SeoTitle,
    string? SeoDescription, IReadOnlyList<AdminProductImageDto> Images, IReadOnlyList<ProductAttributeInput> Attributes,
    int Version, DateTime CreatedAt, DateTime UpdatedAt, DateTime? PublishedAt, bool HasOrders);

public sealed record SetProductStatusRequest(ProductStatus Status);
public sealed record SetFeaturedRequest(bool IsFeatured);
public sealed record DeleteProductResult(bool Deleted, bool Archived, string Message);

public sealed record CategoryUpsertRequest(
    string Name, string? Slug, string? Description, string? ImageUrl, int? ParentId, int SortOrder, bool IsVisible,
    bool IsFeatured, string? SeoTitle, string? SeoDescription);

public sealed record CollectionUpsertRequest(
    string Name, string? Slug, string? Description, string? ImageUrl, int SortOrder, bool IsVisible,
    string? SeoTitle, string? SeoDescription);

public sealed record ReorderRequest(IReadOnlyList<int> Ids);

public sealed record AdjustStockRequest(int QuantityAvailable, string Reason);
public sealed record MarkSoldRequest(string? Reason);

public sealed record InventoryMovementDto(
    long Id, InventoryMovementType Type, int AvailableChange, int ReservedChange, int SoldChange, string? Reason,
    int? OrderId, string? OrderNumber, string? UserName, DateTime CreatedAt);

public sealed record ActiveReservationDto(int Id, int ProductId, string ProductName, int Quantity, string CustomerEmail, DateTime ExpiresAt);
