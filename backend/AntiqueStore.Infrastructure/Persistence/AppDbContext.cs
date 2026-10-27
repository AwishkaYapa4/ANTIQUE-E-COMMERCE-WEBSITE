using AntiqueStore.Application.Common;
using AntiqueStore.Domain.Common;
using AntiqueStore.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;

namespace AntiqueStore.Infrastructure.Persistence;

public class AppDbContext(DbContextOptions<AppDbContext> options, TimeProvider clock) : DbContext(options), IAppDbContext
{
    public DbSet<User> Users => Set<User>();
    public DbSet<UserAddress> UserAddresses => Set<UserAddress>();
    public DbSet<Category> Categories => Set<Category>();
    public DbSet<Collection> Collections => Set<Collection>();
    public DbSet<Product> Products => Set<Product>();
    public DbSet<ProductImage> ProductImages => Set<ProductImage>();
    public DbSet<ProductCollection> ProductCollections => Set<ProductCollection>();
    public DbSet<ProductAttribute> ProductAttributes => Set<ProductAttribute>();
    public DbSet<InventoryReservation> InventoryReservations => Set<InventoryReservation>();
    public DbSet<InventoryMovement> InventoryMovements => Set<InventoryMovement>();
    public DbSet<Cart> Carts => Set<Cart>();
    public DbSet<CartItem> CartItems => Set<CartItem>();
    public DbSet<Wishlist> Wishlists => Set<Wishlist>();
    public DbSet<WishlistItem> WishlistItems => Set<WishlistItem>();
    public DbSet<Order> Orders => Set<Order>();
    public DbSet<OrderItem> OrderItems => Set<OrderItem>();
    public DbSet<OrderStatusHistory> OrderStatusHistory => Set<OrderStatusHistory>();
    public DbSet<Payment> Payments => Set<Payment>();
    public DbSet<PaymentProof> PaymentProofs => Set<PaymentProof>();
    public DbSet<Review> Reviews => Set<Review>();
    public DbSet<Enquiry> Enquiries => Set<Enquiry>();
    public DbSet<Testimonial> Testimonials => Set<Testimonial>();
    public DbSet<NewsletterSubscriber> NewsletterSubscribers => Set<NewsletterSubscriber>();
    public DbSet<DiscountCode> DiscountCodes => Set<DiscountCode>();
    public DbSet<DiscountUsage> DiscountUsages => Set<DiscountUsage>();
    public DbSet<ShippingZone> ShippingZones => Set<ShippingZone>();
    public DbSet<ShippingZoneDistrict> ShippingZoneDistricts => Set<ShippingZoneDistrict>();
    public DbSet<ShippingRate> ShippingRates => Set<ShippingRate>();
    public DbSet<CommerceSettings> CommerceSettings => Set<CommerceSettings>();
    public DbSet<StoreSettings> StoreSettings => Set<StoreSettings>();
    public DbSet<ThemeSettings> ThemeSettings => Set<ThemeSettings>();
    public DbSet<NavigationMenu> NavigationMenus => Set<NavigationMenu>();
    public DbSet<HomepageSection> HomepageSections => Set<HomepageSection>();
    public DbSet<ContentPage> ContentPages => Set<ContentPage>();
    public DbSet<MediaAsset> MediaAssets => Set<MediaAsset>();
    public DbSet<AdminAuditLog> AdminAuditLogs => Set<AdminAuditLog>();

    protected override void ConfigureConventions(ModelConfigurationBuilder builder)
    {
        // Money is always decimal(18,2); never floating point.
        builder.Properties<decimal>().HavePrecision(18, 2);
        // Everything is stored as UTC and read back with DateTimeKind.Utc.
        builder.Properties<DateTime>().HaveConversion<UtcDateTimeConverter>();
        builder.Properties<DateTime?>().HaveConversion<NullableUtcDateTimeConverter>();
    }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(AppDbContext).Assembly);

        // MySQL InnoDB limits the combined size of VARCHAR columns in a row to 65,535 bytes (utf8mb4 = 4 bytes/char).
        // Long free-text columns are therefore stored as LONGTEXT (off-page); their length limits are still enforced
        // by request validation. Indexed columns are never converted. SQL Server has no LONGTEXT type and maps long
        // strings to nvarchar(max) itself, so it is skipped.
        if (Database.IsSqlServer()) return;
        foreach (var entity in modelBuilder.Model.GetEntityTypes())
        foreach (var property in entity.GetProperties().Where(p => p.ClrType == typeof(string)))
        {
            var isIndexed = entity.GetIndexes().Any(i => i.Properties.Contains(property)) || property.IsKey() || property.IsForeignKey();
            if (!isIndexed && property.GetMaxLength() is >= 1000) property.SetColumnType("longtext");
        }
    }

    public override Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        var now = clock.GetUtcNow().UtcDateTime;
        foreach (var entry in ChangeTracker.Entries<Entity>())
        {
            if (entry.State == EntityState.Added)
            {
                if (entry.Entity.CreatedAt == default) entry.Entity.CreatedAt = now;
                entry.Entity.UpdatedAt = now;
            }
            else if (entry.State == EntityState.Modified)
            {
                entry.Entity.UpdatedAt = now;
            }
        }

        foreach (var entry in ChangeTracker.Entries<Product>().Where(e => e.State == EntityState.Modified))
            entry.Entity.Version++;
        foreach (var entry in ChangeTracker.Entries<Order>().Where(e => e.State == EntityState.Modified))
            entry.Entity.Version++;

        return base.SaveChangesAsync(cancellationToken);
    }

    private sealed class UtcDateTimeConverter() : ValueConverter<DateTime, DateTime>(
        v => v.Kind == DateTimeKind.Utc ? v : v.ToUniversalTime(),
        v => DateTime.SpecifyKind(v, DateTimeKind.Utc));

    private sealed class NullableUtcDateTimeConverter() : ValueConverter<DateTime?, DateTime?>(
        v => v.HasValue ? (v.Value.Kind == DateTimeKind.Utc ? v : v.Value.ToUniversalTime()) : v,
        v => v.HasValue ? DateTime.SpecifyKind(v.Value, DateTimeKind.Utc) : v);
}
