using AntiqueStore.Domain.Common;
using AntiqueStore.Domain.Enums;

namespace AntiqueStore.Domain.Entities;

public class Category : Entity
{
    public string Name { get; set; } = "";
    public string Slug { get; set; } = "";
    public string? Description { get; set; }
    public string? ImageUrl { get; set; }
    public int? ParentId { get; set; }
    public Category? Parent { get; set; }
    public List<Category> Children { get; set; } = [];
    public int SortOrder { get; set; }
    public bool IsVisible { get; set; } = true;
    public bool IsFeatured { get; set; }
    public string? SeoTitle { get; set; }
    public string? SeoDescription { get; set; }
}

public class Collection : Entity
{
    public string Name { get; set; } = "";
    public string Slug { get; set; } = "";
    public string? Description { get; set; }
    public string? ImageUrl { get; set; }
    public int SortOrder { get; set; }
    public bool IsVisible { get; set; } = true;
    public string? SeoTitle { get; set; }
    public string? SeoDescription { get; set; }
    public List<ProductCollection> Products { get; set; } = [];
}

public class Product : Entity
{
    public string Name { get; set; } = "";
    public string Slug { get; set; } = "";
    public string Sku { get; set; } = "";
    public string? ShortDescription { get; set; }
    /// <summary>Sanitized HTML.</summary>
    public string? Description { get; set; }
    public decimal Price { get; set; }
    public decimal? CompareAtPrice { get; set; }

    public int? CategoryId { get; set; }
    public Category? Category { get; set; }

    // Antique details
    public ProductCondition Condition { get; set; } = ProductCondition.Good;
    public string? ConditionNotes { get; set; }
    public string? SignsOfWear { get; set; }
    public string? Scratches { get; set; }
    public string? Damage { get; set; }
    public string? RestorationHistory { get; set; }
    public string? MissingParts { get; set; }
    public string? FunctionalStatus { get; set; }
    public string? Material { get; set; }
    public string? Color { get; set; }
    public string? Dimensions { get; set; }
    public decimal? WeightKg { get; set; }
    public string? Era { get; set; }
    public string? ApproximateAge { get; set; }
    public string? Provenance { get; set; }

    /// <summary>
    /// Only true when the store holds supporting documentation (described in
    /// <see cref="ProvenanceEvidence"/>). The storefront never implies authentication otherwise.
    /// </summary>
    public bool HasProvenanceEvidence { get; set; }
    public string? ProvenanceEvidence { get; set; }

    // Inventory counters (modified only through atomic conditional updates)
    public int QuantityAvailable { get; set; }
    public int QuantityReserved { get; set; }
    public int QuantitySold { get; set; }
    public int MaxPerOrder { get; set; } = 1;

    public ProductStatus Status { get; set; } = ProductStatus.Draft;
    public bool IsFeatured { get; set; }
    public bool IsDeliveryAvailable { get; set; } = true;
    public bool ShowWhenSold { get; set; } = true;
    public string? SeoTitle { get; set; }
    public string? SeoDescription { get; set; }
    public DateTime? PublishedAt { get; set; }
    public DateTime? SoldAt { get; set; }

    /// <summary>Optimistic concurrency token for admin edits.</summary>
    public int Version { get; set; }

    public List<ProductImage> Images { get; set; } = [];
    public List<ProductCollection> Collections { get; set; } = [];
    public List<ProductAttribute> Attributes { get; set; } = [];

    public InventoryState InventoryState => GetInventoryState(Status, QuantityAvailable, QuantityReserved);

    public static InventoryState GetInventoryState(ProductStatus status, int available, int reserved) => status switch
    {
        ProductStatus.Draft => InventoryState.Draft,
        ProductStatus.Archived => InventoryState.Archived,
        _ when available > 0 => InventoryState.Available,
        _ when reserved > 0 => InventoryState.Reserved,
        _ => InventoryState.Sold
    };
}

public class ProductImage : Entity
{
    public int ProductId { get; set; }
    public Product? Product { get; set; }
    public int? MediaAssetId { get; set; }
    public MediaAsset? MediaAsset { get; set; }
    public string Url { get; set; } = "";
    public string? AltText { get; set; }
    public int SortOrder { get; set; }
    public int? Width { get; set; }
    public int? Height { get; set; }
}

public class ProductCollection
{
    public int ProductId { get; set; }
    public Product? Product { get; set; }
    public int CollectionId { get; set; }
    public Collection? Collection { get; set; }
    public int SortOrder { get; set; }
}

/// <summary>Free-form extra attribute such as "Maker" or "Origin".</summary>
public class ProductAttribute : Entity
{
    public int ProductId { get; set; }
    public Product? Product { get; set; }
    public string Name { get; set; } = "";
    public string Value { get; set; } = "";
    public int SortOrder { get; set; }
}
