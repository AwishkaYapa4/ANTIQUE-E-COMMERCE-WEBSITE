using AntiqueStore.Application.Catalog;
using AntiqueStore.Application.Common;
using AntiqueStore.Domain.Entities;
using AntiqueStore.Domain.Enums;
using Microsoft.EntityFrameworkCore;

namespace AntiqueStore.Application.Carts;

public sealed record CartLineDto(
    int Id, int ProductId, string Slug, string Name, string Sku, ImageDto? Image, decimal UnitPrice,
    decimal? CompareAtPrice, int Quantity, int MaxQuantity, decimal LineTotal, InventoryState InventoryState,
    bool IsPurchasable, string? Issue);

public sealed record CartDto(IReadOnlyList<CartLineDto> Items, decimal Subtotal, int ItemCount, bool HasIssues, string Currency);

public sealed record AddCartItemRequest(int ProductId, int Quantity = 1);
public sealed record UpdateCartItemRequest(int Quantity);
public sealed record MergeCartRequest(IReadOnlyList<AddCartItemRequest> Items);
public sealed record MergeCartResult(CartDto Cart, IReadOnlyList<string> Skipped);

/// <summary>
/// Server-stored cart for signed-in customers. Prices and availability shown here are always read
/// fresh from the database; the client's copy is never trusted.
/// </summary>
public sealed class CartService(IAppDbContext db)
{
    public async Task<CartDto> GetAsync(int userId, CancellationToken ct)
    {
        var rows = await db.CartItems.AsNoTracking()
            .Where(i => i.Cart!.UserId == userId)
            .OrderBy(i => i.CreatedAt)
            .Select(i => new
            {
                i.Id, i.Quantity, i.ProductId,
                i.Product!.Slug, i.Product.Name, i.Product.Sku, i.Product.Price, i.Product.CompareAtPrice, i.Product.Status,
                i.Product.QuantityAvailable, i.Product.QuantityReserved, i.Product.MaxPerOrder,
                Image = i.Product.Images.OrderBy(x => x.SortOrder)
                    .Select(x => new { x.Url, x.AltText, x.Width, x.Height, Variants = x.MediaAsset != null ? x.MediaAsset.VariantsJson : null })
                    .FirstOrDefault(),
                // Units this user already holds in an active checkout reservation still count as theirs.
                HeldByUser = db.InventoryReservations
                    .Where(r => r.UserId == userId && r.ProductId == i.ProductId && r.Status == ReservationStatus.Active)
                    .Sum(r => (int?)r.Quantity) ?? 0
            })
            .ToListAsync(ct);

        var lines = rows.Select(r =>
        {
            var state = Product.GetInventoryState(r.Status, r.QuantityAvailable, r.QuantityReserved);
            var obtainable = r.Status == ProductStatus.Published ? r.QuantityAvailable + r.HeldByUser : 0;
            var max = Math.Max(0, Math.Min(r.MaxPerOrder, obtainable));
            string? issue = null;
            if (r.Status != ProductStatus.Published) issue = "This item is no longer available.";
            else if (obtainable == 0) issue = state == InventoryState.Reserved
                ? "This item is reserved by another customer at checkout."
                : "Sorry, this item has been sold.";
            else if (r.Quantity > obtainable) issue = $"Only {obtainable} available.";
            var image = r.Image is null ? null : ImageDto.From(r.Image.Url, r.Image.AltText ?? r.Name, r.Image.Width, r.Image.Height, r.Image.Variants);
            return new CartLineDto(r.Id, r.ProductId, r.Slug, r.Name, r.Sku, image, r.Price, r.CompareAtPrice, r.Quantity, max,
                r.Price * r.Quantity, state, issue is null, issue);
        }).ToList();

        var purchasable = lines.Where(l => l.IsPurchasable).ToList();
        return new CartDto(lines, purchasable.Sum(l => l.LineTotal), lines.Sum(l => l.Quantity), lines.Any(l => !l.IsPurchasable),
            CatalogService.Currency);
    }

    public async Task<CartDto> AddAsync(int userId, AddCartItemRequest request, CancellationToken ct)
    {
        await AddInternalAsync(userId, request.ProductId, request.Quantity, ct, throwOnError: true);
        await db.SaveChangesAsync(ct);
        return await GetAsync(userId, ct);
    }

