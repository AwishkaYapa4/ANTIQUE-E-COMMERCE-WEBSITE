using AntiqueStore.Application.Common;

namespace AntiqueStore.Tests;

public sealed class TextTests
{
    [Theory]
    [InlineData("Colonial Teak Planter's Chair", "colonial-teak-planters-chair")]
    [InlineData("Mirrors & Frames", "mirrors-and-frames")]
    [InlineData("  Café  Crème ", "cafe-creme")]
    [InlineData("Oil Painting — Hill Country", "oil-painting-hill-country")]
    public void Slugify_produces_clean_urls(string input, string expected) => Assert.Equal(expected, Text.Slugify(input));

    [Theory]
    [InlineData("/shop", true)]
    [InlineData("https://example.lk/a", true)]
    [InlineData("mailto:hi@example.lk", true)]
    [InlineData("javascript:alert(1)", false)]
    [InlineData("//evil.example", false)]
    [InlineData("data:text/html,x", false)]
    public void Only_safe_links_are_allowed(string href, bool safe) => Assert.Equal(safe, Text.IsSafeLink(href));
}
