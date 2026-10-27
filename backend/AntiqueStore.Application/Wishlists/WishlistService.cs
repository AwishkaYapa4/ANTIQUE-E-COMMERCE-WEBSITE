using AntiqueStore.Application.Catalog;
using AntiqueStore.Application.Common;
using AntiqueStore.Domain.Entities;
using AntiqueStore.Domain.Enums;
using Microsoft.EntityFrameworkCore;

namespace AntiqueStore.Application.Wishlists;

public sealed record WishlistItemDto(int Id, ProductCardDto Product, DateTime AddedAt);
public sealed record AddWishlistItemRequest(int ProductId);

public sealed class WishlistService(IAppDbContext db)
{
    public async Task<IReadOnlyList<WishlistItemDto>> GetAsync(int userId, CancellationToken ct)
    {
        var items = await db.WishlistItems.AsNoTracking()
            .Where(i => i.Wishlist!.UserId == userId && i.Product!.Status == ProductStatus.Published)
            .OrderByDescending(i => i.CreatedAt)
            .Select(i => new { i.Id, i.ProductId, i.CreatedAt })
            .ToListAsync(ct);
        var ids = items.Select(i => i.ProductId).ToList();
        var cards = (await CatalogService.FetchCardsAsync(db.Products.AsNoTracking().Where(p => ids.Contains(p.Id)), ct))
            .ToDictionary(c => c.Id);
        return items.Where(i => cards.ContainsKey(i.ProductId))
            .Select(i => new WishlistItemDto(i.Id, cards[i.ProductId], i.CreatedAt)).ToList();
    }

    public async Task<IReadOnlyList<WishlistItemDto>> AddAsync(int userId, AddWishlistItemRequest request, CancellationToken ct)
    {
        var exists = await db.Products.AnyAsync(p => p.Id == request.ProductId && p.Status == ProductStatus.Published, ct);
        if (!exists) throw new NotFoundException("Product", request.ProductId);

        var wishlist = await db.Wishlists.Include(w => w.Items).FirstOrDefaultAsync(w => w.UserId == userId, ct);
        if (wishlist is null)
        {
            wishlist = new Wishlist { UserId = userId };
            db.Wishlists.Add(wishlist);
        }
        if (wishlist.Items.All(i => i.ProductId != request.ProductId))
        {
            if (wishlist.Items.Count >= 200) throw new BusinessRuleException("Your wishlist is full.", "wishlist_full");
            wishlist.Items.Add(new WishlistItem { ProductId = request.ProductId });
            await db.SaveChangesAsync(ct);
        }
        return await GetAsync(userId, ct);
    }

    /// <summary>Removes by wishlist item id or product id (the UI often only knows the product).</summary>
    public async Task<IReadOnlyList<WishlistItemDto>> RemoveAsync(int userId, int id, bool byProduct, CancellationToken ct)
    {
        await db.WishlistItems
            .Where(i => i.Wishlist!.UserId == userId && (byProduct ? i.ProductId == id : i.Id == id))
            .ExecuteDeleteAsync(ct);
        return await GetAsync(userId, ct);
    }

    public async Task<IReadOnlyList<WishlistItemDto>> MergeAsync(int userId, IReadOnlyList<int> productIds, CancellationToken ct)
    {
        foreach (var id in productIds.Distinct().Take(100))
        {
            try { await AddAsync(userId, new AddWishlistItemRequest(id), ct); }
            catch (NotFoundException) { /* product unpublished since it was saved locally: skip */ }
        }
        return await GetAsync(userId, ct);
    }
}
