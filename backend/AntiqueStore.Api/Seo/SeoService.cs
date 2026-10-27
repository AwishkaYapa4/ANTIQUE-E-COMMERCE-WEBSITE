using System.Net;
using System.Text;
using System.Text.Json;
using System.Xml;
using AntiqueStore.Application.Catalog;
using AntiqueStore.Application.Common;
using AntiqueStore.Application.Customization;
using AntiqueStore.Domain.Common;
using AntiqueStore.Domain.Enums;
using Microsoft.EntityFrameworkCore;

namespace AntiqueStore.Api.Seo;

public sealed class SeoOptions
{
    public const string Section = "Seo";
    /// <summary>Public origin, e.g. https://www.example.lk (used for canonical URLs and the sitemap).</summary>
    public string? PublicBaseUrl { get; set; }
}

/// <summary>
/// Crawlable rendering strategy for a Vite SPA: when ASP.NET serves the built index.html it injects
/// route-specific &lt;title&gt;, meta description, canonical, Open Graph and JSON-LD (Product) tags server-side,
/// so crawlers and link previews get correct metadata without executing JavaScript. The client removes the
/// server tags (data-seo="server") once React takes over and manages metadata itself.
/// </summary>
public sealed class SeoService(IAppDbContext db, CustomizationService customization, CatalogService catalog, IConfiguration config)
{
    private static readonly string[] NoIndexPrefixes = ["/admin", "/account", "/checkout", "/cart", "/login", "/register", "/forgot-password", "/verify-email", "/order-confirmation"];

    private string BaseUrl(HttpRequest request) =>
        (string.IsNullOrWhiteSpace(config[$"{SeoOptions.Section}:PublicBaseUrl"]) ? $"{request.Scheme}://{request.Host}" : config[$"{SeoOptions.Section}:PublicBaseUrl"]!).TrimEnd('/');

    public async Task<string> RenderHeadAsync(HttpRequest request, CancellationToken ct)
    {
        var path = request.Path.Value ?? "/";
        var baseUrl = BaseUrl(request);
        var site = await customization.GetSiteConfigAsync(ContentStage.Published, ct);
        var storeName = site.Brand.StoreName;

        string title = site.Brand.SeoDefaultTitle ?? storeName;
        string? description = site.Brand.SeoDefaultDescription ?? site.Brand.Description;
        string? image = site.Brand.DefaultOgImageUrl;
        string type = "website";
        string? jsonLd = null;
        var noIndex = NoIndexPrefixes.Any(p => path.StartsWith(p, StringComparison.OrdinalIgnoreCase));

        try
        {
            if (path.StartsWith("/products/", StringComparison.OrdinalIgnoreCase))
            {
                var slug = path["/products/".Length..].Trim('/');
                var p = await catalog.GetBySlugAsync(slug, ct);
                title = $"{p.SeoTitle ?? p.Name} | {storeName}";
                description = p.SeoDescription ?? p.ShortDescription;
                image = p.Images.FirstOrDefault()?.Url ?? image;
                type = "product";
                jsonLd = JsonSerializer.Serialize(new Dictionary<string, object?>
                {
                    ["@context"] = "https://schema.org",
                    ["@type"] = "Product",
                    ["name"] = p.Name,
                    ["sku"] = p.Sku,
                    ["description"] = p.ShortDescription,
                    ["image"] = p.Images.Select(i => Absolute(baseUrl, i.Url)).ToArray(),
                    ["category"] = p.Category?.Name,
                    ["material"] = p.Material,
                    ["offers"] = new Dictionary<string, object?>
                    {
                        ["@type"] = "Offer",
                        ["priceCurrency"] = p.Currency,
                        ["price"] = p.Price.ToString("0.00", System.Globalization.CultureInfo.InvariantCulture),
                        ["availability"] = p.InventoryState == InventoryState.Available ? "https://schema.org/InStock" : "https://schema.org/SoldOut",
                        ["itemCondition"] = "https://schema.org/UsedCondition",
                        ["url"] = $"{baseUrl}/products/{p.Slug}"
                    }
                });
            }
            else if (path.StartsWith("/category/", StringComparison.OrdinalIgnoreCase))
            {
                var c = await catalog.GetCategoryAsync(path["/category/".Length..].Trim('/'), ct);
                title = $"{c.SeoTitle ?? c.Name} | {storeName}";
                description = c.SeoDescription ?? c.Description;
                image = c.ImageUrl ?? image;
            }
            else if (path.StartsWith("/collections/", StringComparison.OrdinalIgnoreCase))
            {
                var c = await catalog.GetCollectionAsync(path["/collections/".Length..].Trim('/'), ct);
                title = $"{c.SeoTitle ?? c.Name} | {storeName}";
                description = c.SeoDescription ?? c.Description;
                image = c.ImageUrl ?? image;
            }
            else if (path.StartsWith("/pages/", StringComparison.OrdinalIgnoreCase))
            {
                var slug = path["/pages/".Length..].Trim('/');
                var page = await db.ContentPages.AsNoTracking().Where(x => x.Slug == slug && x.IsPublished)
                    .Select(x => new { x.Title, x.SeoTitle, x.SeoDescription }).FirstOrDefaultAsync(ct);
                if (page is not null)
                {
                    title = $"{page.SeoTitle ?? page.Title} | {storeName}";
                    description = page.SeoDescription ?? description;
                }
            }
            else if (path.Equals("/shop", StringComparison.OrdinalIgnoreCase))
            {
                title = $"Shop Antiques & Vintage | {storeName}";
            }
            else if (path.Equals("/collections", StringComparison.OrdinalIgnoreCase))
            {
                title = $"Collections | {storeName}";
            }
            else if (path.Equals("/contact", StringComparison.OrdinalIgnoreCase))
            {
                title = $"Contact Us | {storeName}";
            }
        }
        catch (NotFoundException)
        {
            noIndex = true;
            title = $"Not found | {storeName}";
        }

        var canonical = baseUrl + (path == "/" ? "/" : path.TrimEnd('/'));
        var sb = new StringBuilder();
        sb.Append($"<title data-seo=\"server\">{E(title)}</title>");
        if (description is not null) sb.Append($"<meta data-seo=\"server\" name=\"description\" content=\"{E(Truncate(description, 300))}\">");
        if (noIndex) sb.Append("<meta data-seo=\"server\" name=\"robots\" content=\"noindex, nofollow\">");
        else sb.Append($"<link data-seo=\"server\" rel=\"canonical\" href=\"{E(canonical)}\">");
        sb.Append($"<meta data-seo=\"server\" property=\"og:site_name\" content=\"{E(storeName)}\">");
        sb.Append($"<meta data-seo=\"server\" property=\"og:title\" content=\"{E(title)}\">");
        sb.Append($"<meta data-seo=\"server\" property=\"og:type\" content=\"{type}\">");
        sb.Append($"<meta data-seo=\"server\" property=\"og:url\" content=\"{E(canonical)}\">");
        if (description is not null) sb.Append($"<meta data-seo=\"server\" property=\"og:description\" content=\"{E(Truncate(description, 300))}\">");
        if (image is not null) sb.Append($"<meta data-seo=\"server\" property=\"og:image\" content=\"{E(Absolute(baseUrl, image))}\">");
        sb.Append("<meta data-seo=\"server\" name=\"twitter:card\" content=\"summary_large_image\">");
        if (site.Brand.FaviconUrl is { } favicon) sb.Append($"<link data-seo=\"server\" rel=\"icon\" href=\"{E(favicon)}\">");
        if (jsonLd is not null)
            sb.Append($"<script data-seo=\"server\" type=\"application/ld+json\">{jsonLd.Replace("</", "<\\/")}</script>");
        return sb.ToString();
    }

