using AntiqueStore.Application.Common;
using AntiqueStore.Application.Inventory;
using AntiqueStore.Domain.Entities;
using AntiqueStore.Domain.Enums;
using Microsoft.EntityFrameworkCore;

namespace AntiqueStore.Application.Admin;

public sealed class AdminProductService(
    IAppDbContext db, IHtmlSanitizer sanitizer, IAuditLogger audit, ICurrentUser currentUser, InventoryService inventory,
    TimeProvider clock)
{
    public async Task<PagedResult<AdminProductListItemDto>> ListAsync(AdminProductQuery q, CancellationToken ct)
    {
        var products = db.Products.AsNoTracking();
        if (!string.IsNullOrWhiteSpace(q.Q))
        {
            var term = q.Q.Trim();
            products = products.Where(p => p.Name.Contains(term) || p.Sku.Contains(term) || p.Slug.Contains(term));
        }
        if (q.Status is { } status) products = products.Where(p => p.Status == status);
        if (q.CategoryId is { } cat) products = products.Where(p => p.CategoryId == cat);
        if (q.CollectionId is { } col) products = products.Where(p => p.Collections.Any(c => c.CollectionId == col));
        if (q.Featured is { } featured) products = products.Where(p => p.IsFeatured == featured);
        products = q.Inventory switch
        {
            InventoryState.Draft => products.Where(p => p.Status == ProductStatus.Draft),
            InventoryState.Archived => products.Where(p => p.Status == ProductStatus.Archived),
            InventoryState.Available => products.Where(p => p.Status == ProductStatus.Published && p.QuantityAvailable > 0),
            InventoryState.Reserved => products.Where(p => p.Status == ProductStatus.Published && p.QuantityAvailable == 0 && p.QuantityReserved > 0),
            InventoryState.Sold => products.Where(p => p.Status == ProductStatus.Published && p.QuantityAvailable == 0 && p.QuantityReserved == 0),
            _ => products
        };
        products = q.Sort switch
        {
            "name" => products.OrderBy(p => p.Name),
            "price_asc" => products.OrderBy(p => p.Price),
            "price_desc" => products.OrderByDescending(p => p.Price),
            "created" => products.OrderByDescending(p => p.CreatedAt),
            _ => products.OrderByDescending(p => p.UpdatedAt)
        };

        var page = await products.ToPagedAsync(q, x => x.Select(p => new
        {
            p.Id, p.Name, p.Sku, p.Slug, p.Price, p.CompareAtPrice, p.Status, p.QuantityAvailable, p.QuantityReserved,
            p.QuantitySold, CategoryName = p.Category != null ? p.Category.Name : null,
            ImageUrl = p.Images.OrderBy(i => i.SortOrder).Select(i => i.Url).FirstOrDefault(), p.IsFeatured, p.UpdatedAt
        }), ct);

        return new PagedResult<AdminProductListItemDto>(page.Items.Select(p => new AdminProductListItemDto(
            p.Id, p.Name, p.Sku, p.Slug, p.Price, p.CompareAtPrice, p.Status,
            Product.GetInventoryState(p.Status, p.QuantityAvailable, p.QuantityReserved),
            p.QuantityAvailable, p.QuantityReserved, p.QuantitySold, p.CategoryName, p.ImageUrl, p.IsFeatured, p.UpdatedAt)).ToList(),
            page.Page, page.PageSize, page.TotalCount);
    }

    public async Task<AdminProductDto> GetAsync(int id, CancellationToken ct)
    {
        var p = await db.Products.AsNoTracking()
            .Include(x => x.Images.OrderBy(i => i.SortOrder))
            .Include(x => x.Attributes.OrderBy(a => a.SortOrder))
            .Include(x => x.Collections)
            .AsSplitQuery()
            .FirstOrDefaultAsync(x => x.Id == id, ct) ?? throw new NotFoundException("Product", id);
        var hasOrders = await db.OrderItems.AnyAsync(i => i.ProductId == id, ct);

        return new AdminProductDto(
            p.Id, p.Name, p.Slug, p.Sku, p.ShortDescription, p.Description, p.Price, p.CompareAtPrice, p.CategoryId,
            p.Collections.Select(c => c.CollectionId).ToList(), p.Condition, p.ConditionNotes, p.SignsOfWear, p.Scratches,
            p.Damage, p.RestorationHistory, p.MissingParts, p.FunctionalStatus, p.Material, p.Color, p.Dimensions,
            p.WeightKg, p.Era, p.ApproximateAge, p.Provenance, p.HasProvenanceEvidence, p.ProvenanceEvidence,
            p.QuantityAvailable, p.QuantityReserved, p.QuantitySold, p.MaxPerOrder, p.Status, p.InventoryState,
            p.IsFeatured, p.IsDeliveryAvailable, p.ShowWhenSold, p.SeoTitle, p.SeoDescription,
            p.Images.Select(i => new AdminProductImageDto(i.Id, i.MediaAssetId, i.Url, i.AltText, i.SortOrder, i.Width, i.Height)).ToList(),
            p.Attributes.Select(a => new ProductAttributeInput(a.Name, a.Value)).ToList(),
            p.Version, p.CreatedAt, p.UpdatedAt, p.PublishedAt, hasOrders);
    }

    public async Task<AdminProductDto> CreateAsync(ProductUpsertRequest r, CancellationToken ct)
    {
        var adminId = currentUser.RequireUserId();
        await EnsureUniqueSkuAsync(r.Sku, null, ct);
        var product = new Product { QuantityAvailable = r.InitialQuantity };
        await ApplyAsync(product, r, ct);
        product.Slug = await UniqueSlugAsync(string.IsNullOrWhiteSpace(r.Slug) ? r.Name : r.Slug, null, ct);

        await using var tx = await db.Database.BeginTransactionAsync(ct);
        db.Products.Add(product);
        await db.SaveChangesAsync(ct);
        if (r.InitialQuantity > 0)
        {
            db.InventoryMovements.Add(new InventoryMovement
            {
                ProductId = product.Id, Type = InventoryMovementType.InitialStock, AvailableChange = r.InitialQuantity,
                UserId = adminId, Reason = "Product created", CreatedAt = clock.GetUtcNow().UtcDateTime
            });
        }
        audit.Log("product.create", "Product", product.Id, $"Created \"{product.Name}\" ({product.Sku})");
        await db.SaveChangesAsync(ct);
        await tx.CommitAsync(ct);
        return await GetAsync(product.Id, ct);
    }

    public async Task<AdminProductDto> UpdateAsync(int id, ProductUpsertRequest r, CancellationToken ct)
    {
        var product = await db.Products
            .Include(p => p.Images).Include(p => p.Attributes).Include(p => p.Collections)
            .AsSplitQuery()
            .FirstOrDefaultAsync(p => p.Id == id, ct) ?? throw new NotFoundException("Product", id);
        if (r.Version is { } v && v != product.Version)
            throw new ConflictException("This product was changed by someone else. Reload to see the latest version.");

        await EnsureUniqueSkuAsync(r.Sku, id, ct);
        await ApplyAsync(product, r, ct);
        var requestedSlug = string.IsNullOrWhiteSpace(r.Slug) ? product.Slug : Text.Slugify(r.Slug);
        if (requestedSlug != product.Slug) product.Slug = await UniqueSlugAsync(requestedSlug, id, ct);

        audit.Log("product.update", "Product", product.Id, $"Updated \"{product.Name}\"");
        try
        {
            await db.SaveChangesAsync(ct);
        }
        catch (DbUpdateConcurrencyException)
        {
            throw new ConflictException("This product was changed by someone else. Reload to see the latest version.");
        }
        return await GetAsync(id, ct);
    }

    /// <summary>Hard-deletes unused products; products with order history are archived to keep records accurate.</summary>
    public async Task<DeleteProductResult> DeleteAsync(int id, CancellationToken ct)
    {
        var product = await db.Products.FirstOrDefaultAsync(p => p.Id == id, ct) ?? throw new NotFoundException("Product", id);
        var referenced = await db.OrderItems.AnyAsync(i => i.ProductId == id, ct) ||
                         await db.InventoryReservations.AnyAsync(r => r.ProductId == id, ct);
        if (referenced)
        {
            product.Status = ProductStatus.Archived;
            product.IsFeatured = false;
            audit.Log("product.archive", "Product", id, $"Archived \"{product.Name}\" (has order history)");
            await db.SaveChangesAsync(ct);
            return new DeleteProductResult(false, true, "This product has order history, so it was archived instead of deleted.");
        }

        await using var tx = await db.Database.BeginTransactionAsync(ct);
        await db.InventoryMovements.Where(m => m.ProductId == id).ExecuteDeleteAsync(ct);
        db.Products.Remove(product);
        audit.Log("product.delete", "Product", id, $"Deleted \"{product.Name}\" ({product.Sku})");
        await db.SaveChangesAsync(ct);
        await tx.CommitAsync(ct);
        return new DeleteProductResult(true, false, "Product deleted.");
    }

    public async Task<AdminProductDto> DuplicateAsync(int id, CancellationToken ct)
    {
        var source = await GetAsync(id, ct);
        var baseSku = source.Sku.Length > 56 ? source.Sku[..56] : source.Sku;
        var sku = $"{baseSku}-COPY";
        for (var i = 2; await db.Products.AnyAsync(p => p.Sku == sku, ct); i++) sku = $"{baseSku}-COPY{i}";

        var copy = new ProductUpsertRequest
        {
            Name = $"{source.Name} (Copy)", Sku = sku, ShortDescription = source.ShortDescription, Description = source.Description,
            Price = source.Price, CompareAtPrice = source.CompareAtPrice, CategoryId = source.CategoryId,
            CollectionIds = source.CollectionIds, Condition = source.Condition, ConditionNotes = source.ConditionNotes,
            SignsOfWear = source.SignsOfWear, Scratches = source.Scratches, Damage = source.Damage,
            RestorationHistory = source.RestorationHistory, MissingParts = source.MissingParts,
            FunctionalStatus = source.FunctionalStatus, Material = source.Material, Color = source.Color,
            Dimensions = source.Dimensions, WeightKg = source.WeightKg, Era = source.Era, ApproximateAge = source.ApproximateAge,
            // Provenance belongs to the specific original piece; it is deliberately not copied.
            InitialQuantity = 1, MaxPerOrder = source.MaxPerOrder, Status = ProductStatus.Draft,
            IsDeliveryAvailable = source.IsDeliveryAvailable, ShowWhenSold = source.ShowWhenSold,
            Images = source.Images.Select(i => new ProductImageInput(i.MediaAssetId, i.Url, i.AltText)).ToList(),
            Attributes = source.Attributes
        };
        return await CreateAsync(copy, ct);
    }

    public async Task<AdminProductDto> SetStatusAsync(int id, ProductStatus status, CancellationToken ct)
    {
        var product = await db.Products.FirstOrDefaultAsync(p => p.Id == id, ct) ?? throw new NotFoundException("Product", id);
        if (status == ProductStatus.Published)
        {
            var hasImage = await db.ProductImages.AnyAsync(i => i.ProductId == id, ct);
            if (!hasImage) throw new BusinessRuleException("Add at least one photo before publishing.", "image_required");
            product.PublishedAt ??= clock.GetUtcNow().UtcDateTime;
        }
        var from = product.Status;
        product.Status = status;
        audit.Log("product.status", "Product", id, $"\"{product.Name}\": {from} → {status}");
        await db.SaveChangesAsync(ct);
        return await GetAsync(id, ct);
    }

    public async Task<AdminProductDto> SetFeaturedAsync(int id, bool featured, CancellationToken ct)
    {
        var product = await db.Products.FirstOrDefaultAsync(p => p.Id == id, ct) ?? throw new NotFoundException("Product", id);
        product.IsFeatured = featured;
        audit.Log("product.featured", "Product", id, $"\"{product.Name}\" featured = {featured}");
        await db.SaveChangesAsync(ct);
        return await GetAsync(id, ct);
    }

    public async Task<AdminProductDto> AdjustStockAsync(int id, AdjustStockRequest r, CancellationToken ct)
    {
        var adminId = currentUser.RequireUserId();
        await using var tx = await db.Database.BeginTransactionAsync(ct);
        var delta = await inventory.AdjustAvailableAsync(id, r.QuantityAvailable, adminId, r.Reason.Trim(), ct);
        audit.Log("inventory.adjust", "Product", id, $"Available stock changed by {delta:+#;-#;0}", new { r.QuantityAvailable, r.Reason });
        await db.SaveChangesAsync(ct);
        await tx.CommitAsync(ct);
        return await GetAsync(id, ct);
    }

    public async Task<AdminProductDto> MarkSoldAsync(int id, MarkSoldRequest r, CancellationToken ct)
    {
        var adminId = currentUser.RequireUserId();
        await using var tx = await db.Database.BeginTransactionAsync(ct);
        var units = await inventory.MarkSoldOfflineAsync(id, adminId, Text.TrimToNull(r.Reason) ?? "Sold outside the website", ct);
        if (units == 0) throw new BusinessRuleException("There is no available stock to mark as sold.", "nothing_available");
        audit.Log("inventory.mark_sold", "Product", id, $"Marked {units} unit(s) as sold", new { r.Reason });
        await db.SaveChangesAsync(ct);
        await tx.CommitAsync(ct);
        return await GetAsync(id, ct);
    }

    public async Task<IReadOnlyList<InventoryMovementDto>> GetMovementsAsync(int productId, CancellationToken ct)
    {
        var rows = await db.InventoryMovements.AsNoTracking().Where(m => m.ProductId == productId)
            .OrderByDescending(m => m.CreatedAt).Take(200)
            .Select(m => new
            {
                m.Id, m.Type, m.AvailableChange, m.ReservedChange, m.SoldChange, m.Reason, m.OrderId, m.UserId, m.CreatedAt,
                OrderNumber = db.Orders.Where(o => o.Id == m.OrderId).Select(o => o.OrderNumber).FirstOrDefault(),
                UserName = db.Users.Where(u => u.Id == m.UserId).Select(u => u.DisplayName ?? u.Email).FirstOrDefault()
            }).ToListAsync(ct);
        return rows.Select(m => new InventoryMovementDto(m.Id, m.Type, m.AvailableChange, m.ReservedChange, m.SoldChange,
            m.Reason, m.OrderId, m.OrderNumber, m.UserName, m.CreatedAt)).ToList();
    }

    public async Task<IReadOnlyList<ActiveReservationDto>> GetActiveReservationsAsync(CancellationToken ct) =>
        await db.InventoryReservations.AsNoTracking().Where(r => r.Status == ReservationStatus.Active)
            .OrderBy(r => r.ExpiresAt)
            .Select(r => new ActiveReservationDto(r.Id, r.ProductId, r.Product!.Name, r.Quantity, r.User!.Email, r.ExpiresAt))
            .ToListAsync(ct);

    private async Task ApplyAsync(Product p, ProductUpsertRequest r, CancellationToken ct)
    {
        if (r.CategoryId is { } catId && !await db.Categories.AnyAsync(c => c.Id == catId, ct))
            throw RequestValidationException.For("categoryId", "Selected category does not exist.");
        var collectionIds = r.CollectionIds.Distinct().ToList();
        if (collectionIds.Count > 0 && await db.Collections.CountAsync(c => collectionIds.Contains(c.Id), ct) != collectionIds.Count)
            throw RequestValidationException.For("collectionIds", "One or more collections do not exist.");

        p.Name = r.Name.Trim();
        p.Sku = r.Sku.Trim().ToUpperInvariant();
        p.ShortDescription = Text.TrimToNull(r.ShortDescription);
        p.Description = string.IsNullOrWhiteSpace(r.Description) ? null : sanitizer.Sanitize(r.Description);
        p.Price = r.Price;
        p.CompareAtPrice = r.CompareAtPrice is > 0 ? r.CompareAtPrice : null;
        p.CategoryId = r.CategoryId;
        p.Condition = r.Condition;
        p.ConditionNotes = Text.TrimToNull(r.ConditionNotes);
        p.SignsOfWear = Text.TrimToNull(r.SignsOfWear);
        p.Scratches = Text.TrimToNull(r.Scratches);
        p.Damage = Text.TrimToNull(r.Damage);
        p.RestorationHistory = Text.TrimToNull(r.RestorationHistory);
        p.MissingParts = Text.TrimToNull(r.MissingParts);
        p.FunctionalStatus = Text.TrimToNull(r.FunctionalStatus);
        p.Material = Text.TrimToNull(r.Material);
        p.Color = Text.TrimToNull(r.Color);
        p.Dimensions = Text.TrimToNull(r.Dimensions);
        p.WeightKg = r.WeightKg;
        p.Era = Text.TrimToNull(r.Era);
        p.ApproximateAge = Text.TrimToNull(r.ApproximateAge);
        p.Provenance = Text.TrimToNull(r.Provenance);
        p.ProvenanceEvidence = Text.TrimToNull(r.ProvenanceEvidence);
        p.HasProvenanceEvidence = r.HasProvenanceEvidence && p.ProvenanceEvidence is not null;
        p.MaxPerOrder = r.MaxPerOrder;
        p.IsFeatured = r.IsFeatured;
        p.IsDeliveryAvailable = r.IsDeliveryAvailable;
        p.ShowWhenSold = r.ShowWhenSold;
        p.SeoTitle = Text.TrimToNull(r.SeoTitle);
        p.SeoDescription = Text.TrimToNull(r.SeoDescription);

        if (r.Status == ProductStatus.Published && r.Images.Count == 0)
            throw RequestValidationException.For("images", "Add at least one photo before publishing.");
        if (r.Status == ProductStatus.Published) p.PublishedAt ??= clock.GetUtcNow().UtcDateTime;
        p.Status = r.Status;

        // Images: resolve media assets server-side so dimensions/URLs can't be spoofed.
        var assetIds = r.Images.Where(i => i.MediaAssetId.HasValue).Select(i => i.MediaAssetId!.Value).Distinct().ToList();
        var assets = await db.MediaAssets.AsNoTracking().Where(a => assetIds.Contains(a.Id)).ToDictionaryAsync(a => a.Id, ct);
        p.Images.Clear();
        var order = 0;
        foreach (var img in r.Images)
        {
            if (img.MediaAssetId is { } assetId)
            {
                if (!assets.TryGetValue(assetId, out var asset))
                    throw RequestValidationException.For("images", "An image could not be found in the media library.");
                p.Images.Add(new ProductImage { MediaAssetId = asset.Id, Url = asset.Url, Width = asset.Width, Height = asset.Height,
                    AltText = Text.TrimToNull(img.AltText) ?? asset.AltText, SortOrder = order++ });
            }
            else if (Text.IsSafeImageUrl(img.Url))
            {
                p.Images.Add(new ProductImage { Url = img.Url!.Trim(), AltText = Text.TrimToNull(img.AltText), SortOrder = order++ });
            }
            else
            {
                throw RequestValidationException.For("images", "Image URLs must be http(s) or site-relative.");
            }
        }

        p.Attributes.Clear();
        var attrOrder = 0;
        foreach (var a in r.Attributes.Where(a => !string.IsNullOrWhiteSpace(a.Name) && !string.IsNullOrWhiteSpace(a.Value)))
            p.Attributes.Add(new ProductAttribute { Name = a.Name.Trim(), Value = a.Value.Trim(), SortOrder = attrOrder++ });

        var existing = p.Collections.Select(c => c.CollectionId).ToHashSet();
        p.Collections.RemoveAll(c => !collectionIds.Contains(c.CollectionId));
        foreach (var cid in collectionIds.Where(c => !existing.Contains(c)))
            p.Collections.Add(new ProductCollection { CollectionId = cid });
    }

    private async Task EnsureUniqueSkuAsync(string sku, int? excludeId, CancellationToken ct)
    {
        var normalized = sku.Trim().ToUpperInvariant();
        if (await db.Products.AnyAsync(p => p.Sku == normalized && p.Id != excludeId, ct))
            throw RequestValidationException.For("sku", "Another product already uses this SKU.");
    }

    private async Task<string> UniqueSlugAsync(string source, int? excludeId, CancellationToken ct)
    {
        var baseSlug = Text.Slugify(source);
        if (baseSlug.Length == 0) baseSlug = "item";
        var slug = baseSlug;
        for (var i = 2; await db.Products.AnyAsync(p => p.Slug == slug && p.Id != excludeId, ct); i++) slug = $"{baseSlug}-{i}";
        return slug;
    }
}
