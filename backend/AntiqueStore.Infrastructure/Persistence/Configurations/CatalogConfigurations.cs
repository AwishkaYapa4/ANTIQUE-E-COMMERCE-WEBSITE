using AntiqueStore.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace AntiqueStore.Infrastructure.Persistence.Configurations;

internal static class Len
{
    public const int Short = 64;
    public const int Name = 200;
    public const int Url = 1024;
    public const int Text = 2000;
    public const int Email = 254;
}

public class UserConfiguration : IEntityTypeConfiguration<User>
{
    public void Configure(EntityTypeBuilder<User> b)
    {
        b.Property(x => x.FirebaseUid).HasMaxLength(128).IsRequired();
        b.HasIndex(x => x.FirebaseUid).IsUnique();
        b.Property(x => x.Email).HasMaxLength(Len.Email).IsRequired();
        b.HasIndex(x => x.Email);
        b.Property(x => x.DisplayName).HasMaxLength(Len.Name);
        b.Property(x => x.PhoneNumber).HasMaxLength(32);
        b.Property(x => x.PhotoUrl).HasMaxLength(Len.Url);
        b.HasMany(x => x.Addresses).WithOne(x => x.User).HasForeignKey(x => x.UserId).OnDelete(DeleteBehavior.Cascade);
    }
}

public class UserAddressConfiguration : IEntityTypeConfiguration<UserAddress>
{
    public void Configure(EntityTypeBuilder<UserAddress> b)
    {
        b.Property(x => x.Label).HasMaxLength(Len.Short);
        b.Property(x => x.FullName).HasMaxLength(Len.Name).IsRequired();
        b.Property(x => x.Phone).HasMaxLength(32).IsRequired();
        b.Property(x => x.Line1).HasMaxLength(Len.Name).IsRequired();
        b.Property(x => x.Line2).HasMaxLength(Len.Name);
        b.Property(x => x.City).HasMaxLength(100).IsRequired();
        b.Property(x => x.District).HasMaxLength(100).IsRequired();
        b.Property(x => x.PostalCode).HasMaxLength(20);
        b.Property(x => x.Country).HasMaxLength(100);
        b.HasIndex(x => x.UserId);
    }
}

public class CategoryConfiguration : IEntityTypeConfiguration<Category>
{
    public void Configure(EntityTypeBuilder<Category> b)
    {
        b.Property(x => x.Name).HasMaxLength(Len.Name).IsRequired();
        b.Property(x => x.Slug).HasMaxLength(Len.Name).IsRequired();
        b.HasIndex(x => x.Slug).IsUnique();
        b.Property(x => x.Description).HasMaxLength(Len.Text);
        b.Property(x => x.ImageUrl).HasMaxLength(Len.Url);
        b.Property(x => x.SeoTitle).HasMaxLength(Len.Name);
        b.Property(x => x.SeoDescription).HasMaxLength(500);
        // Deleting a parent must never cascade into children or products.
        b.HasOne(x => x.Parent).WithMany(x => x.Children).HasForeignKey(x => x.ParentId).OnDelete(DeleteBehavior.Restrict);
        b.HasIndex(x => new { x.IsVisible, x.SortOrder });
    }
}

public class CollectionConfiguration : IEntityTypeConfiguration<Collection>
{
    public void Configure(EntityTypeBuilder<Collection> b)
    {
        b.Property(x => x.Name).HasMaxLength(Len.Name).IsRequired();
        b.Property(x => x.Slug).HasMaxLength(Len.Name).IsRequired();
        b.HasIndex(x => x.Slug).IsUnique();
        b.Property(x => x.Description).HasMaxLength(Len.Text);
        b.Property(x => x.ImageUrl).HasMaxLength(Len.Url);
        b.Property(x => x.SeoTitle).HasMaxLength(Len.Name);
        b.Property(x => x.SeoDescription).HasMaxLength(500);
    }
}

