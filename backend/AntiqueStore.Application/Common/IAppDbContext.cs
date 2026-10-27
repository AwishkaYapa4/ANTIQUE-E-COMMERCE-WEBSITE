using AntiqueStore.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;

namespace AntiqueStore.Application.Common;

public interface IAppDbContext
{
    DbSet<User> Users { get; }
    DbSet<UserAddress> UserAddresses { get; }
    DbSet<Category> Categories { get; }
    DbSet<Collection> Collections { get; }
    DbSet<Product> Products { get; }
    DbSet<ProductImage> ProductImages { get; }
    DbSet<ProductCollection> ProductCollections { get; }
    DbSet<ProductAttribute> ProductAttributes { get; }
    DbSet<InventoryReservation> InventoryReservations { get; }
    DbSet<InventoryMovement> InventoryMovements { get; }
    DbSet<Cart> Carts { get; }
    DbSet<CartItem> CartItems { get; }
    DbSet<Wishlist> Wishlists { get; }
    DbSet<WishlistItem> WishlistItems { get; }
    DbSet<Order> Orders { get; }
    DbSet<OrderItem> OrderItems { get; }
    DbSet<OrderStatusHistory> OrderStatusHistory { get; }
    DbSet<Payment> Payments { get; }
    DbSet<PaymentProof> PaymentProofs { get; }
    DbSet<Review> Reviews { get; }
    DbSet<Enquiry> Enquiries { get; }
    DbSet<Testimonial> Testimonials { get; }
    DbSet<NewsletterSubscriber> NewsletterSubscribers { get; }
    DbSet<DiscountCode> DiscountCodes { get; }
    DbSet<DiscountUsage> DiscountUsages { get; }
    DbSet<ShippingZone> ShippingZones { get; }
    DbSet<ShippingZoneDistrict> ShippingZoneDistricts { get; }
    DbSet<ShippingRate> ShippingRates { get; }
    DbSet<CommerceSettings> CommerceSettings { get; }
    DbSet<StoreSettings> StoreSettings { get; }
    DbSet<ThemeSettings> ThemeSettings { get; }
    DbSet<NavigationMenu> NavigationMenus { get; }
    DbSet<HomepageSection> HomepageSections { get; }
    DbSet<ContentPage> ContentPages { get; }
    DbSet<MediaAsset> MediaAssets { get; }
    DbSet<AdminAuditLog> AdminAuditLogs { get; }

    DatabaseFacade Database { get; }
    Microsoft.EntityFrameworkCore.ChangeTracking.ChangeTracker ChangeTracker { get; }
    Task<int> SaveChangesAsync(CancellationToken cancellationToken = default);
}
