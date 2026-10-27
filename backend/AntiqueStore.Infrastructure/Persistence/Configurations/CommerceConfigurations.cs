using AntiqueStore.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace AntiqueStore.Infrastructure.Persistence.Configurations;

public class InventoryReservationConfiguration : IEntityTypeConfiguration<InventoryReservation>
{
    public void Configure(EntityTypeBuilder<InventoryReservation> b)
    {
        b.Property(x => x.CheckoutToken).HasMaxLength(64).IsRequired();
        b.HasOne(x => x.Product).WithMany().HasForeignKey(x => x.ProductId).OnDelete(DeleteBehavior.Restrict);
        b.HasOne(x => x.User).WithMany().HasForeignKey(x => x.UserId).OnDelete(DeleteBehavior.Restrict);
        b.HasOne(x => x.Order).WithMany().HasForeignKey(x => x.OrderId).OnDelete(DeleteBehavior.Restrict);
        b.HasIndex(x => new { x.Status, x.ExpiresAt });
        b.HasIndex(x => new { x.UserId, x.CheckoutToken });
        b.ToTable(t => t.HasCheckConstraint("CK_InventoryReservations_Quantity", "Quantity > 0"));
    }
}

public class InventoryMovementConfiguration : IEntityTypeConfiguration<InventoryMovement>
{
    public void Configure(EntityTypeBuilder<InventoryMovement> b)
    {
        b.Property(x => x.Reason).HasMaxLength(500);
        b.HasOne(x => x.Product).WithMany().HasForeignKey(x => x.ProductId).OnDelete(DeleteBehavior.Restrict);
        b.HasIndex(x => new { x.ProductId, x.CreatedAt });
    }
}

public class CartConfiguration : IEntityTypeConfiguration<Cart>
{
    public void Configure(EntityTypeBuilder<Cart> b)
    {
        b.HasIndex(x => x.UserId).IsUnique();
        b.HasOne(x => x.User).WithMany().HasForeignKey(x => x.UserId).OnDelete(DeleteBehavior.Cascade);
        b.HasMany(x => x.Items).WithOne(x => x.Cart).HasForeignKey(x => x.CartId).OnDelete(DeleteBehavior.Cascade);
    }
}

public class CartItemConfiguration : IEntityTypeConfiguration<CartItem>
{
    public void Configure(EntityTypeBuilder<CartItem> b)
    {
        b.HasIndex(x => new { x.CartId, x.ProductId }).IsUnique();
        b.HasOne(x => x.Product).WithMany().HasForeignKey(x => x.ProductId).OnDelete(DeleteBehavior.Cascade);
    }
}

public class WishlistConfiguration : IEntityTypeConfiguration<Wishlist>
{
    public void Configure(EntityTypeBuilder<Wishlist> b)
    {
        b.HasIndex(x => x.UserId).IsUnique();
        b.HasOne(x => x.User).WithMany().HasForeignKey(x => x.UserId).OnDelete(DeleteBehavior.Cascade);
        b.HasMany(x => x.Items).WithOne(x => x.Wishlist).HasForeignKey(x => x.WishlistId).OnDelete(DeleteBehavior.Cascade);
    }
}

public class WishlistItemConfiguration : IEntityTypeConfiguration<WishlistItem>
{
    public void Configure(EntityTypeBuilder<WishlistItem> b)
    {
        b.HasIndex(x => new { x.WishlistId, x.ProductId }).IsUnique();
        b.HasOne(x => x.Product).WithMany().HasForeignKey(x => x.ProductId).OnDelete(DeleteBehavior.Cascade);
    }
}

