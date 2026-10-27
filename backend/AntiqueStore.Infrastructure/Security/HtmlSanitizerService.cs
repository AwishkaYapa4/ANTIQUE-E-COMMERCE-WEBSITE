using Ganss.Xss;
using AppSanitizer = AntiqueStore.Application.Common.IHtmlSanitizer;

namespace AntiqueStore.Infrastructure.Security;

/// <summary>
/// Allow-list HTML sanitizer for admin-authored rich text. Removes scripts, styles, event handlers,
/// iframes and any URL scheme other than http(s)/mailto/tel. External links get rel="noopener noreferrer".
/// </summary>
public sealed class HtmlSanitizerService : AppSanitizer
{
    private readonly HtmlSanitizer _sanitizer;

    public HtmlSanitizerService()
    {
        var options = new HtmlSanitizerOptions
        {
            AllowedTags = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
            {
                "p", "br", "hr", "h2", "h3", "h4", "strong", "b", "em", "i", "u", "s", "blockquote",
                "ul", "ol", "li", "a", "img", "figure", "figcaption", "table", "thead", "tbody", "tr", "th", "td", "span"
            },
            AllowedAttributes = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
            {
                "href", "title", "target", "rel", "src", "alt", "width", "height", "colspan", "rowspan"
            },
            AllowedSchemes = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "http", "https", "mailto", "tel" },
            UriAttributes = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "href", "src" },
            AllowedCssProperties = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        };
        _sanitizer = new HtmlSanitizer(options) { KeepChildNodes = true };
        _sanitizer.PostProcessNode += (_, e) =>
        {
            if (e.Node is AngleSharp.Dom.IElement { TagName: "A" } a)
            {
                var target = a.GetAttribute("target");
                if (target is not null && target != "_blank") a.RemoveAttribute("target");
                if (a.GetAttribute("target") == "_blank" || (a.GetAttribute("href")?.StartsWith("http", StringComparison.OrdinalIgnoreCase) ?? false))
                    a.SetAttribute("rel", "noopener noreferrer");
            }
            if (e.Node is AngleSharp.Dom.IElement { TagName: "IMG" } img)
                img.SetAttribute("loading", "lazy");
        };
    }

    public string Sanitize(string? html) => string.IsNullOrWhiteSpace(html) ? "" : _sanitizer.Sanitize(html).Trim();
}

