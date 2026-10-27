using AntiqueStore.Application.Common;
using AntiqueStore.Domain.Entities;
using AntiqueStore.Domain.Enums;
using Microsoft.EntityFrameworkCore;

namespace AntiqueStore.Application.Catalog;

/// <summary>Public (customer-facing) catalog queries. Only published products are ever returned.</summary>
public sealed class CatalogService(IAppDbContext db)
{
    public const string Currency = "LKR";

    /// <summary>Products a customer may see: published, and either sellable or flagged to stay visible once sold.</summary>
    public IQueryable<Product> VisibleProducts() =>
        db.Products.AsNoTracking().Where(p => p.Status == ProductStatus.Published &&
                                              (p.ShowWhenSold || p.QuantityAvailable > 0 || p.QuantityReserved > 0));

    public async Task<PagedResult<ProductCardDto>> SearchAsync(ProductQuery query, CancellationToken ct)
    {
        var products = ApplyFilters(VisibleProducts(), query);

        products = query.Sort switch
        {
            ProductSort.PriceAsc => products.OrderBy(p => p.Price).ThenByDescending(p => p.Id),
            ProductSort.PriceDesc => products.OrderByDescending(p => p.Price).ThenByDescending(p => p.Id),
            ProductSort.Newest => products.OrderByDescending(p => p.PublishedAt).ThenByDescending(p => p.Id),
            ProductSort.RecentlyAdded => products.OrderByDescending(p => p.CreatedAt).ThenByDescending(p => p.Id),
            // Featured: sellable first, then featured, then newest.
            _ => products.OrderByDescending(p => p.QuantityAvailable > 0).ThenByDescending(p => p.IsFeatured)
                .ThenByDescending(p => p.PublishedAt).ThenByDescending(p => p.Id)
        };

        return await products.ToPagedAsync(query, FetchCardsAsync, ct);
    }

    public IQueryable<Product> ApplyFilters(IQueryable<Product> products, ProductQuery query)
    {
        if (!string.IsNullOrWhiteSpace(query.Q))
        {
            var term = query.Q.Trim();
            products = products.Where(p => p.Name.Contains(term) || p.Sku == term ||
                                           (p.ShortDescription != null && p.ShortDescription.Contains(term)) ||
                                           (p.Material != null && p.Material.Contains(term)) ||
                                           (p.Era != null && p.Era.Contains(term)) ||
                                           (p.Category != null && p.Category.Name.Contains(term)));
        }

        if (!string.IsNullOrWhiteSpace(query.Category))
        {
            // Include direct children so a parent category page shows its sub-categories' items.
            products = products.Where(p => p.Category != null &&
                                           (p.Category.Slug == query.Category ||
                                            (p.Category.Parent != null && p.Category.Parent.Slug == query.Category)));
        }

        if (!string.IsNullOrWhiteSpace(query.Collection))
            products = products.Where(p => p.Collections.Any(c => c.Collection!.Slug == query.Collection && c.Collection.IsVisible));

        if (query.Ids is { Length: > 0 } ids)
        {
            var limited = ids.Take(100).ToArray();
            products = products.Where(p => limited.Contains(p.Id));
        }
        if (query.MinPrice is { } min) products = products.Where(p => p.Price >= min);
        if (query.MaxPrice is { } max) products = products.Where(p => p.Price <= max);
        if (query.Condition is { Length: > 0 } conditions) products = products.Where(p => conditions.Contains(p.Condition));
        if (query.Era is { Length: > 0 } eras) products = products.Where(p => p.Era != null && eras.Contains(p.Era));
        if (query.Material is { Length: > 0 } materials) products = products.Where(p => p.Material != null && materials.Contains(p.Material));

        products = query.Availability switch
        {
            AvailabilityFilter.Available => products.Where(p => p.QuantityAvailable > 0),
            AvailabilityFilter.Sold => products.Where(p => p.QuantityAvailable == 0 && p.QuantityReserved == 0),
            _ => products
        };
        return products;
    }

    private sealed class CardRow
    {
        public int Id { get; init; }
        public string Slug { get; init; } = "";
        public string Name { get; init; } = "";
        public decimal Price { get; init; }
        public decimal? CompareAtPrice { get; init; }
        public ProductCondition Condition { get; init; }
        public string? Era { get; init; }
        public bool IsFeatured { get; init; }
        public int MaxPerOrder { get; init; }
        public ProductStatus Status { get; init; }
        public int QuantityAvailable { get; init; }
        public int QuantityReserved { get; init; }
        public string? CategoryName { get; init; }
        public string? CategorySlug { get; init; }
    }

    private sealed class ImageRow
    {
        public int ProductId { get; init; }
        public int SortOrder { get; init; }
        public string Url { get; init; } = "";
        public string? AltText { get; init; }
        public int? Width { get; init; }
        public int? Height { get; init; }
        public string? Variants { get; init; }
    }