public class OrderConfiguration : IEntityTypeConfiguration<Order>
{
    public void Configure(EntityTypeBuilder<Order> b)
    {
        b.Property(x => x.OrderNumber).HasMaxLength(32).IsRequired();
        b.HasIndex(x => x.OrderNumber).IsUnique();
        b.Property(x => x.IdempotencyKey).HasMaxLength(64).IsRequired();
        // Unique per user: a retried "Place order" request can never create a second order.
        b.HasIndex(x => new { x.UserId, x.IdempotencyKey }).IsUnique();
        b.Property(x => x.CustomerName).HasMaxLength(Len.Name).IsRequired();
        b.Property(x => x.CustomerEmail).HasMaxLength(Len.Email).IsRequired();
        b.Property(x => x.CustomerPhone).HasMaxLength(32).IsRequired();
        b.Property(x => x.AddressLine1).HasMaxLength(Len.Name);
        b.Property(x => x.AddressLine2).HasMaxLength(Len.Name);
        b.Property(x => x.City).HasMaxLength(100);
        b.Property(x => x.District).HasMaxLength(100);
        b.Property(x => x.PostalCode).HasMaxLength(20);
        b.Property(x => x.Country).HasMaxLength(100);
        b.Property(x => x.CustomerNotes).HasMaxLength(1000);
        b.Property(x => x.Currency).HasMaxLength(3);
        b.Property(x => x.DiscountCodeSnapshot).HasMaxLength(Len.Short);
        b.Property(x => x.ShippingRateName).HasMaxLength(Len.Name);
        b.Property(x => x.TrackingCarrier).HasMaxLength(100);
        b.Property(x => x.TrackingNumber).HasMaxLength(100);
        b.Property(x => x.TrackingUrl).HasMaxLength(Len.Url);
        b.Property(x => x.AdminNotes).HasMaxLength(Len.Text);
        b.Property(x => x.CancellationReason).HasMaxLength(500);
        b.Property(x => x.Version).IsConcurrencyToken();

        b.HasOne(x => x.User).WithMany().HasForeignKey(x => x.UserId).OnDelete(DeleteBehavior.Restrict);
        b.HasOne(x => x.DiscountCode).WithMany().HasForeignKey(x => x.DiscountCodeId).OnDelete(DeleteBehavior.SetNull);
        b.HasMany(x => x.Items).WithOne(x => x.Order).HasForeignKey(x => x.OrderId).OnDelete(DeleteBehavior.Cascade);
        b.HasMany(x => x.Payments).WithOne(x => x.Order).HasForeignKey(x => x.OrderId).OnDelete(DeleteBehavior.Cascade);
        b.HasMany(x => x.History).WithOne(x => x.Order).HasForeignKey(x => x.OrderId).OnDelete(DeleteBehavior.Cascade);

        b.HasIndex(x => new { x.UserId, x.PlacedAt });
        b.HasIndex(x => new { x.Status, x.PlacedAt });
        b.HasIndex(x => new { x.PaymentStatus, x.PlacedAt });
        b.HasIndex(x => x.PlacedAt);
    }
}

public class OrderItemConfiguration : IEntityTypeConfiguration<OrderItem>
{
    public void Configure(EntityTypeBuilder<OrderItem> b)
    {
        b.Property(x => x.ProductName).HasMaxLength(Len.Name).IsRequired();
        b.Property(x => x.ProductSlug).HasMaxLength(Len.Name).IsRequired();
        b.Property(x => x.Sku).HasMaxLength(Len.Short).IsRequired();
        b.Property(x => x.ImageUrl).HasMaxLength(Len.Url);
        // Products referenced by orders can never be hard-deleted (they are archived instead).
        b.HasOne(x => x.Product).WithMany().HasForeignKey(x => x.ProductId).OnDelete(DeleteBehavior.Restrict);
        b.HasIndex(x => x.ProductId);
    }
}

public class OrderStatusHistoryConfiguration : IEntityTypeConfiguration<OrderStatusHistory>
{
    public void Configure(EntityTypeBuilder<OrderStatusHistory> b)
    {
        b.Property(x => x.Note).HasMaxLength(1000);
        b.HasIndex(x => new { x.OrderId, x.CreatedAt });
    }
}

public class PaymentConfiguration : IEntityTypeConfiguration<Payment>
{
    public void Configure(EntityTypeBuilder<Payment> b)
    {
        b.Property(x => x.Currency).HasMaxLength(3);
        b.Property(x => x.Reference).HasMaxLength(200);
        b.Property(x => x.ProviderName).HasMaxLength(Len.Short);
        b.Property(x => x.ProviderTransactionId).HasMaxLength(200);
        b.Property(x => x.Notes).HasMaxLength(1000);
        b.HasMany(x => x.Proofs).WithOne(x => x.Payment).HasForeignKey(x => x.PaymentId).OnDelete(DeleteBehavior.Cascade);
        b.HasIndex(x => x.OrderId);
    }
}

