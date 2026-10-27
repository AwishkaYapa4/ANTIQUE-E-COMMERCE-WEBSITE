using AntiqueStore.Domain.Common;

namespace AntiqueStore.Domain.Entities;

/// <summary>Brand, contact, announcement and footer settings. One row per <see cref="ContentStage"/>.</summary>
public class StoreSettings : Entity
{
    public ContentStage Stage { get; set; }
    public string StoreName { get; set; } = "Antique Heritage Store";
    public string? Tagline { get; set; }
    public string? Description { get; set; }
    public string? LogoUrl { get; set; }
    public string? FaviconUrl { get; set; }
    public string? ContactEmail { get; set; }
    public string? ContactPhone { get; set; }
    public string? WhatsAppNumber { get; set; }
    public string? Address { get; set; }
    public string? OpeningHours { get; set; }
    public string? MapEmbedUrl { get; set; }
    /// <summary>JSON array of { platform, url }.</summary>
    public string SocialLinksJson { get; set; } = "[]";
    public bool AnnouncementEnabled { get; set; }
    public string? AnnouncementText { get; set; }
    public string? AnnouncementLink { get; set; }
    public string? FooterDescription { get; set; }
    public string? CopyrightText { get; set; }
    public bool NewsletterEnabled { get; set; } = true;
    public string? SeoDefaultTitle { get; set; }
    public string? SeoDefaultDescription { get; set; }
    public string? DefaultOgImageUrl { get; set; }
    public int? UpdatedByUserId { get; set; }
}

/// <summary>Design tokens. One row per <see cref="ContentStage"/>.</summary>
public class ThemeSettings : Entity
{
    public ContentStage Stage { get; set; }
    public string BackgroundColor { get; set; } = "#F8F5EF";
    public string SurfaceColor { get; set; } = "#F0E8DC";
    public string TextColor { get; set; } = "#29231E";
    public string MutedTextColor { get; set; } = "#77716B";
    public string PrimaryColor { get; set; } = "#45382B";
    public string AccentColor { get; set; } = "#B08D57";
    public string SecondaryColor { get; set; } = "#777660";
    public string BorderColor { get; set; } = "#E5DED3";
    public string ButtonColor { get; set; } = "#45382B";
    public string ButtonTextColor { get; set; } = "#FFFFFF";
    public string SuccessColor { get; set; } = "#47745B";
    public string ErrorColor { get; set; } = "#B84D4D";
    public string HeadingFont { get; set; } = "Cormorant Garamond";
    public string BodyFont { get; set; } = "Inter";
    public int BaseFontSize { get; set; } = 16;
    /// <summary>Modular type scale ratio x100 (e.g. 125 = 1.25).</summary>
    public int HeadingScale { get; set; } = 125;
    public int BorderRadius { get; set; } = 4;
    public int? UpdatedByUserId { get; set; }
}

/// <summary>Navigation menu (main, footer columns). Items stored as validated JSON.</summary>
public class NavigationMenu : Entity
{
    public ContentStage Stage { get; set; }
    public string Location { get; set; } = "";
    public string Title { get; set; } = "";
    /// <summary>JSON array of { label, href, children? }.</summary>
    public string ItemsJson { get; set; } = "[]";
}

/// <summary>A homepage builder section. Config JSON is validated per <see cref="Type"/>.</summary>
public class HomepageSection : Entity
{
    public ContentStage Stage { get; set; }
    /// <summary>Stable id shared by the draft and published copy of a section.</summary>
    public string SectionKey { get; set; } = "";
    public string Type { get; set; } = "";
    public int SortOrder { get; set; }
    public bool IsVisible { get; set; } = true;
    public string ConfigJson { get; set; } = "{}";
}

public class ContentPage : Entity
{
    public string Slug { get; set; } = "";
    public string Title { get; set; } = "";
    /// <summary>Published sanitized HTML.</summary>
    public string Body { get; set; } = "";
    public string DraftTitle { get; set; } = "";
    public string DraftBody { get; set; } = "";
    public bool HasUnpublishedChanges { get; set; }
    public bool IsPublished { get; set; }
    /// <summary>System pages (policies, about) cannot be deleted.</summary>
    public bool IsSystem { get; set; }
    public string? SeoTitle { get; set; }
    public string? SeoDescription { get; set; }
    public DateTime? PublishedAt { get; set; }
}

public class MediaAsset : Entity
{
    public string Provider { get; set; } = "";
    public string StorageKey { get; set; } = "";
    public string Url { get; set; } = "";
    /// <summary>JSON object of width → URL for responsive images.</summary>
    public string VariantsJson { get; set; } = "{}";
    public string FileName { get; set; } = "";
    public string ContentType { get; set; } = "";
    public long SizeBytes { get; set; }
    public int Width { get; set; }
    public int Height { get; set; }
    public string? AltText { get; set; }
    public string? Title { get; set; }
    public string Folder { get; set; } = "general";
    public int? UploadedByUserId { get; set; }
}

public class AdminAuditLog
{
    public long Id { get; set; }
    public int? UserId { get; set; }
    public string? UserEmail { get; set; }
    public string Action { get; set; } = "";
    public string EntityType { get; set; } = "";
    public string? EntityId { get; set; }
    public string? Summary { get; set; }
    public string? DataJson { get; set; }
    public string? IpAddress { get; set; }
    public DateTime CreatedAt { get; set; }
}
