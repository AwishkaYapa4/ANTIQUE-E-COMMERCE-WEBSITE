using AntiqueStore.Application.Catalog;
using AntiqueStore.Application.Common;
using AntiqueStore.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace AntiqueStore.Application.Admin;

/// <summary>Admin management of categories and collections.</summary>
public sealed class AdminTaxonomyService(IAppDbContext db, CatalogService catalog, IAuditLogger audit)
{
    public Task<IReadOnlyList<CategoryDto>> ListCategoriesAsync(CancellationToken ct) => catalog.GetCategoriesAsync(true, ct);

    public async Task<CategoryDto> CreateCategoryAsync(CategoryUpsertRequest r, CancellationToken ct)
    {
        var category = new Category();
        await ApplyAsync(category, r, ct);
        db.Categories.Add(category);
        await db.SaveChangesAsync(ct);
        audit.Log("category.create", "Category", category.Id, $"Created category \"{category.Name}\"");
        await db.SaveChangesAsync(ct);
        return await GetCategoryAsync(category.Id, ct);
    }

    public async Task<CategoryDto> UpdateCategoryAsync(int id, CategoryUpsertRequest r, CancellationToken ct)
    {
        var category = await db.Categories.FirstOrDefaultAsync(c => c.Id == id, ct) ?? throw new NotFoundException("Category", id);
        await ApplyAsync(category, r, ct);
        audit.Log("category.update", "Category", id, $"Updated category \"{category.Name}\"");
        await db.SaveChangesAsync(ct);
        return await GetCategoryAsync(id, ct);
    }

    /// <summary>Refuses to delete categories that still hold products or sub-categories, so no product is orphaned silently.</summary>
    public async Task DeleteCategoryAsync(int id, CancellationToken ct)
    {
        var category = await db.Categories.FirstOrDefaultAsync(c => c.Id == id, ct) ?? throw new NotFoundException("Category", id);
        var productCount = await db.Products.CountAsync(p => p.CategoryId == id, ct);
        if (productCount > 0)
            throw new BusinessRuleException(
                $"This category contains {productCount} product(s). Move them to another category or hide the category instead.",
                "category_in_use");
        if (await db.Categories.AnyAsync(c => c.ParentId == id, ct))
            throw new BusinessRuleException("This category has sub-categories. Move or delete them first.", "category_has_children");
        db.Categories.Remove(category);
        audit.Log("category.delete", "Category", id, $"Deleted category \"{category.Name}\"");
        await db.SaveChangesAsync(ct);
    }

    public async Task ReorderCategoriesAsync(ReorderRequest r, CancellationToken ct)
    {
        var categories = await db.Categories.Where(c => r.Ids.Contains(c.Id)).ToListAsync(ct);
        for (var i = 0; i < r.Ids.Count; i++)
            if (categories.FirstOrDefault(c => c.Id == r.Ids[i]) is { } c) c.SortOrder = i;
        audit.Log("category.reorder", "Category", null, "Reordered categories");
        await db.SaveChangesAsync(ct);
    }

    private async Task<CategoryDto> GetCategoryAsync(int id, CancellationToken ct) =>
        (await catalog.GetCategoriesAsync(true, ct)).First(c => c.Id == id);

    private async Task ApplyAsync(Category c, CategoryUpsertRequest r, CancellationToken ct)
    {
        if (r.ParentId is { } parentId)
        {
            if (parentId == c.Id) throw RequestValidationException.For("parentId", "A category cannot be its own parent.");
            var parent = await db.Categories.AsNoTracking().FirstOrDefaultAsync(x => x.Id == parentId, ct)
                         ?? throw RequestValidationException.For("parentId", "Parent category does not exist.");
            if (parent.ParentId is not null)
                throw RequestValidationException.For("parentId", "Only one level of sub-categories is supported.");
        }
        c.Name = r.Name.Trim();
        c.Slug = await UniqueSlugAsync(db.Categories, string.IsNullOrWhiteSpace(r.Slug) ? r.Name : r.Slug, c.Id, ct);
        c.Description = Text.TrimToNull(r.Description);
        c.ImageUrl = Text.TrimToNull(r.ImageUrl);
        c.ParentId = r.ParentId;
        c.SortOrder = r.SortOrder;
        c.IsVisible = r.IsVisible;
        c.IsFeatured = r.IsFeatured;
        c.SeoTitle = Text.TrimToNull(r.SeoTitle);
        c.SeoDescription = Text.TrimToNull(r.SeoDescription);
    }

    public Task<IReadOnlyList<CollectionDto>> ListCollectionsAsync(CancellationToken ct) => catalog.GetCollectionsAsync(true, ct);

    public async Task<CollectionDto> CreateCollectionAsync(CollectionUpsertRequest r, CancellationToken ct)
    {
        var collection = new Collection();
        await ApplyAsync(collection, r, ct);
        db.Collections.Add(collection);
        await db.SaveChangesAsync(ct);
        audit.Log("collection.create", "Collection", collection.Id, $"Created collection \"{collection.Name}\"");
        await db.SaveChangesAsync(ct);
        return (await catalog.GetCollectionsAsync(true, ct)).First(c => c.Id == collection.Id);
    }

    public async Task<CollectionDto> UpdateCollectionAsync(int id, CollectionUpsertRequest r, CancellationToken ct)
    {
        var collection = await db.Collections.FirstOrDefaultAsync(c => c.Id == id, ct) ?? throw new NotFoundException("Collection", id);
        await ApplyAsync(collection, r, ct);
        audit.Log("collection.update", "Collection", id, $"Updated collection \"{collection.Name}\"");
        await db.SaveChangesAsync(ct);
        return (await catalog.GetCollectionsAsync(true, ct)).First(c => c.Id == id);
    }

    /// <summary>Deleting a collection only removes the grouping; products themselves are untouched.</summary>
    public async Task DeleteCollectionAsync(int id, CancellationToken ct)
    {
        var collection = await db.Collections.FirstOrDefaultAsync(c => c.Id == id, ct) ?? throw new NotFoundException("Collection", id);
        db.Collections.Remove(collection);
        audit.Log("collection.delete", "Collection", id, $"Deleted collection \"{collection.Name}\"");
        await db.SaveChangesAsync(ct);
    }

    private async Task ApplyAsync(Collection c, CollectionUpsertRequest r, CancellationToken ct)
    {
        c.Name = r.Name.Trim();
        c.Slug = await UniqueSlugAsync(db.Collections, string.IsNullOrWhiteSpace(r.Slug) ? r.Name : r.Slug, c.Id, ct);
        c.Description = Text.TrimToNull(r.Description);
        c.ImageUrl = Text.TrimToNull(r.ImageUrl);
        c.SortOrder = r.SortOrder;
        c.IsVisible = r.IsVisible;
        c.SeoTitle = Text.TrimToNull(r.SeoTitle);
        c.SeoDescription = Text.TrimToNull(r.SeoDescription);
    }

    private static async Task<string> UniqueSlugAsync<T>(DbSet<T> set, string source, int excludeId, CancellationToken ct)
        where T : Domain.Common.Entity
    {
        var baseSlug = Text.Slugify(source);
        if (baseSlug.Length == 0) baseSlug = "group";
        var slug = baseSlug;
        for (var i = 2; await set.AnyAsync(e => EF.Property<string>(e, "Slug") == slug && e.Id != excludeId, ct); i++)
            slug = $"{baseSlug}-{i}";
        return slug;
    }
}