public class ProductConfiguration : IEntityTypeConfiguration<Product>
{
    public void Configure(EntityTypeBuilder<Product> b)
    {
        b.Property(x => x.Name).HasMaxLength(Len.Name).IsRequired();
        b.Property(x => x.Slug).HasMaxLength(Len.Name).IsRequired();
        b.HasIndex(x => x.Slug).IsUnique();
        b.Property(x => x.Sku).HasMaxLength(Len.Short).IsRequired();
        b.HasIndex(x => x.Sku).IsUnique();
        b.Property(x => x.ShortDescription).HasMaxLength(500);
        b.Property(x => x.ConditionNotes).HasMaxLength(Len.Text);
        b.Property(x => x.SignsOfWear).HasMaxLength(Len.Text);
        b.Property(x => x.Scratches).HasMaxLength(Len.Text);
        b.Property(x => x.Damage).HasMaxLength(Len.Text);
        b.Property(x => x.RestorationHistory).HasMaxLength(Len.Text);
        b.Property(x => x.MissingParts).HasMaxLength(Len.Text);
        b.Property(x => x.FunctionalStatus).HasMaxLength(Len.Text);
        b.Property(x => x.Material).HasMaxLength(100);
        b.Property(x => x.Color).HasMaxLength(100);
        b.Property(x => x.Dimensions).HasMaxLength(Len.Name);
        b.Property(x => x.Era).HasMaxLength(100);
        b.Property(x => x.ApproximateAge).HasMaxLength(100);
        b.Property(x => x.Provenance).HasMaxLength(Len.Text);
        b.Property(x => x.ProvenanceEvidence).HasMaxLength(Len.Text);
        b.Property(x => x.SeoTitle).HasMaxLength(Len.Name);
        b.Property(x => x.SeoDescription).HasMaxLength(500);
        b.Property(x => x.Version).IsConcurrencyToken();
        b.Ignore(x => x.InventoryState);

        // Database-level guarantee that stock never goes negative, even if application code is wrong.
        b.ToTable(t =>
        {
            t.HasCheckConstraint("CK_Products_QuantityAvailable", "QuantityAvailable >= 0");
            t.HasCheckConstraint("CK_Products_QuantityReserved", "QuantityReserved >= 0");
            t.HasCheckConstraint("CK_Products_QuantitySold", "QuantitySold >= 0");
            t.HasCheckConstraint("CK_Products_Price", "Price >= 0");
        });

        b.HasOne(x => x.Category).WithMany().HasForeignKey(x => x.CategoryId).OnDelete(DeleteBehavior.SetNull);
        b.HasMany(x => x.Images).WithOne(x => x.Product).HasForeignKey(x => x.ProductId).OnDelete(DeleteBehavior.Cascade);
        b.HasMany(x => x.Attributes).WithOne(x => x.Product).HasForeignKey(x => x.ProductId).OnDelete(DeleteBehavior.Cascade);

        b.HasIndex(x => new { x.Status, x.CreatedAt });
        b.HasIndex(x => new { x.Status, x.Price });
        b.HasIndex(x => new { x.Status, x.IsFeatured });
        b.HasIndex(x => x.Era);
        b.HasIndex(x => x.Material);
    }
}

public class ProductImageConfiguration : IEntityTypeConfiguration<ProductImage>
{
    public void Configure(EntityTypeBuilder<ProductImage> b)
    {
        b.Property(x => x.Url).HasMaxLength(Len.Url).IsRequired();
        b.Property(x => x.AltText).HasMaxLength(300);
        b.HasOne(x => x.MediaAsset).WithMany().HasForeignKey(x => x.MediaAssetId).OnDelete(DeleteBehavior.SetNull);
        b.HasIndex(x => new { x.ProductId, x.SortOrder });
    }
}

public class ProductCollectionConfiguration : IEntityTypeConfiguration<ProductCollection>
{
    public void Configure(EntityTypeBuilder<ProductCollection> b)
    {
        b.HasKey(x => new { x.ProductId, x.CollectionId });
        b.HasOne(x => x.Product).WithMany(x => x.Collections).HasForeignKey(x => x.ProductId).OnDelete(DeleteBehavior.Cascade);
        b.HasOne(x => x.Collection).WithMany(x => x.Products).HasForeignKey(x => x.CollectionId).OnDelete(DeleteBehavior.Cascade);
        b.HasIndex(x => new { x.CollectionId, x.SortOrder });
    }
}

public class ProductAttributeConfiguration : IEntityTypeConfiguration<ProductAttribute>
{
    public void Configure(EntityTypeBuilder<ProductAttribute> b)
    {
        b.Property(x => x.Name).HasMaxLength(100).IsRequired();
        b.Property(x => x.Value).HasMaxLength(500).IsRequired();
    }
}

public class MediaAssetConfiguration : IEntityTypeConfiguration<MediaAsset>
{
    public void Configure(EntityTypeBuilder<MediaAsset> b)
    {
        b.Property(x => x.Provider).HasMaxLength(Len.Short).IsRequired();
        b.Property(x => x.StorageKey).HasMaxLength(500).IsRequired();
        b.Property(x => x.Url).HasMaxLength(Len.Url).IsRequired();
        b.Property(x => x.FileName).HasMaxLength(255);
        b.Property(x => x.ContentType).HasMaxLength(100);
        b.Property(x => x.AltText).HasMaxLength(300);
        b.Property(x => x.Title).HasMaxLength(Len.Name);
        b.Property(x => x.Folder).HasMaxLength(Len.Short);
        b.HasIndex(x => x.CreatedAt);
        b.HasIndex(x => x.Folder);
    }
}
