using System.Text.Json;
using AntiqueStore.Application.Catalog;

namespace AntiqueStore.Application.Customization;

public sealed record SocialLink(string Platform, string Url);

public sealed record BrandSettingsDto(
    string StoreName, string? Tagline, string? Description, string? LogoUrl, string? FaviconUrl,
    string? ContactEmail, string? ContactPhone, string? WhatsAppNumber, string? Address, string? OpeningHours,
    string? MapEmbedUrl, IReadOnlyList<SocialLink> SocialLinks, bool AnnouncementEnabled, string? AnnouncementText,
    string? AnnouncementLink, string? FooterDescription, string? CopyrightText, bool NewsletterEnabled,
    string? SeoDefaultTitle, string? SeoDefaultDescription, string? DefaultOgImageUrl);

public sealed record ThemeDto(
    string BackgroundColor, string SurfaceColor, string TextColor, string MutedTextColor, string PrimaryColor,
    string AccentColor, string SecondaryColor, string BorderColor, string ButtonColor, string ButtonTextColor,
    string SuccessColor, string ErrorColor, string HeadingFont, string BodyFont, int BaseFontSize, int HeadingScale,
    int BorderRadius);

public sealed record NavItem(string Label, string Href, IReadOnlyList<NavItem>? Children = null);

public sealed record NavigationMenuDto(string Location, string Title, IReadOnlyList<NavItem> Items);

public sealed record HomepageSectionInput(string? Key, string Type, bool IsVisible, JsonElement Config);

public sealed record HomepageSectionDto(string Key, string Type, int SortOrder, bool IsVisible, JsonElement Config);

public sealed record TestimonialDto(int Id, string Quote, string AuthorName, string? AuthorLocation, int? Rating);

/// <summary>A section plus the live data it needs (products, categories, testimonials).</summary>
public sealed record RenderedSectionDto(
    string Key, string Type, JsonElement Config, IReadOnlyList<ProductCardDto>? Products,
    IReadOnlyList<CategoryDto>? Categories, IReadOnlyList<TestimonialDto>? Testimonials);

public sealed record PublicCommerceDto(
    string Currency, bool CashOnDeliveryEnabled, bool BankTransferEnabled, bool LocalPickupEnabled,
    decimal? FreeShippingThreshold, int ReservationMinutes, bool ReviewsEnabled, string? DeliveryInstructions,
    string? ReturnsSummary);

public sealed record SiteConfigDto(
    BrandSettingsDto Brand, ThemeDto Theme, IReadOnlyList<NavigationMenuDto> Navigation, PublicCommerceDto Commerce,
    bool IsPreview);

public sealed record HomepageDto(IReadOnlyList<RenderedSectionDto> Sections, bool IsPreview);

public sealed record AdminCustomizationDto(
    BrandSettingsDto Brand, ThemeDto Theme, IReadOnlyList<NavigationMenuDto> Navigation,
    IReadOnlyList<HomepageSectionDto> Sections, bool HasUnpublishedChanges, DateTime? LastPublishedAt,
    IReadOnlyList<string> HeadingFonts, IReadOnlyList<string> BodyFonts, IReadOnlyList<string> SectionTypes);

public sealed record SaveNavigationRequest(IReadOnlyList<NavigationMenuDto> Menus);

public sealed record SaveHomepageRequest(IReadOnlyList<HomepageSectionInput> Sections);

public static class FontCatalog
{
    public static readonly IReadOnlyList<string> Heading =
        ["Cormorant Garamond", "Playfair Display", "Lora", "Libre Baskerville", "EB Garamond", "DM Serif Display"];

    public static readonly IReadOnlyList<string> Body =
        ["Inter", "DM Sans", "Lato", "Source Sans 3", "Nunito Sans", "Work Sans"];
}

public static class NavigationLocations
{
    public const string Main = "main";
    public const string FooterShop = "footer-shop";
    public const string FooterHelp = "footer-help";
    public static readonly IReadOnlyList<string> All = [Main, FooterShop, FooterHelp];
}