    /// <summary>
    /// Projects products to cards. Images are loaded in a second query (instead of a per-row TOP-N subquery),
    /// which keeps the SQL portable — no LATERAL/APPLY needed on MySQL or SQLite.
    /// </summary>
    public static async Task<List<ProductCardDto>> FetchCardsAsync(IQueryable<Product> products, CancellationToken ct)
    {
        var rows = await products.Select(p => new CardRow
        {
            Id = p.Id, Slug = p.Slug, Name = p.Name, Price = p.Price, CompareAtPrice = p.CompareAtPrice,
            Condition = p.Condition, Era = p.Era, IsFeatured = p.IsFeatured, MaxPerOrder = p.MaxPerOrder,
            Status = p.Status, QuantityAvailable = p.QuantityAvailable, QuantityReserved = p.QuantityReserved,
            CategoryName = p.Category != null ? p.Category.Name : null,
            CategorySlug = p.Category != null ? p.Category.Slug : null
        }).ToListAsync(ct);
        if (rows.Count == 0) return [];

        var ids = rows.Select(r => r.Id).ToList();
        // Plain JOIN from the same source query (keeps the same DbContext; no LATERAL needed).
        var images = (await products.SelectMany(p => p.Images).Where(i => ids.Contains(i.ProductId))
                .Select(i => new ImageRow
                {
                    ProductId = i.ProductId, SortOrder = i.SortOrder, Url = i.Url, AltText = i.AltText, Width = i.Width,
                    Height = i.Height, Variants = i.MediaAsset != null ? i.MediaAsset.VariantsJson : null
                }).ToListAsync(ct))
            .GroupBy(i => i.ProductId)
            .ToDictionary(g => g.Key, g => g.OrderBy(i => i.SortOrder).Take(2).ToList());

        return rows.Select(p =>
        {
            var imgs = images.GetValueOrDefault(p.Id) ?? [];
            return new ProductCardDto(
                p.Id, p.Slug, p.Name, p.Price, p.CompareAtPrice, Currency, p.CategoryName, p.CategorySlug,
                p.Condition, p.Era,
                imgs.Count > 0 ? ToImage(imgs[0], p.Name) : null,
                imgs.Count > 1 ? ToImage(imgs[1], p.Name) : null,
                Product.GetInventoryState(p.Status, p.QuantityAvailable, p.QuantityReserved), p.IsFeatured, p.MaxPerOrder);
        }).ToList();

        static ImageDto? ToImage(ImageRow i, string name) => ImageDto.From(i.Url, i.AltText ?? name, i.Width, i.Height, i.Variants);
    }

    public async Task<IReadOnlyList<ProductCardDto>> GetCardsAsync(IQueryable<Product> query, int take, CancellationToken ct) =>
        await FetchCardsAsync(query.Take(take), ct);

    public async Task<ProductDetailDto> GetBySlugAsync(string slug, CancellationToken ct)
    {
        var p = await VisibleProducts()
            .Include(x => x.Category)
            .Include(x => x.Images.OrderBy(i => i.SortOrder)).ThenInclude(i => i.MediaAsset)
            .Include(x => x.Attributes.OrderBy(a => a.SortOrder))
            .Include(x => x.Collections).ThenInclude(c => c.Collection)
            .AsSplitQuery()
            .FirstOrDefaultAsync(x => x.Slug == slug, ct) ?? throw new NotFoundException("Product", slug);

        return await ToDetailAsync(p, ct);
    }

    internal async Task<ProductDetailDto> ToDetailAsync(Product p, CancellationToken ct)
    {
        var reviewStats = await db.Reviews.AsNoTracking()
            .Where(r => r.ProductId == p.Id && r.Status == ReviewStatus.Approved)
            .GroupBy(r => r.ProductId)
            .Select(g => new { Count = g.Count(), Avg = g.Average(r => (double)r.Rating) })
            .FirstOrDefaultAsync(ct);

        return new ProductDetailDto(
            p.Id, p.Slug, p.Name, p.Sku, p.Price, p.CompareAtPrice, Currency, p.ShortDescription, p.Description,
            p.Category is null ? null : new CategoryRefDto(p.Category.Id, p.Category.Name, p.Category.Slug),
            p.Collections.Where(c => c.Collection is { IsVisible: true })
                .Select(c => new CategoryRefDto(c.Collection!.Id, c.Collection.Name, c.Collection.Slug)).ToList(),
            new ConditionReportDto(p.Condition, p.ConditionNotes, p.SignsOfWear, p.Scratches, p.Damage,
                p.RestorationHistory, p.MissingParts, p.FunctionalStatus),
            p.Material, p.Color, p.Dimensions, p.WeightKg, p.Era, p.ApproximateAge, p.Provenance,
            // Never imply documented provenance without stored evidence.
            p.HasProvenanceEvidence && !string.IsNullOrWhiteSpace(p.ProvenanceEvidence),
            p.HasProvenanceEvidence ? p.ProvenanceEvidence : null,
            p.InventoryState, p.MaxPerOrder, p.IsDeliveryAvailable,
            p.Images.Select(i => ImageDto.From(i.Url, i.AltText ?? p.Name, i.Width, i.Height, i.MediaAsset?.VariantsJson)!).ToList(),
            p.Attributes.Select(a => new ProductAttributeDto(a.Name, a.Value)).ToList(),
            p.SeoTitle, p.SeoDescription, p.PublishedAt,
            new ReviewSummaryDto(reviewStats?.Count ?? 0, reviewStats is null ? null : Math.Round(reviewStats.Avg, 1)));
    }

