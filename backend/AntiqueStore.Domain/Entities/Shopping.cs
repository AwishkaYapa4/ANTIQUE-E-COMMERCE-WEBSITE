using AntiqueStore.Domain.Common;

namespace AntiqueStore.Domain.Entities;

public class Cart : Entity
{
    public int UserId { get; set; }
    public User? User { get; set; }
    public List<CartItem> Items { get; set; } = [];
}

public class CartItem : Entity
{
    public int CartId { get; set; }
    public Cart? Cart { get; set; }
    public int ProductId { get; set; }
    public Product? Product { get; set; }
    public int Quantity { get; set; } = 1;
}

public class Wishlist : Entity
{
    public int UserId { get; set; }
    public User? User { get; set; }
    public List<WishlistItem> Items { get; set; } = [];
}

public class WishlistItem : Entity
{
    public int WishlistId { get; set; }
    public Wishlist? Wishlist { get; set; }
    public int ProductId { get; set; }
    public Product? Product { get; set; }
}