public class PaymentProofConfiguration : IEntityTypeConfiguration<PaymentProof>
{
    public void Configure(EntityTypeBuilder<PaymentProof> b)
    {
        b.Property(x => x.StorageKey).HasMaxLength(500).IsRequired();
        b.Property(x => x.OriginalFileName).HasMaxLength(255);
        b.Property(x => x.ContentType).HasMaxLength(100);
    }
}

public class DiscountCodeConfiguration : IEntityTypeConfiguration<DiscountCode>
{
    public void Configure(EntityTypeBuilder<DiscountCode> b)
    {
        b.Property(x => x.Code).HasMaxLength(Len.Short).IsRequired();
        b.HasIndex(x => x.Code).IsUnique();
        b.Property(x => x.Description).HasMaxLength(500);
        b.ToTable(t => t.HasCheckConstraint("CK_DiscountCodes_UsedCount", "UsedCount >= 0"));
    }
}

public class DiscountUsageConfiguration : IEntityTypeConfiguration<DiscountUsage>
{
    public void Configure(EntityTypeBuilder<DiscountUsage> b)
    {
        b.HasIndex(x => x.OrderId).IsUnique();
        b.HasIndex(x => new { x.DiscountCodeId, x.UserId });
        b.HasOne(x => x.DiscountCode).WithMany().HasForeignKey(x => x.DiscountCodeId).OnDelete(DeleteBehavior.Restrict);
        b.HasOne(x => x.Order).WithMany().HasForeignKey(x => x.OrderId).OnDelete(DeleteBehavior.Cascade);
    }
}

public class ShippingZoneConfiguration : IEntityTypeConfiguration<ShippingZone>
{
    public void Configure(EntityTypeBuilder<ShippingZone> b)
    {
        b.Property(x => x.Name).HasMaxLength(Len.Name).IsRequired();
        b.Property(x => x.EstimatedDelivery).HasMaxLength(Len.Name);
        b.HasMany(x => x.Districts).WithOne(x => x.ShippingZone).HasForeignKey(x => x.ShippingZoneId).OnDelete(DeleteBehavior.Cascade);
        b.HasMany(x => x.Rates).WithOne(x => x.ShippingZone).HasForeignKey(x => x.ShippingZoneId).OnDelete(DeleteBehavior.Cascade);
    }
}

public class ShippingZoneDistrictConfiguration : IEntityTypeConfiguration<ShippingZoneDistrict>
{
    public void Configure(EntityTypeBuilder<ShippingZoneDistrict> b)
    {
        b.Property(x => x.District).HasMaxLength(100).IsRequired();
        // A district belongs to exactly one zone, so a shipping quote is never ambiguous.
        b.HasIndex(x => x.District).IsUnique();
    }
}

public class ShippingRateConfiguration : IEntityTypeConfiguration<ShippingRate>
{
    public void Configure(EntityTypeBuilder<ShippingRate> b)
    {
        b.Property(x => x.Name).HasMaxLength(Len.Name).IsRequired();
        b.ToTable(t => t.HasCheckConstraint("CK_ShippingRates_Fee", "Fee >= 0"));
    }
}

public class CommerceSettingsConfiguration : IEntityTypeConfiguration<CommerceSettings>
{
    public void Configure(EntityTypeBuilder<CommerceSettings> b)
    {
        b.Property(x => x.Currency).HasMaxLength(3);
        b.Property(x => x.OrderNumberPrefix).HasMaxLength(8);
        b.Property(x => x.LocalPickupAddress).HasMaxLength(500);
        b.Property(x => x.LocalPickupInstructions).HasMaxLength(Len.Text);
        b.Property(x => x.DeliveryInstructions).HasMaxLength(Len.Text);
        b.Property(x => x.BankName).HasMaxLength(Len.Name);
        b.Property(x => x.BankAccountName).HasMaxLength(Len.Name);
        b.Property(x => x.BankAccountNumber).HasMaxLength(Len.Short);
        b.Property(x => x.BankBranch).HasMaxLength(Len.Name);
        b.Property(x => x.BankTransferInstructions).HasMaxLength(Len.Text);
        b.Property(x => x.ReturnsSummary).HasMaxLength(Len.Text);
    }
}
