using AntiqueStore.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace AntiqueStore.Infrastructure.Persistence.Configurations;

public class StoreSettingsConfiguration : IEntityTypeConfiguration<StoreSettings>
{
    public void Configure(EntityTypeBuilder<StoreSettings> b)
    {
        b.HasIndex(x => x.Stage).IsUnique();
        b.Property(x => x.StoreName).HasMaxLength(Len.Name).IsRequired();
        b.Property(x => x.Tagline).HasMaxLength(Len.Name);
        b.Property(x => x.Description).HasMaxLength(Len.Text);
        b.Property(x => x.LogoUrl).HasMaxLength(Len.Url);
        b.Property(x => x.FaviconUrl).HasMaxLength(Len.Url);
        b.Property(x => x.ContactEmail).HasMaxLength(Len.Email);
        b.Property(x => x.ContactPhone).HasMaxLength(32);
        b.Property(x => x.WhatsAppNumber).HasMaxLength(32);
        b.Property(x => x.Address).HasMaxLength(500);
        b.Property(x => x.OpeningHours).HasMaxLength(500);
        b.Property(x => x.MapEmbedUrl).HasMaxLength(Len.Url);
        b.Property(x => x.SocialLinksJson).HasMaxLength(4000);
        b.Property(x => x.AnnouncementText).HasMaxLength(300);
        b.Property(x => x.AnnouncementLink).HasMaxLength(Len.Url);
        b.Property(x => x.FooterDescription).HasMaxLength(Len.Text);
        b.Property(x => x.CopyrightText).HasMaxLength(300);
        b.Property(x => x.SeoDefaultTitle).HasMaxLength(Len.Name);
        b.Property(x => x.SeoDefaultDescription).HasMaxLength(500);
        b.Property(x => x.DefaultOgImageUrl).HasMaxLength(Len.Url);
    }
}

public class ThemeSettingsConfiguration : IEntityTypeConfiguration<ThemeSettings>
{
    public void Configure(EntityTypeBuilder<ThemeSettings> b)
    {
        b.HasIndex(x => x.Stage).IsUnique();
        foreach (var p in new[]
                 {
                     nameof(ThemeSettings.BackgroundColor), nameof(ThemeSettings.SurfaceColor), nameof(ThemeSettings.TextColor),
                     nameof(ThemeSettings.MutedTextColor), nameof(ThemeSettings.PrimaryColor), nameof(ThemeSettings.AccentColor),
                     nameof(ThemeSettings.SecondaryColor), nameof(ThemeSettings.BorderColor), nameof(ThemeSettings.ButtonColor),
                     nameof(ThemeSettings.ButtonTextColor), nameof(ThemeSettings.SuccessColor), nameof(ThemeSettings.ErrorColor)
                 })
            b.Property<string>(p).HasMaxLength(7).IsRequired();
        b.Property(x => x.HeadingFont).HasMaxLength(Len.Short);
        b.Property(x => x.BodyFont).HasMaxLength(Len.Short);
    }
}

public class NavigationMenuConfiguration : IEntityTypeConfiguration<NavigationMenu>
{
    public void Configure(EntityTypeBuilder<NavigationMenu> b)
    {
        b.Property(x => x.Location).HasMaxLength(Len.Short).IsRequired();
        b.Property(x => x.Title).HasMaxLength(Len.Name);
        b.Property(x => x.ItemsJson).HasMaxLength(16000);
        b.HasIndex(x => new { x.Stage, x.Location }).IsUnique();
    }
}

public class HomepageSectionConfiguration : IEntityTypeConfiguration<HomepageSection>
{
    public void Configure(EntityTypeBuilder<HomepageSection> b)
    {
        b.Property(x => x.SectionKey).HasMaxLength(64).IsRequired();
        b.Property(x => x.Type).HasMaxLength(Len.Short).IsRequired();
        // ConfigJson is unbounded (long text) – rich text sections can be large.
        b.HasIndex(x => new { x.Stage, x.SectionKey }).IsUnique();
        b.HasIndex(x => new { x.Stage, x.SortOrder });
    }
}