    public async Task<IReadOnlyList<ProductCardDto>> GetRelatedAsync(string slug, int take, CancellationToken ct)
    {
        var product = await db.Products.AsNoTracking().Where(p => p.Slug == slug)
            .Select(p => new { p.Id, p.CategoryId, p.Era }).FirstOrDefaultAsync(ct) ?? throw new NotFoundException("Product", slug);

        var related = VisibleProducts().Where(p => p.Id != product.Id && p.QuantityAvailable > 0 &&
                                                   (p.CategoryId == product.CategoryId || (product.Era != null && p.Era == product.Era)))
            .OrderByDescending(p => p.CategoryId == product.CategoryId).ThenByDescending(p => p.PublishedAt);
        return await GetCardsAsync(related, take, ct);
    }

    public async Task<ProductFacetsDto> GetFacetsAsync(string? categorySlug, CancellationToken ct)
    {
        var baseQuery = ApplyFilters(VisibleProducts(), new ProductQuery { Category = categorySlug });

        var categories = await VisibleProducts().Where(p => p.Category != null && p.Category.IsVisible)
            .GroupBy(p => new { p.Category!.Slug, p.Category.Name })
            .Select(g => new FacetValueDto(g.Key.Slug, g.Key.Name, g.Count()))
            .ToListAsync(ct);
        var conditions = await baseQuery.GroupBy(p => p.Condition).Select(g => new { g.Key, Count = g.Count() }).ToListAsync(ct);
        var eras = await baseQuery.Where(p => p.Era != null).GroupBy(p => p.Era!)
            .Select(g => new FacetValueDto(g.Key, g.Key, g.Count())).ToListAsync(ct);
        var materials = await baseQuery.Where(p => p.Material != null).GroupBy(p => p.Material!)
            .Select(g => new FacetValueDto(g.Key, g.Key, g.Count())).ToListAsync(ct);
        var minPrice = await baseQuery.MinAsync(p => (decimal?)p.Price, ct);
        var maxPrice = await baseQuery.MaxAsync(p => (decimal?)p.Price, ct);

        return new ProductFacetsDto(
            categories.OrderBy(c => c.Label).ToList(),
            conditions.OrderBy(c => c.Key).Select(c => new FacetValueDto(c.Key.ToString(), ConditionLabel(c.Key), c.Count)).ToList(),
            eras.OrderBy(e => e.Label).ToList(),
            materials.OrderBy(m => m.Label).ToList(),
            minPrice, maxPrice);
    }

    public static string ConditionLabel(ProductCondition c) => c switch
    {
        ProductCondition.VeryGood => "Very Good",
        ProductCondition.ForRestoration => "For Restoration",
        _ => c.ToString()
    };

    public async Task<IReadOnlyList<CategoryDto>> GetCategoriesAsync(bool includeHidden, CancellationToken ct)
    {
        var visible = VisibleProducts();
        return await db.Categories.AsNoTracking()
            .Where(c => includeHidden || c.IsVisible)
            .OrderBy(c => c.SortOrder).ThenBy(c => c.Name)
            .Select(c => new CategoryDto(c.Id, c.Name, c.Slug, c.Description, c.ImageUrl, c.ParentId, c.SortOrder,
                c.IsVisible, c.IsFeatured, visible.Count(p => p.CategoryId == c.Id), c.SeoTitle, c.SeoDescription))
            .ToListAsync(ct);
    }

    public async Task<CategoryDto> GetCategoryAsync(string slug, CancellationToken ct) =>
        (await GetCategoriesAsync(false, ct)).FirstOrDefault(c => c.Slug == slug) ?? throw new NotFoundException("Category", slug);

    public async Task<IReadOnlyList<CollectionDto>> GetCollectionsAsync(bool includeHidden, CancellationToken ct)
    {
        var visible = VisibleProducts();
        return await db.Collections.AsNoTracking()
            .Where(c => includeHidden || c.IsVisible)
            .OrderBy(c => c.SortOrder).ThenBy(c => c.Name)
            .Select(c => new CollectionDto(c.Id, c.Name, c.Slug, c.Description, c.ImageUrl, c.SortOrder, c.IsVisible,
                visible.Count(p => p.Collections.Any(pc => pc.CollectionId == c.Id)), c.SeoTitle, c.SeoDescription))
            .ToListAsync(ct);
    }

    public async Task<CollectionDto> GetCollectionAsync(string slug, CancellationToken ct) =>
        (await GetCollectionsAsync(false, ct)).FirstOrDefault(c => c.Slug == slug) ?? throw new NotFoundException("Collection", slug);
}
