using System.Text.Json;
using System.Text.Json.Serialization;
using AntiqueStore.Application.Common;

namespace AntiqueStore.Application.Customization;

public static class SectionTypes
{
    public const string Hero = "hero";
    public const string FeaturedCategories = "featuredCategories";
    public const string FeaturedProducts = "featuredProducts";
    public const string NewArrivals = "newArrivals";
    public const string CollectionProducts = "collectionProducts";
    public const string ImageText = "imageText";
    public const string PromoBanner = "promoBanner";
    public const string Testimonials = "testimonials";
    public const string Newsletter = "newsletter";
    public const string RichText = "richText";
    public const string EraShowcase = "eraShowcase";
    public const string TrustFeatures = "trustFeatures";

    public static readonly IReadOnlyList<string> All =
    [
        Hero, FeaturedCategories, FeaturedProducts, NewArrivals, CollectionProducts, ImageText, PromoBanner,
        Testimonials, Newsletter, RichText, EraShowcase, TrustFeatures
    ];
}

public sealed record CtaLink(string Label, string Href);

public sealed record HeroConfig(
    string? ImageUrl, string Title, string? Subtitle, string? Eyebrow, CtaLink? PrimaryCta, CtaLink? SecondaryCta,
    string Alignment = "left", string Height = "large", int OverlayOpacity = 35);

public sealed record FeaturedCategoriesConfig(string Title, string? Subtitle, int[]? CategoryIds, int Limit = 8);

public sealed record FeaturedProductsConfig(string Title, string? Subtitle, int[]? ProductIds, int Limit = 8, CtaLink? Cta = null);

public sealed record NewArrivalsConfig(string Title, string? Subtitle, int Limit = 8, CtaLink? Cta = null);

public sealed record CollectionProductsConfig(string Title, string? Subtitle, int? CollectionId, int Limit = 8, CtaLink? Cta = null);

public sealed record ImageTextConfig(
    string? ImageUrl, string? Eyebrow, string Heading, string? Body, CtaLink? Cta,
    string ImagePosition = "left", string Background = "surface");

public sealed record PromoBannerConfig(string? ImageUrl, string? Eyebrow, string Heading, string? Text, CtaLink? Cta, string Theme = "dark");

public sealed record TestimonialsConfig(string Title, string? Subtitle, int Limit = 6);

public sealed record NewsletterConfig(string Title, string? Text, string ButtonLabel = "Subscribe");

public sealed record RichTextConfig(string? Title, string Html, string Width = "narrow");

public sealed record EraItem(string Label, string? ImageUrl, string Href);
public sealed record EraShowcaseConfig(string Title, string? Subtitle, EraItem[] Items);

public sealed record TrustItem(string Icon, string Title, string Text);
public sealed record TrustFeaturesConfig(string? Title, TrustItem[] Items);

