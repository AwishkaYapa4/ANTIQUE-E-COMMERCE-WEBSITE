using AntiqueStore.Application.Common;
using AntiqueStore.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace AntiqueStore.Application.Content;

public sealed record ContentPageDto(string Slug, string Title, string Body, string? SeoTitle, string? SeoDescription, DateTime? UpdatedAt);

public sealed record AdminContentPageDto(
    int Id, string Slug, string Title, string DraftTitle, string DraftBody, bool IsPublished, bool HasUnpublishedChanges,
    bool IsSystem, string? SeoTitle, string? SeoDescription, DateTime? PublishedAt, DateTime UpdatedAt);

public sealed record ContentPageUpsertRequest(string? Slug, string Title, string Body, string? SeoTitle, string? SeoDescription);

/// <summary>About, contact, policy and custom pages. Edits save as draft; publishing copies draft → live.</summary>
public sealed class ContentPageService(IAppDbContext db, IHtmlSanitizer sanitizer, IAuditLogger audit, TimeProvider clock)
{
    public async Task<ContentPageDto> GetPublishedAsync(string slug, CancellationToken ct) =>
        await db.ContentPages.AsNoTracking().Where(p => p.Slug == slug && p.IsPublished)
            .Select(p => new ContentPageDto(p.Slug, p.Title, p.Body, p.SeoTitle, p.SeoDescription, p.PublishedAt))
            .FirstOrDefaultAsync(ct) ?? throw new NotFoundException("Page", slug);

    public async Task<ContentPageDto> GetDraftPreviewAsync(string slug, CancellationToken ct) =>
        await db.ContentPages.AsNoTracking().Where(p => p.Slug == slug)
            .Select(p => new ContentPageDto(p.Slug, p.DraftTitle, p.DraftBody, p.SeoTitle, p.SeoDescription, p.UpdatedAt))
            .FirstOrDefaultAsync(ct) ?? throw new NotFoundException("Page", slug);

    public async Task<IReadOnlyList<AdminContentPageDto>> ListAsync(CancellationToken ct) =>
        await db.ContentPages.AsNoTracking().OrderByDescending(p => p.IsSystem).ThenBy(p => p.Title)
            .Select(p => ToDto(p)).ToListAsync(ct);

    public async Task<AdminContentPageDto> GetAsync(int id, CancellationToken ct) =>
        await db.ContentPages.AsNoTracking().Where(p => p.Id == id).Select(p => ToDto(p)).FirstOrDefaultAsync(ct)
        ?? throw new NotFoundException("Page", id);

    public async Task<AdminContentPageDto> CreateAsync(ContentPageUpsertRequest r, CancellationToken ct)
    {
        var slug = Text.Slugify(string.IsNullOrWhiteSpace(r.Slug) ? r.Title : r.Slug);
        if (await db.ContentPages.AnyAsync(p => p.Slug == slug, ct))
            throw RequestValidationException.For("slug", "A page with this URL already exists.");
        var page = new ContentPage { Slug = slug, Title = r.Title.Trim(), Body = "" };
        Apply(page, r);
        db.ContentPages.Add(page);
        await db.SaveChangesAsync(ct);
        audit.Log("page.create", "ContentPage", page.Id, $"Created page \"{page.DraftTitle}\"");
        await db.SaveChangesAsync(ct);
        return await GetAsync(page.Id, ct);
    }

    public async Task<AdminContentPageDto> SaveDraftAsync(int id, ContentPageUpsertRequest r, CancellationToken ct)
    {
        var page = await db.ContentPages.FirstOrDefaultAsync(p => p.Id == id, ct) ?? throw new NotFoundException("Page", id);
        if (!page.IsSystem && !string.IsNullOrWhiteSpace(r.Slug))
        {
            var slug = Text.Slugify(r.Slug);
            if (slug != page.Slug && await db.ContentPages.AnyAsync(p => p.Slug == slug, ct))
                throw RequestValidationException.For("slug", "A page with this URL already exists.");
            page.Slug = slug;
        }
        Apply(page, r);
        audit.Log("page.draft", "ContentPage", id, $"Saved draft of \"{page.DraftTitle}\"");
        await db.SaveChangesAsync(ct);
        return await GetAsync(id, ct);
    }

    public async Task<AdminContentPageDto> PublishAsync(int id, CancellationToken ct)
    {
        var page = await db.ContentPages.FirstOrDefaultAsync(p => p.Id == id, ct) ?? throw new NotFoundException("Page", id);
        page.Title = page.DraftTitle;
        page.Body = page.DraftBody;
        page.IsPublished = true;
        page.HasUnpublishedChanges = false;
        page.PublishedAt = clock.GetUtcNow().UtcDateTime;
        audit.Log("page.publish", "ContentPage", id, $"Published \"{page.Title}\"");
        await db.SaveChangesAsync(ct);
        return await GetAsync(id, ct);
    }

    public async Task<AdminContentPageDto> UnpublishAsync(int id, CancellationToken ct)
    {
        var page = await db.ContentPages.FirstOrDefaultAsync(p => p.Id == id, ct) ?? throw new NotFoundException("Page", id);
        if (page.IsSystem) throw new BusinessRuleException("Policy and system pages must stay published.", "system_page");
        page.IsPublished = false;
        audit.Log("page.unpublish", "ContentPage", id, $"Unpublished \"{page.Title}\"");
        await db.SaveChangesAsync(ct);
        return await GetAsync(id, ct);
    }

    public async Task<AdminContentPageDto> DiscardDraftAsync(int id, CancellationToken ct)
    {
        var page = await db.ContentPages.FirstOrDefaultAsync(p => p.Id == id, ct) ?? throw new NotFoundException("Page", id);
        page.DraftTitle = page.Title;
        page.DraftBody = page.Body;
        page.HasUnpublishedChanges = false;
        await db.SaveChangesAsync(ct);
        return await GetAsync(id, ct);
    }

    public async Task DeleteAsync(int id, CancellationToken ct)
    {
        var page = await db.ContentPages.FirstOrDefaultAsync(p => p.Id == id, ct) ?? throw new NotFoundException("Page", id);
        if (page.IsSystem) throw new BusinessRuleException("System pages cannot be deleted.", "system_page");
        db.ContentPages.Remove(page);
        audit.Log("page.delete", "ContentPage", id, $"Deleted page \"{page.Title}\"");
        await db.SaveChangesAsync(ct);
    }

    private void Apply(ContentPage page, ContentPageUpsertRequest r)
    {
        page.DraftTitle = r.Title.Trim();
        page.DraftBody = sanitizer.Sanitize(r.Body);
        page.SeoTitle = Text.TrimToNull(r.SeoTitle);
        page.SeoDescription = Text.TrimToNull(r.SeoDescription);
        page.HasUnpublishedChanges = page.DraftTitle != page.Title || page.DraftBody != page.Body;
    }

    private static AdminContentPageDto ToDto(ContentPage p) => new(
        p.Id, p.Slug, p.Title, p.DraftTitle, p.DraftBody, p.IsPublished, p.HasUnpublishedChanges, p.IsSystem,
        p.SeoTitle, p.SeoDescription, p.PublishedAt, p.UpdatedAt);
}