public class ContentPageConfiguration : IEntityTypeConfiguration<ContentPage>
{
    public void Configure(EntityTypeBuilder<ContentPage> b)
    {
        b.Property(x => x.Slug).HasMaxLength(Len.Name).IsRequired();
        b.HasIndex(x => x.Slug).IsUnique();
        b.Property(x => x.Title).HasMaxLength(Len.Name).IsRequired();
        b.Property(x => x.DraftTitle).HasMaxLength(Len.Name).IsRequired();
        b.Property(x => x.SeoTitle).HasMaxLength(Len.Name);
        b.Property(x => x.SeoDescription).HasMaxLength(500);
    }
}

public class ReviewConfiguration : IEntityTypeConfiguration<Review>
{
    public void Configure(EntityTypeBuilder<Review> b)
    {
        b.Property(x => x.Title).HasMaxLength(Len.Name);
        b.Property(x => x.Body).HasMaxLength(Len.Text).IsRequired();
        b.HasIndex(x => new { x.ProductId, x.UserId }).IsUnique();
        b.HasIndex(x => new { x.Status, x.CreatedAt });
        b.HasOne(x => x.Product).WithMany().HasForeignKey(x => x.ProductId).OnDelete(DeleteBehavior.Cascade);
        b.HasOne(x => x.User).WithMany().HasForeignKey(x => x.UserId).OnDelete(DeleteBehavior.Cascade);
        b.ToTable(t => t.HasCheckConstraint("CK_Reviews_Rating", "Rating >= 1 AND Rating <= 5"));
    }
}

public class EnquiryConfiguration : IEntityTypeConfiguration<Enquiry>
{
    public void Configure(EntityTypeBuilder<Enquiry> b)
    {
        b.Property(x => x.Name).HasMaxLength(Len.Name).IsRequired();
        b.Property(x => x.Email).HasMaxLength(Len.Email).IsRequired();
        b.Property(x => x.Phone).HasMaxLength(32);
        b.Property(x => x.Subject).HasMaxLength(Len.Name);
        b.Property(x => x.Message).HasMaxLength(4000).IsRequired();
        b.Property(x => x.AdminNotes).HasMaxLength(Len.Text);
        b.Property(x => x.IpHash).HasMaxLength(64);
        b.HasOne(x => x.Product).WithMany().HasForeignKey(x => x.ProductId).OnDelete(DeleteBehavior.SetNull);
        b.HasIndex(x => new { x.Status, x.CreatedAt });
    }
}

public class TestimonialConfiguration : IEntityTypeConfiguration<Testimonial>
{
    public void Configure(EntityTypeBuilder<Testimonial> b)
    {
        b.Property(x => x.Quote).HasMaxLength(Len.Text).IsRequired();
        b.Property(x => x.AuthorName).HasMaxLength(Len.Name).IsRequired();
        b.Property(x => x.AuthorLocation).HasMaxLength(Len.Name);
    }
}

public class NewsletterSubscriberConfiguration : IEntityTypeConfiguration<NewsletterSubscriber>
{
    public void Configure(EntityTypeBuilder<NewsletterSubscriber> b)
    {
        b.Property(x => x.Email).HasMaxLength(Len.Email).IsRequired();
        b.HasIndex(x => x.Email).IsUnique();
        b.Property(x => x.Source).HasMaxLength(Len.Short);
        b.Property(x => x.UnsubscribeToken).HasMaxLength(64).IsRequired();
        b.HasIndex(x => x.UnsubscribeToken).IsUnique();
    }
}

public class AdminAuditLogConfiguration : IEntityTypeConfiguration<AdminAuditLog>
{
    public void Configure(EntityTypeBuilder<AdminAuditLog> b)
    {
        b.Property(x => x.UserEmail).HasMaxLength(Len.Email);
        b.Property(x => x.Action).HasMaxLength(100).IsRequired();
        b.Property(x => x.EntityType).HasMaxLength(100).IsRequired();
        b.Property(x => x.EntityId).HasMaxLength(100);
        b.Property(x => x.Summary).HasMaxLength(1000);
        b.Property(x => x.IpAddress).HasMaxLength(64);
        b.HasIndex(x => x.CreatedAt);
        b.HasIndex(x => new { x.EntityType, x.EntityId });
    }
}
