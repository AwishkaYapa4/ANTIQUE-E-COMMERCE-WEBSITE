using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;

namespace AntiqueStore.Application.Common;

public static partial class Text
{
    public static string Slugify(string value)
    {
        var normalized = value.Normalize(NormalizationForm.FormD);
        var sb = new StringBuilder(normalized.Length);
        foreach (var c in normalized)
        {
            if (CharUnicodeInfo.GetUnicodeCategory(c) != UnicodeCategory.NonSpacingMark) sb.Append(c);
        }
        // Apostrophes join words ("planter's" → "planters") rather than splitting them.
        var cleaned = sb.ToString().ToLowerInvariant().Replace("'", "").Replace("’", "").Replace("&", " and ");
        var slug = NonSlugChars().Replace(cleaned, "-").Trim('-');
        slug = MultiDash().Replace(slug, "-");
        return slug.Length > 120 ? slug[..120].TrimEnd('-') : slug;
    }

    public static bool IsValidSlug(string? value) => value is not null && SlugPattern().IsMatch(value);

    /// <summary>Allows site-relative paths ("/shop"), anchors and http(s) URLs. Blocks javascript:, data:, etc.</summary>
    public static bool IsSafeLink(string? href)
    {
        if (string.IsNullOrWhiteSpace(href)) return false;
        href = href.Trim();
        if (href.StartsWith("//")) return false;
        if (href.StartsWith('/') || href.StartsWith('#')) return true;
        if (href.StartsWith("mailto:", StringComparison.OrdinalIgnoreCase) ||
            href.StartsWith("tel:", StringComparison.OrdinalIgnoreCase)) return true;
        return Uri.TryCreate(href, UriKind.Absolute, out var uri) && (uri.Scheme == Uri.UriSchemeHttps || uri.Scheme == Uri.UriSchemeHttp);
    }

    /// <summary>Image URLs: site-relative or http(s) only.</summary>
    public static bool IsSafeImageUrl(string? url) =>
        !string.IsNullOrWhiteSpace(url) && (url.StartsWith('/') && !url.StartsWith("//") ||
            Uri.TryCreate(url, UriKind.Absolute, out var uri) && (uri.Scheme == Uri.UriSchemeHttps || uri.Scheme == Uri.UriSchemeHttp));

    public static bool IsHexColor(string? value) => value is not null && HexColor().IsMatch(value);

    public static string? TrimToNull(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    [GeneratedRegex("[^a-z0-9]+")] private static partial Regex NonSlugChars();
    [GeneratedRegex("-{2,}")] private static partial Regex MultiDash();
    [GeneratedRegex("^[a-z0-9]+(?:-[a-z0-9]+)*$")] private static partial Regex SlugPattern();
    [GeneratedRegex("^#[0-9A-Fa-f]{6}$")] private static partial Regex HexColor();
}