    public async Task<CartDto> UpdateAsync(int userId, int itemId, UpdateCartItemRequest request, CancellationToken ct)
    {
        var item = await db.CartItems.Include(i => i.Product)
            .FirstOrDefaultAsync(i => i.Id == itemId && i.Cart!.UserId == userId, ct) ?? throw new NotFoundException("Cart item", itemId);
        if (request.Quantity <= 0)
        {
            db.CartItems.Remove(item);
        }
        else
        {
            var product = item.Product!;
            if (request.Quantity > product.MaxPerOrder)
                throw new BusinessRuleException($"You can buy at most {product.MaxPerOrder} of this item.", "quantity_limit");
            if (request.Quantity > product.QuantityAvailable + await HeldByUserAsync(userId, product.Id, ct))
                throw new BusinessRuleException("The requested quantity is not available.", "insufficient_stock");
            item.Quantity = request.Quantity;
        }
        await db.SaveChangesAsync(ct);
        return await GetAsync(userId, ct);
    }

    public async Task<CartDto> RemoveAsync(int userId, int itemId, CancellationToken ct)
    {
        var item = await db.CartItems.FirstOrDefaultAsync(i => i.Id == itemId && i.Cart!.UserId == userId, ct)
                   ?? throw new NotFoundException("Cart item", itemId);
        db.CartItems.Remove(item);
        await db.SaveChangesAsync(ct);
        return await GetAsync(userId, ct);
    }

    /// <summary>Merges a guest (browser) cart after sign-in. Unavailable items are skipped, never forced in.</summary>
    public async Task<MergeCartResult> MergeAsync(int userId, MergeCartRequest request, CancellationToken ct)
    {
        var skipped = new List<string>();
        foreach (var line in request.Items.DistinctBy(i => i.ProductId).Take(50))
        {
            var error = await AddInternalAsync(userId, line.ProductId, line.Quantity, ct, throwOnError: false, mergeMode: true);
            if (error is not null) skipped.Add(error);
        }
        await db.SaveChangesAsync(ct);
        return new MergeCartResult(await GetAsync(userId, ct), skipped);
    }

    public async Task ClearAsync(int userId, CancellationToken ct) =>
        await db.CartItems.Where(i => i.Cart!.UserId == userId).ExecuteDeleteAsync(ct);

    private async Task<int> HeldByUserAsync(int userId, int productId, CancellationToken ct) =>
        await db.InventoryReservations
            .Where(r => r.UserId == userId && r.ProductId == productId && r.Status == ReservationStatus.Active)
            .SumAsync(r => (int?)r.Quantity, ct) ?? 0;

    private async Task<string?> AddInternalAsync(int userId, int productId, int quantity, CancellationToken ct,
        bool throwOnError, bool mergeMode = false)
    {
        string? Fail(string message, string code)
        {
            if (throwOnError) throw new BusinessRuleException(message, code);
            return message;
        }

        if (quantity <= 0) return Fail("Quantity must be at least 1.", "invalid_quantity");
        var product = await db.Products.AsNoTracking().FirstOrDefaultAsync(p => p.Id == productId, ct);
        if (product is null || product.Status != ProductStatus.Published)
            return Fail("This item is no longer available.", "unavailable");

        var cart = await db.Carts.Include(c => c.Items).FirstOrDefaultAsync(c => c.UserId == userId, ct);
        if (cart is null)
        {
            cart = new Domain.Entities.Cart { UserId = userId };
            db.Carts.Add(cart);
        }

        var existing = cart.Items.FirstOrDefault(i => i.ProductId == productId);
        var desired = mergeMode ? Math.Max(existing?.Quantity ?? 0, quantity) : (existing?.Quantity ?? 0) + quantity;
        var obtainable = product.QuantityAvailable + await HeldByUserAsync(userId, productId, ct);

        if (obtainable <= 0)
            return Fail(product.QuantityReserved > 0
                ? $"\"{product.Name}\" is currently reserved by another customer."
                : $"\"{product.Name}\" has already been sold.", "sold");
        if (desired > product.MaxPerOrder)
        {
            if (existing is not null && product.MaxPerOrder == 1) return Fail($"\"{product.Name}\" is already in your cart.", "already_in_cart");
            return Fail($"You can buy at most {product.MaxPerOrder} of \"{product.Name}\".", "quantity_limit");
        }
        if (desired > obtainable) return Fail($"Only {obtainable} of \"{product.Name}\" available.", "insufficient_stock");

        if (existing is null) cart.Items.Add(new CartItem { ProductId = productId, Quantity = desired });
        else existing.Quantity = desired;
        return null;
    }
}