    public async Task<string> RenderSitemapAsync(HttpRequest request, CancellationToken ct)
    {
        var baseUrl = BaseUrl(request);
        var products = await catalog.VisibleProducts().OrderByDescending(p => p.UpdatedAt).Take(45000)
            .Select(p => new { p.Slug, p.UpdatedAt }).ToListAsync(ct);
        var categories = await db.Categories.AsNoTracking().Where(c => c.IsVisible).Select(c => new { c.Slug, c.UpdatedAt }).ToListAsync(ct);
        var collections = await db.Collections.AsNoTracking().Where(c => c.IsVisible).Select(c => new { c.Slug, c.UpdatedAt }).ToListAsync(ct);
        var pages = await db.ContentPages.AsNoTracking().Where(p => p.IsPublished).Select(p => new { p.Slug, p.UpdatedAt }).ToListAsync(ct);

        var settings = new XmlWriterSettings { Async = true, Indent = false, Encoding = new UTF8Encoding(false) };
        await using var output = new Utf8StringWriter();
        await using (var w = XmlWriter.Create(output, settings))
        {
            await w.WriteStartDocumentAsync();
            w.WriteStartElement("urlset", "http://www.sitemaps.org/schemas/sitemap/0.9");
            void Url(string loc, DateTime? lastMod = null)
            {
                w.WriteStartElement("url");
                w.WriteElementString("loc", baseUrl + loc);
                if (lastMod is { } lm) w.WriteElementString("lastmod", lm.ToString("yyyy-MM-dd"));
                w.WriteEndElement();
            }
            Url("/");
            Url("/shop");
            Url("/collections");
            Url("/contact");
            foreach (var c in categories) Url($"/category/{c.Slug}", c.UpdatedAt);
            foreach (var c in collections) Url($"/collections/{c.Slug}", c.UpdatedAt);
            foreach (var p in pages) Url($"/pages/{p.Slug}", p.UpdatedAt);
            foreach (var p in products) Url($"/products/{p.Slug}", p.UpdatedAt);
            w.WriteEndElement();
            await w.WriteEndDocumentAsync();
        }
        return output.ToString();
    }

    public string RenderRobots(HttpRequest request) =>
        $"""
        User-agent: *
        Disallow: /admin
        Disallow: /account
        Disallow: /checkout
        Disallow: /cart
        Disallow: /api/
        Allow: /api/products
        Sitemap: {BaseUrl(request)}/sitemap.xml
        """;

    private static string E(string value) => WebUtility.HtmlEncode(value);

    private static string Truncate(string value, int max) => value.Length <= max ? value : value[..(max - 1)] + "â€¦";

    private static string Absolute(string baseUrl, string url) => url.StartsWith('/') ? baseUrl + url : url;
}

/// <summary>StringWriter that reports UTF-8 so the XML declaration matches the HTTP response encoding.</summary>
internal sealed class Utf8StringWriter : StringWriter
{
    public override Encoding Encoding => new UTF8Encoding(false);
}
