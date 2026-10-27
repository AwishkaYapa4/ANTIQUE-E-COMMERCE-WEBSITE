using System.Text.Json;
using AntiqueStore.Application.Catalog;
using AntiqueStore.Application.Common;
using AntiqueStore.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace AntiqueStore.Application.Media;

public sealed record MediaAssetDto(
    int Id, string Url, string? SrcSet, string FileName, string ContentType, long SizeBytes, int Width, int Height,
    string? AltText, string? Title, string Folder, DateTime CreatedAt, bool IsInUse);

public sealed record MediaQuery : PageRequest
{
    public string? Q { get; init; }
    public string? Folder { get; init; }
    public bool? Unused { get; init; }
}

public sealed record UpdateMediaRequest(string? AltText, string? Title);

public sealed record MediaUpload(Stream Content, string FileName, string ContentType, long Length);

public sealed class MediaService(
    IAppDbContext db, IImageStorage storage, IImageProcessor processor, IAuditLogger audit, ICurrentUser currentUser,
    ILogger<MediaService> logger)
{
    public const long MaxUploadBytes = 10 * 1024 * 1024;
    public const int MaxDimension = 2400;
    public static readonly string[] AllowedContentTypes = ["image/jpeg", "image/png", "image/webp"];
    public static readonly string[] Folders = ["products", "categories", "homepage", "branding", "general"];

    public async Task<PagedResult<MediaAssetDto>> ListAsync(MediaQuery q, CancellationToken ct)
    {
        var assets = db.MediaAssets.AsNoTracking();
        if (!string.IsNullOrWhiteSpace(q.Q))
        {
            var term = q.Q.Trim();
            assets = assets.Where(a => a.FileName.Contains(term) || (a.AltText != null && a.AltText.Contains(term)) ||
                                       (a.Title != null && a.Title.Contains(term)));
        }
        if (!string.IsNullOrWhiteSpace(q.Folder)) assets = assets.Where(a => a.Folder == q.Folder);
        if (q.Unused == true) assets = assets.Where(a => !db.ProductImages.Any(i => i.MediaAssetId == a.Id));

        var page = await assets.OrderByDescending(a => a.CreatedAt).ToPagedAsync(q, x => x, ct);
        var usage = await GetUsageAsync(page.Items, ct);
        return new PagedResult<MediaAssetDto>(page.Items.Select(a => ToDto(a, usage.Contains(a.Id))).ToList(), page.Page, page.PageSize, page.TotalCount);
    }

    public async Task<MediaAssetDto> UploadAsync(MediaUpload upload, string? folder, string? altText, CancellationToken ct)
    {
        if (upload.Length <= 0) throw RequestValidationException.For("file", "The file is empty.");
        if (upload.Length > MaxUploadBytes) throw RequestValidationException.For("file", "Images must be smaller than 10 MB.");
        if (!AllowedContentTypes.Contains(upload.ContentType.ToLowerInvariant()))
            throw RequestValidationException.For("file", "Only JPG, PNG and WEBP images are allowed.");
        folder = Folders.Contains(folder) ? folder! : "general";

        // Decoding + re-encoding rejects non-images and strips EXIF/GPS metadata and embedded payloads.
        var processed = await processor.ProcessAsync(upload.Content, upload.Length, MaxDimension, ct);
        await using var content = processed.Content;
        var safeName = Path.GetFileNameWithoutExtension(upload.FileName);
        safeName = Text.Slugify(safeName);
        if (safeName.Length == 0) safeName = "image";

        var stored = await storage.UploadAsync(content, safeName + processed.Extension, folder, processed.Width, processed.Height, ct);
        var asset = new MediaAsset
        {
            Provider = stored.Provider, StorageKey = stored.StorageKey, Url = stored.Url,
            VariantsJson = JsonSerializer.Serialize(stored.Variants.ToDictionary(k => k.Key.ToString(), v => v.Value)),
            FileName = safeName + processed.Extension, ContentType = stored.ContentType, SizeBytes = stored.SizeBytes,
            Width = stored.Width, Height = stored.Height, AltText = Text.TrimToNull(altText), Folder = folder,
            UploadedByUserId = currentUser.UserId
        };
        db.MediaAssets.Add(asset);
        try
        {
            await db.SaveChangesAsync(ct);
        }
        catch
        {
            await storage.DeleteAsync(stored.StorageKey, CancellationToken.None);
            throw;
        }
        audit.Log("media.upload", "MediaAsset", asset.Id, $"Uploaded {asset.FileName}");
        await db.SaveChangesAsync(ct);
        return ToDto(asset, false);
    }

    public async Task<MediaAssetDto> UpdateAsync(int id, UpdateMediaRequest r, CancellationToken ct)
    {
        var asset = await db.MediaAssets.FirstOrDefaultAsync(a => a.Id == id, ct) ?? throw new NotFoundException("Image", id);
        asset.AltText = Text.TrimToNull(r.AltText);
        asset.Title = Text.TrimToNull(r.Title);
        await db.SaveChangesAsync(ct);
        var usage = await GetUsageAsync([asset], ct);
        return ToDto(asset, usage.Contains(id));
    }

    /// <summary>Deletes an image only when nothing on the site references it.</summary>
    public async Task DeleteAsync(int id, CancellationToken ct)
    {
        var asset = await db.MediaAssets.FirstOrDefaultAsync(a => a.Id == id, ct) ?? throw new NotFoundException("Image", id);
        var usage = await GetUsageAsync([asset], ct);
        if (usage.Contains(id))
            throw new BusinessRuleException("This image is in use on the website. Remove it from products and pages first.", "media_in_use");

        db.MediaAssets.Remove(asset);
        audit.Log("media.delete", "MediaAsset", id, $"Deleted {asset.FileName}");
        await db.SaveChangesAsync(ct);
        try
        {
            await storage.DeleteAsync(asset.StorageKey, ct);
        }
        catch (Exception ex)
        {
            // The DB record is gone; an orphaned file in storage is harmless but should be visible in logs.
            logger.LogWarning(ex, "Failed to delete stored file {StorageKey} for media asset {Id}", asset.StorageKey, id);
        }
    }

    /// <summary>Finds which assets are referenced by products, categories, collections, branding or homepage sections.</summary>
    private async Task<HashSet<int>> GetUsageAsync(IReadOnlyCollection<MediaAsset> assets, CancellationToken ct)
    {
        if (assets.Count == 0) return [];
        var ids = assets.Select(a => a.Id).ToList();
        var urls = assets.Select(a => a.Url).ToList();
        var used = (await db.ProductImages.AsNoTracking()
            .Where(i => (i.MediaAssetId != null && ids.Contains(i.MediaAssetId.Value)) || urls.Contains(i.Url))
            .Select(i => new { i.MediaAssetId, i.Url }).ToListAsync(ct))
            .Select(i => i.MediaAssetId ?? assets.First(a => a.Url == i.Url).Id).ToHashSet();

        var referencedUrls = new List<string?>();
        referencedUrls.AddRange(await db.Categories.AsNoTracking().Where(c => c.ImageUrl != null && urls.Contains(c.ImageUrl)).Select(c => c.ImageUrl).ToListAsync(ct));
        referencedUrls.AddRange(await db.Collections.AsNoTracking().Where(c => c.ImageUrl != null && urls.Contains(c.ImageUrl)).Select(c => c.ImageUrl).ToListAsync(ct));
        var store = await db.StoreSettings.AsNoTracking().Select(s => new { s.LogoUrl, s.FaviconUrl, s.DefaultOgImageUrl }).ToListAsync(ct);
        referencedUrls.AddRange(store.SelectMany(s => new[] { s.LogoUrl, s.FaviconUrl, s.DefaultOgImageUrl }));
        var sectionJson = await db.HomepageSections.AsNoTracking().Select(s => s.ConfigJson).ToListAsync(ct);
        foreach (var a in assets)
        {
            if (referencedUrls.Contains(a.Url) || sectionJson.Any(j => j.Contains(a.Url, StringComparison.Ordinal)))
                used.Add(a.Id);
        }
        return used;
    }

    private static MediaAssetDto ToDto(MediaAsset a, bool inUse) => new(
        a.Id, a.Url, ImageDto.BuildSrcSet(a.VariantsJson), a.FileName, a.ContentType, a.SizeBytes, a.Width, a.Height,
        a.AltText, a.Title, a.Folder, a.CreatedAt, inUse);
}