/// <summary>
/// Validates homepage section configuration against a typed schema per section type, then re-serializes
/// it so unknown properties are dropped. Rich text is sanitized; links and image URLs must be safe.
/// </summary>
public static class SectionConfigValidator
{
    public static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web)
    {
        DefaultIgnoreCondition = JsonIgnoreCondition.Never,
        UnmappedMemberHandling = JsonUnmappedMemberHandling.Skip
    };

    private static readonly string[] TrustIcons = ["gem", "camera", "shield", "truck", "search", "heart", "award", "clock", "package", "sparkles"];

    public static string Normalize(string type, JsonElement config, IHtmlSanitizer sanitizer, string path)
    {
        var errors = new List<string>();
        object normalized;
        try
        {
            normalized = type switch
            {
                SectionTypes.Hero => Check(Deserialize<HeroConfig>(config), errors, c =>
                {
                    Required(c.Title, "Title", 150, errors);
                    Max(c.Subtitle, "Subtitle", 400, errors);
                    Max(c.Eyebrow, "Eyebrow", 80, errors);
                    Image(c.ImageUrl, errors);
                    Cta(c.PrimaryCta, errors);
                    Cta(c.SecondaryCta, errors);
                    OneOf(c.Alignment, "Alignment", ["left", "center", "right"], errors);
                    OneOf(c.Height, "Height", ["medium", "large", "full"], errors);
                    if (c.OverlayOpacity is < 0 or > 80) errors.Add("Overlay opacity must be between 0 and 80.");
                }),
                SectionTypes.FeaturedCategories => Check(Deserialize<FeaturedCategoriesConfig>(config), errors, c =>
                {
                    Heading(c.Title, c.Subtitle, errors);
                    Limit(c.Limit, 12, errors);
                }),
                SectionTypes.FeaturedProducts => Check(Deserialize<FeaturedProductsConfig>(config), errors, c =>
                {
                    Heading(c.Title, c.Subtitle, errors);
                    Limit(c.Limit, 24, errors);
                    Cta(c.Cta, errors);
                }),
                SectionTypes.NewArrivals => Check(Deserialize<NewArrivalsConfig>(config), errors, c =>
                {
                    Heading(c.Title, c.Subtitle, errors);
                    Limit(c.Limit, 24, errors);
                    Cta(c.Cta, errors);
                }),
                SectionTypes.CollectionProducts => Check(Deserialize<CollectionProductsConfig>(config), errors, c =>
                {
                    Heading(c.Title, c.Subtitle, errors);
                    Limit(c.Limit, 24, errors);
                    Cta(c.Cta, errors);
                }),
                SectionTypes.ImageText => Check(Deserialize<ImageTextConfig>(config), errors, c =>
                {
                    Required(c.Heading, "Heading", 150, errors);
                    Max(c.Body, "Body", 3000, errors);
                    Max(c.Eyebrow, "Eyebrow", 80, errors);
                    Image(c.ImageUrl, errors);
                    Cta(c.Cta, errors);
                    OneOf(c.ImagePosition, "Image position", ["left", "right"], errors);
                    OneOf(c.Background, "Background", ["surface", "background", "dark"], errors);
                }),
                SectionTypes.PromoBanner => Check(Deserialize<PromoBannerConfig>(config), errors, c =>
                {
                    Required(c.Heading, "Heading", 150, errors);
                    Max(c.Text, "Text", 500, errors);
                    Max(c.Eyebrow, "Eyebrow", 80, errors);
                    Image(c.ImageUrl, errors);
                    Cta(c.Cta, errors);
                    OneOf(c.Theme, "Theme", ["dark", "light", "accent"], errors);
                }),
                SectionTypes.Testimonials => Check(Deserialize<TestimonialsConfig>(config), errors, c =>
                {
                    Heading(c.Title, c.Subtitle, errors);
                    Limit(c.Limit, 12, errors);
                }),
                SectionTypes.Newsletter => Check(Deserialize<NewsletterConfig>(config), errors, c =>
                {
                    Required(c.Title, "Title", 150, errors);
                    Max(c.Text, "Text", 400, errors);
                    Required(c.ButtonLabel, "Button label", 40, errors);
                }),
                SectionTypes.RichText => Check(Deserialize<RichTextConfig>(config), errors, c =>
                {
                    Max(c.Title, "Title", 150, errors);
                    Required(c.Html, "Content", 20000, errors);
                    OneOf(c.Width, "Width", ["narrow", "wide"], errors);
                }) with { Html = sanitizer.Sanitize(Deserialize<RichTextConfig>(config).Html) },
                SectionTypes.EraShowcase => Check(Deserialize<EraShowcaseConfig>(config), errors, c =>
                {
                    Heading(c.Title, c.Subtitle, errors);
                    if (c.Items is null || c.Items.Length is 0 or > 12) errors.Add("Add between 1 and 12 eras.");
                    foreach (var item in c.Items ?? [])
                    {
                        Required(item.Label, "Era label", 60, errors);
                        Image(item.ImageUrl, errors);
                        Link(item.Href, errors);
                    }
                }),
                SectionTypes.TrustFeatures => Check(Deserialize<TrustFeaturesConfig>(config), errors, c =>
                {
                    Max(c.Title, "Title", 150, errors);
                    if (c.Items is null || c.Items.Length is 0 or > 6) errors.Add("Add between 1 and 6 features.");
                    foreach (var item in c.Items ?? [])
                    {
                        OneOf(item.Icon, "Icon", TrustIcons, errors);
                        Required(item.Title, "Feature title", 80, errors);
                        Max(item.Text, "Feature text", 300, errors);
                    }
                }),
                _ => throw new RequestValidationException(new Dictionary<string, string[]> { [path] = [$"Unknown section type '{type}'."] })
            };
        }
        catch (JsonException)
        {
            errors.Add("Section settings are not in the expected format.");
            normalized = new { };
        }

        if (errors.Count > 0) throw new RequestValidationException(new Dictionary<string, string[]> { [path] = errors.ToArray() });
        return JsonSerializer.Serialize(normalized, normalized.GetType(), Json);
    }

    private static T Deserialize<T>(JsonElement e) =>
        e.Deserialize<T>(Json) ?? throw new JsonException("Empty configuration");

    private static T Check<T>(T config, List<string> errors, Action<T> validate)
    {
        validate(config);
        return config;
    }

    private static void Heading(string? title, string? subtitle, List<string> errors)
    {
        Required(title, "Title", 150, errors);
        Max(subtitle, "Subtitle", 400, errors);
    }

    private static void Required(string? value, string name, int max, List<string> errors)
    {
        if (string.IsNullOrWhiteSpace(value)) errors.Add($"{name} is required.");
        else Max(value, name, max, errors);
    }

    private static void Max(string? value, string name, int max, List<string> errors)
    {
        if (value is not null && value.Length > max) errors.Add($"{name} must be at most {max} characters.");
    }

    private static void Limit(int value, int max, List<string> errors)
    {
        if (value < 1 || value > max) errors.Add($"Number of items must be between 1 and {max}.");
    }

    private static void OneOf(string? value, string name, string[] allowed, List<string> errors)
    {
        if (value is null || !allowed.Contains(value)) errors.Add($"{name} must be one of: {string.Join(", ", allowed)}.");
    }

    private static void Image(string? url, List<string> errors)
    {
        if (!string.IsNullOrWhiteSpace(url) && !Text.IsSafeImageUrl(url)) errors.Add("Image URL must be an http(s) or site-relative URL.");
    }

    private static void Link(string? href, List<string> errors)
    {
        if (!Text.IsSafeLink(href)) errors.Add("Links must be site-relative paths (e.g. /shop) or http(s) URLs.");
    }

    private static void Cta(CtaLink? cta, List<string> errors)
    {
        if (cta is null) return;
        Required(cta.Label, "Button label", 40, errors);
        Link(cta.Href, errors);
    }
}
