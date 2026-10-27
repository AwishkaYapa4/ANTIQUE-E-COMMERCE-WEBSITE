using System.Text.Json;
using AntiqueStore.Application.Catalog;
using AntiqueStore.Application.Common;
using AntiqueStore.Application.Settings;
using AntiqueStore.Domain.Common;
using AntiqueStore.Domain.Entities;
using AntiqueStore.Domain.Enums;
using Microsoft.EntityFrameworkCore;

namespace AntiqueStore.Application.Customization;

/// <summary>
/// Draft/publish workflow for brand, theme, navigation and homepage. Admin edits only ever touch the
/// <see cref="ContentStage.Draft"/> rows; customers only ever read <see cref="ContentStage.Published"/> rows.
/// Publishing copies draft → published inside one transaction, so the live site never shows a half-published state.
/// </summary>
public sealed class CustomizationService(
    IAppDbContext db, IHtmlSanitizer sanitizer, IAuditLogger audit, CommerceSettingsService commerce, CatalogService catalog,
    TimeProvider clock)
{
    private static readonly JsonSerializerOptions Json = SectionConfigValidator.Json;

    // ---------- Reads ----------

    public async Task<SiteConfigDto> GetSiteConfigAsync(ContentStage stage, CancellationToken ct)
    {
        var brand = await GetStoreAsync(stage, ct);
        var theme = await GetThemeAsync(stage, ct);
        var nav = await GetNavigationAsync(stage, ct);
        var c = await commerce.GetAsync(ct);
        return new SiteConfigDto(ToDto(brand), ToDto(theme), nav,
            new PublicCommerceDto(c.Currency, c.CashOnDeliveryEnabled, c.BankTransferEnabled, c.LocalPickupEnabled,
                c.FreeShippingThreshold, c.ReservationMinutes, c.ReviewsEnabled, c.DeliveryInstructions, c.ReturnsSummary),
            stage == ContentStage.Draft);
    }

    public async Task<AdminCustomizationDto> GetDraftAsync(CancellationToken ct)
    {
        var brand = ToDto(await GetStoreAsync(ContentStage.Draft, ct));
        var theme = ToDto(await GetThemeAsync(ContentStage.Draft, ct));
        var nav = await GetNavigationAsync(ContentStage.Draft, ct);
        var sections = await GetSectionsAsync(ContentStage.Draft, ct);
        var lastPublished = await db.StoreSettings.AsNoTracking().Where(s => s.Stage == ContentStage.Published)
            .Select(s => (DateTime?)s.UpdatedAt).FirstOrDefaultAsync(ct);
        return new AdminCustomizationDto(brand, theme, nav, sections, await HasUnpublishedChangesAsync(ct), lastPublished,
            FontCatalog.Heading, FontCatalog.Body, SectionTypes.All);
    }

    public async Task<bool> HasUnpublishedChangesAsync(CancellationToken ct)
    {
        string Snapshot(object o) => JsonSerializer.Serialize(o, Json);
        var draft = Snapshot(new object[]
        {
            ToDto(await GetStoreAsync(ContentStage.Draft, ct)), ToDto(await GetThemeAsync(ContentStage.Draft, ct)),
            await GetNavigationAsync(ContentStage.Draft, ct), SectionSnapshot(await GetSectionsAsync(ContentStage.Draft, ct))
        });
        var published = Snapshot(new object[]
        {
            ToDto(await GetStoreAsync(ContentStage.Published, ct)), ToDto(await GetThemeAsync(ContentStage.Published, ct)),
            await GetNavigationAsync(ContentStage.Published, ct), SectionSnapshot(await GetSectionsAsync(ContentStage.Published, ct))
        });
        return draft != published;
    }

    private static object SectionSnapshot(IReadOnlyList<HomepageSectionDto> sections) =>
        sections.Select(s => new { s.Key, s.Type, s.IsVisible, Config = s.Config.GetRawText() }).ToList();

    public async Task<IReadOnlyList<HomepageSectionDto>> GetSectionsAsync(ContentStage stage, CancellationToken ct)
    {
        var rows = await db.HomepageSections.AsNoTracking().Where(s => s.Stage == stage).OrderBy(s => s.SortOrder).ToListAsync(ct);
        return rows.Select(s => new HomepageSectionDto(s.SectionKey, s.Type, s.SortOrder, s.IsVisible, ParseConfig(s.ConfigJson))).ToList();
    }

    public async Task<HomepageDto> GetHomepageAsync(ContentStage stage, CancellationToken ct)
    {
        var sections = await GetSectionsAsync(stage, ct);
        var rendered = new List<RenderedSectionDto>();
        foreach (var s in sections.Where(s => s.IsVisible))
            rendered.Add(await RenderAsync(s, ct));
        return new HomepageDto(rendered, stage == ContentStage.Draft);
    }

    private async Task<RenderedSectionDto> RenderAsync(HomepageSectionDto s, CancellationToken ct)
    {
        IReadOnlyList<ProductCardDto>? products = null;
        IReadOnlyList<CategoryDto>? categories = null;
        IReadOnlyList<TestimonialDto>? testimonials = null;
        var visible = catalog.VisibleProducts();

        switch (s.Type)
        {
            case SectionTypes.FeaturedProducts:
            {
                var c = s.Config.Deserialize<FeaturedProductsConfig>(Json)!;
                if (c.ProductIds is { Length: > 0 } ids)
                {
                    var cards = await CatalogService.FetchCardsAsync(visible.Where(p => ids.Contains(p.Id)), ct);
                    products = ids.Select(id => cards.FirstOrDefault(x => x.Id == id)).OfType<ProductCardDto>().Take(c.Limit).ToList();
                }
                else
                {
                    products = await catalog.GetCardsAsync(visible.Where(p => p.IsFeatured)
                        .OrderByDescending(p => p.QuantityAvailable > 0).ThenByDescending(p => p.PublishedAt), c.Limit, ct);
                }
                break;
            }
            case SectionTypes.NewArrivals:
            {
                var c = s.Config.Deserialize<NewArrivalsConfig>(Json)!;
                products = await catalog.GetCardsAsync(visible.Where(p => p.QuantityAvailable > 0).OrderByDescending(p => p.PublishedAt), c.Limit, ct);
                break;
            }
            case SectionTypes.CollectionProducts:
            {
                var c = s.Config.Deserialize<CollectionProductsConfig>(Json)!;
                products = c.CollectionId is { } cid
                    ? await catalog.GetCardsAsync(visible.Where(p => p.Collections.Any(pc => pc.CollectionId == cid))
                        .OrderByDescending(p => p.QuantityAvailable > 0).ThenByDescending(p => p.PublishedAt), c.Limit, ct)
                    : [];
                break;
            }
            case SectionTypes.FeaturedCategories:
            {
                var c = s.Config.Deserialize<FeaturedCategoriesConfig>(Json)!;
                var all = await catalog.GetCategoriesAsync(false, ct);
                categories = c.CategoryIds is { Length: > 0 } ids
                    ? ids.Select(id => all.FirstOrDefault(x => x.Id == id)).OfType<CategoryDto>().Take(c.Limit).ToList()
                    : all.Where(x => x.IsFeatured || all.All(a => !a.IsFeatured)).Where(x => x.ParentId is null).Take(c.Limit).ToList();
                break;
            }
            case SectionTypes.Testimonials:
            {
                var c = s.Config.Deserialize<TestimonialsConfig>(Json)!;
                testimonials = await db.Testimonials.AsNoTracking().Where(t => t.IsPublished)
                    .OrderBy(t => t.SortOrder).ThenByDescending(t => t.CreatedAt).Take(c.Limit)
                    .Select(t => new TestimonialDto(t.Id, t.Quote, t.AuthorName, t.AuthorLocation, t.Rating)).ToListAsync(ct);
                break;
            }
        }
        return new RenderedSectionDto(s.Key, s.Type, s.Config, products, categories, testimonials);
    }

    // ---------- Draft edits ----------

    public async Task<AdminCustomizationDto> SaveBrandAsync(BrandSettingsDto r, CancellationToken ct)
    {
        var s = await GetOrCreateStoreAsync(ContentStage.Draft, ct);
        s.StoreName = r.StoreName.Trim();
        s.Tagline = Text.TrimToNull(r.Tagline);
        s.Description = Text.TrimToNull(r.Description);
        s.LogoUrl = Text.TrimToNull(r.LogoUrl);
        s.FaviconUrl = Text.TrimToNull(r.FaviconUrl);
        s.ContactEmail = Text.TrimToNull(r.ContactEmail);
        s.ContactPhone = Text.TrimToNull(r.ContactPhone);
        s.WhatsAppNumber = Text.TrimToNull(r.WhatsAppNumber);
        s.Address = Text.TrimToNull(r.Address);
        s.OpeningHours = Text.TrimToNull(r.OpeningHours);
        s.MapEmbedUrl = Text.TrimToNull(r.MapEmbedUrl);
        s.SocialLinksJson = JsonSerializer.Serialize(r.SocialLinks.Where(l => Text.IsSafeLink(l.Url))
            .Select(l => new SocialLink(l.Platform.Trim().ToLowerInvariant(), l.Url.Trim())).ToList(), Json);
        s.AnnouncementEnabled = r.AnnouncementEnabled;
        s.AnnouncementText = Text.TrimToNull(r.AnnouncementText);
        s.AnnouncementLink = Text.TrimToNull(r.AnnouncementLink);
        s.FooterDescription = Text.TrimToNull(r.FooterDescription);
        s.CopyrightText = Text.TrimToNull(r.CopyrightText);
        s.NewsletterEnabled = r.NewsletterEnabled;
        s.SeoDefaultTitle = Text.TrimToNull(r.SeoDefaultTitle);
        s.SeoDefaultDescription = Text.TrimToNull(r.SeoDefaultDescription);
        s.DefaultOgImageUrl = Text.TrimToNull(r.DefaultOgImageUrl);
        audit.Log("customization.brand", "StoreSettings", "draft", "Saved brand settings draft");
        await db.SaveChangesAsync(ct);
        return await GetDraftAsync(ct);
    }

    public async Task<AdminCustomizationDto> SaveThemeAsync(ThemeDto r, CancellationToken ct)
    {
        var t = await GetOrCreateThemeAsync(ContentStage.Draft, ct);
        CopyTheme(r, t);
        audit.Log("customization.theme", "ThemeSettings", "draft", "Saved theme draft");
        await db.SaveChangesAsync(ct);
        return await GetDraftAsync(ct);
    }

    public async Task<AdminCustomizationDto> SaveNavigationAsync(SaveNavigationRequest r, CancellationToken ct)
    {
        foreach (var menu in r.Menus)
        {
            var row = await db.NavigationMenus.FirstOrDefaultAsync(m => m.Stage == ContentStage.Draft && m.Location == menu.Location, ct);
            if (row is null)
            {
                row = new NavigationMenu { Stage = ContentStage.Draft, Location = menu.Location };
                db.NavigationMenus.Add(row);
            }
            row.Title = menu.Title.Trim();
            row.ItemsJson = JsonSerializer.Serialize(menu.Items.Select(CleanNav).ToList(), Json);
        }
        audit.Log("customization.navigation", "NavigationMenu", "draft", "Saved navigation draft");
        await db.SaveChangesAsync(ct);
        return await GetDraftAsync(ct);
    }

    private static NavItem CleanNav(NavItem i) =>
        new(i.Label.Trim(), i.Href.Trim(), i.Children is { Count: > 0 } ? i.Children.Select(CleanNav).ToList() : null);

    public async Task<AdminCustomizationDto> SaveHomepageAsync(SaveHomepageRequest r, CancellationToken ct)
    {
        if (r.Sections.Count > 30) throw RequestValidationException.For("sections", "A homepage can have at most 30 sections.");
        var normalized = r.Sections.Select((s, i) => new
        {
            Key = string.IsNullOrWhiteSpace(s.Key) ? Guid.NewGuid().ToString("N") : s.Key.Trim(),
            s.Type, s.IsVisible, Order = i,
            Config = SectionConfigValidator.Normalize(s.Type, s.Config, sanitizer, $"sections[{i}]")
        }).ToList();
        if (normalized.Select(n => n.Key).Distinct().Count() != normalized.Count)
            throw RequestValidationException.For("sections", "Section keys must be unique.");

        await using var tx = await db.Database.BeginTransactionAsync(ct);
        await db.HomepageSections.Where(s => s.Stage == ContentStage.Draft).ExecuteDeleteAsync(ct);
        foreach (var n in normalized)
        {
            db.HomepageSections.Add(new HomepageSection
            {
                Stage = ContentStage.Draft, SectionKey = n.Key, Type = n.Type, IsVisible = n.IsVisible, SortOrder = n.Order, ConfigJson = n.Config
            });
        }
        audit.Log("customization.homepage", "HomepageSection", "draft", $"Saved homepage draft ({normalized.Count} sections)");
        await db.SaveChangesAsync(ct);
        await tx.CommitAsync(ct);
        return await GetDraftAsync(ct);
    }

    // ---------- Publish / discard ----------

    public Task<AdminCustomizationDto> PublishAsync(CancellationToken ct) =>
        CopyStageAsync(ContentStage.Draft, ContentStage.Published, "customization.publish", "Published website customization", ct);

    public Task<AdminCustomizationDto> DiscardDraftAsync(CancellationToken ct) =>
        CopyStageAsync(ContentStage.Published, ContentStage.Draft, "customization.discard", "Discarded draft changes", ct);

    private async Task<AdminCustomizationDto> CopyStageAsync(ContentStage from, ContentStage to, string action, string summary, CancellationToken ct)
    {
        await using var tx = await db.Database.BeginTransactionAsync(ct);

        var sourceStore = await GetStoreAsync(from, ct);
        var targetStore = await GetOrCreateStoreAsync(to, ct);
        CopyStore(sourceStore, targetStore);

        var sourceTheme = await GetThemeAsync(from, ct);
        var targetTheme = await GetOrCreateThemeAsync(to, ct);
        CopyTheme(ToDto(sourceTheme), targetTheme);

        var sourceMenus = await db.NavigationMenus.AsNoTracking().Where(m => m.Stage == from).ToListAsync(ct);
        await db.NavigationMenus.Where(m => m.Stage == to).ExecuteDeleteAsync(ct);
        foreach (var m in sourceMenus)
            db.NavigationMenus.Add(new NavigationMenu { Stage = to, Location = m.Location, Title = m.Title, ItemsJson = m.ItemsJson });

        var sourceSections = await db.HomepageSections.AsNoTracking().Where(s => s.Stage == from).ToListAsync(ct);
        await db.HomepageSections.Where(s => s.Stage == to).ExecuteDeleteAsync(ct);
        foreach (var s in sourceSections)
            db.HomepageSections.Add(new HomepageSection
            {
                Stage = to, SectionKey = s.SectionKey, Type = s.Type, SortOrder = s.SortOrder, IsVisible = s.IsVisible, ConfigJson = s.ConfigJson
            });

        targetStore.UpdatedAt = clock.GetUtcNow().UtcDateTime;
        audit.Log(action, "Customization", null, summary);
        await db.SaveChangesAsync(ct);
        await tx.CommitAsync(ct);
        return await GetDraftAsync(ct);
    }

    // ---------- Helpers ----------

    private async Task<StoreSettings> GetStoreAsync(ContentStage stage, CancellationToken ct) =>
        await db.StoreSettings.AsNoTracking().FirstOrDefaultAsync(s => s.Stage == stage, ct) ?? new StoreSettings { Stage = stage };

    private async Task<StoreSettings> GetOrCreateStoreAsync(ContentStage stage, CancellationToken ct)
    {
        var s = await db.StoreSettings.FirstOrDefaultAsync(x => x.Stage == stage, ct);
        if (s is null) { s = new StoreSettings { Stage = stage }; db.StoreSettings.Add(s); }
        return s;
    }

    private async Task<ThemeSettings> GetThemeAsync(ContentStage stage, CancellationToken ct) =>
        await db.ThemeSettings.AsNoTracking().FirstOrDefaultAsync(s => s.Stage == stage, ct) ?? new ThemeSettings { Stage = stage };

    private async Task<ThemeSettings> GetOrCreateThemeAsync(ContentStage stage, CancellationToken ct)
    {
        var t = await db.ThemeSettings.FirstOrDefaultAsync(x => x.Stage == stage, ct);
        if (t is null) { t = new ThemeSettings { Stage = stage }; db.ThemeSettings.Add(t); }
        return t;
    }

    private async Task<IReadOnlyList<NavigationMenuDto>> GetNavigationAsync(ContentStage stage, CancellationToken ct)
    {
        var rows = await db.NavigationMenus.AsNoTracking().Where(m => m.Stage == stage).ToListAsync(ct);
        return NavigationLocations.All.Select(loc =>
        {
            var row = rows.FirstOrDefault(r => r.Location == loc);
            var items = row is null ? [] : JsonSerializer.Deserialize<List<NavItem>>(row.ItemsJson, Json) ?? [];
            return new NavigationMenuDto(loc, row?.Title ?? "", items);
        }).ToList();
    }

    private static JsonElement ParseConfig(string json)
    {
        using var doc = JsonDocument.Parse(string.IsNullOrWhiteSpace(json) ? "{}" : json);
        return doc.RootElement.Clone();
    }

    private static BrandSettingsDto ToDto(StoreSettings s) => new(
        s.StoreName, s.Tagline, s.Description, s.LogoUrl, s.FaviconUrl, s.ContactEmail, s.ContactPhone, s.WhatsAppNumber,
        s.Address, s.OpeningHours, s.MapEmbedUrl,
        JsonSerializer.Deserialize<List<SocialLink>>(string.IsNullOrWhiteSpace(s.SocialLinksJson) ? "[]" : s.SocialLinksJson, Json) ?? [],
        s.AnnouncementEnabled, s.AnnouncementText, s.AnnouncementLink, s.FooterDescription, s.CopyrightText, s.NewsletterEnabled,
        s.SeoDefaultTitle, s.SeoDefaultDescription, s.DefaultOgImageUrl);

    private static ThemeDto ToDto(ThemeSettings t) => new(
        t.BackgroundColor, t.SurfaceColor, t.TextColor, t.MutedTextColor, t.PrimaryColor, t.AccentColor, t.SecondaryColor,
        t.BorderColor, t.ButtonColor, t.ButtonTextColor, t.SuccessColor, t.ErrorColor, t.HeadingFont, t.BodyFont,
        t.BaseFontSize, t.HeadingScale, t.BorderRadius);

    private static void CopyTheme(ThemeDto r, ThemeSettings t)
    {
        t.BackgroundColor = r.BackgroundColor.ToUpperInvariant();
        t.SurfaceColor = r.SurfaceColor.ToUpperInvariant();
        t.TextColor = r.TextColor.ToUpperInvariant();
        t.MutedTextColor = r.MutedTextColor.ToUpperInvariant();
        t.PrimaryColor = r.PrimaryColor.ToUpperInvariant();
        t.AccentColor = r.AccentColor.ToUpperInvariant();
        t.SecondaryColor = r.SecondaryColor.ToUpperInvariant();
        t.BorderColor = r.BorderColor.ToUpperInvariant();
        t.ButtonColor = r.ButtonColor.ToUpperInvariant();
        t.ButtonTextColor = r.ButtonTextColor.ToUpperInvariant();
        t.SuccessColor = r.SuccessColor.ToUpperInvariant();
        t.ErrorColor = r.ErrorColor.ToUpperInvariant();
        t.HeadingFont = r.HeadingFont;
        t.BodyFont = r.BodyFont;
        t.BaseFontSize = r.BaseFontSize;
        t.HeadingScale = r.HeadingScale;
        t.BorderRadius = r.BorderRadius;
    }

    private static void CopyStore(StoreSettings from, StoreSettings to)
    {
        to.StoreName = from.StoreName;
        to.Tagline = from.Tagline;
        to.Description = from.Description;
        to.LogoUrl = from.LogoUrl;
        to.FaviconUrl = from.FaviconUrl;
        to.ContactEmail = from.ContactEmail;
        to.ContactPhone = from.ContactPhone;
        to.WhatsAppNumber = from.WhatsAppNumber;
        to.Address = from.Address;
        to.OpeningHours = from.OpeningHours;
        to.MapEmbedUrl = from.MapEmbedUrl;
        to.SocialLinksJson = from.SocialLinksJson;
        to.AnnouncementEnabled = from.AnnouncementEnabled;
        to.AnnouncementText = from.AnnouncementText;
        to.AnnouncementLink = from.AnnouncementLink;
        to.FooterDescription = from.FooterDescription;
        to.CopyrightText = from.CopyrightText;
        to.NewsletterEnabled = from.NewsletterEnabled;
        to.SeoDefaultTitle = from.SeoDefaultTitle;
        to.SeoDefaultDescription = from.SeoDefaultDescription;
        to.DefaultOgImageUrl = from.DefaultOgImageUrl;
    }
}
