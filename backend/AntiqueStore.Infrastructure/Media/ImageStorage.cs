using System.Security.Cryptography;
using AntiqueStore.Application.Common;
using CloudinaryDotNet;
using CloudinaryDotNet.Actions;
using Microsoft.Extensions.Options;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Formats.Webp;
using SixLabors.ImageSharp.Processing;

namespace AntiqueStore.Infrastructure.Media;

public sealed class StorageOptions
{
    public const string Section = "Storage";
    /// <summary>"Local" (development) or "Cloudinary".</summary>
    public string Provider { get; set; } = "Local";
    public string LocalRoot { get; set; } = "wwwroot/uploads";
    public string LocalPublicPath { get; set; } = "/uploads";
    public string PrivateRoot { get; set; } = "App_Data/private";
    public string? CloudinaryCloudName { get; set; }
    public string? CloudinaryApiKey { get; set; }
    public string? CloudinaryApiSecret { get; set; }
    public string CloudinaryFolder { get; set; } = "antique-store";
}

internal static class ImageVariants
{
    public static readonly int[] Widths = [400, 800, 1600];
}

/// <summary>Development storage on local disk. Not suitable for multi-instance production hosting.</summary>
public sealed class LocalImageStorage(IOptions<StorageOptions> options, Microsoft.Extensions.Hosting.IHostEnvironment env) : IImageStorage
{
    private readonly StorageOptions _o = options.Value;
    public string ProviderName => "local";

    private string Root => Path.GetFullPath(Path.Combine(env.ContentRootPath, _o.LocalRoot));

    public async Task<StoredImage> UploadAsync(Stream processedImage, string fileName, string folder, int width, int height, CancellationToken ct)
    {
        var id = Convert.ToHexString(RandomNumberGenerator.GetBytes(8)).ToLowerInvariant();
        var baseName = $"{Path.GetFileNameWithoutExtension(fileName)}-{id}";
        var dir = Path.Combine(Root, folder);
        Directory.CreateDirectory(dir);

        var masterPath = Path.Combine(dir, baseName + ".webp");
        await using (var file = File.Create(masterPath))
            await processedImage.CopyToAsync(file, ct);

        var variants = new Dictionary<int, string>();
        using (var image = await Image.LoadAsync(masterPath, ct))
        {
            foreach (var w in ImageVariants.Widths.Where(w => w < image.Width))
            {
                using var clone = image.Clone(x => x.Resize(new ResizeOptions { Mode = ResizeMode.Max, Size = new SixLabors.ImageSharp.Size(w, 0) }));
                var variantName = $"{baseName}-{w}.webp";
                await clone.SaveAsWebpAsync(Path.Combine(dir, variantName), new WebpEncoder { Quality = 80 }, ct);
                variants[w] = $"{_o.LocalPublicPath}/{folder}/{variantName}";
            }
        }
        var url = $"{_o.LocalPublicPath}/{folder}/{baseName}.webp";
        variants[width] = url;
        return new StoredImage(ProviderName, $"{folder}/{baseName}", url, variants, width, height, new FileInfo(masterPath).Length, "image/webp");
    }

    public Task DeleteAsync(string storageKey, CancellationToken ct)
    {
        var folder = Path.GetDirectoryName(storageKey) ?? "";
        var name = Path.GetFileName(storageKey);
        var dir = Path.GetFullPath(Path.Combine(Root, folder));
        if (!dir.StartsWith(Root, StringComparison.OrdinalIgnoreCase) || !Directory.Exists(dir)) return Task.CompletedTask;
        foreach (var file in Directory.EnumerateFiles(dir, name + "*.webp"))
            File.Delete(file);
        return Task.CompletedTask;
    }
}

/// <summary>Cloudinary storage. Responsive variants are delivered via on-the-fly URL transformations.</summary>
public sealed class CloudinaryImageStorage : IImageStorage
{
    private readonly Cloudinary _cloudinary;
    private readonly StorageOptions _o;
    public string ProviderName => "cloudinary";

    public CloudinaryImageStorage(IOptions<StorageOptions> options)
    {
        _o = options.Value;
        if (string.IsNullOrWhiteSpace(_o.CloudinaryCloudName) || string.IsNullOrWhiteSpace(_o.CloudinaryApiKey) ||
            string.IsNullOrWhiteSpace(_o.CloudinaryApiSecret))
            throw new InvalidOperationException("Cloudinary storage selected but Storage:Cloudinary* settings are missing.");
        _cloudinary = new Cloudinary(new Account(_o.CloudinaryCloudName, _o.CloudinaryApiKey, _o.CloudinaryApiSecret)) { Api = { Secure = true } };
    }

    public async Task<StoredImage> UploadAsync(Stream processedImage, string fileName, string folder, int width, int height, CancellationToken ct)
    {
        var result = await _cloudinary.UploadAsync(new ImageUploadParams
        {
            File = new FileDescription(fileName, processedImage),
            Folder = $"{_o.CloudinaryFolder}/{folder}",
            UseFilename = true,
            UniqueFilename = true,
            Overwrite = false
        }, ct);
        if (result.Error is not null) throw new InvalidOperationException($"Image upload failed: {result.Error.Message}");

        var variants = ImageVariants.Widths.Where(w => w < result.Width).ToDictionary(w => w, w =>
            _cloudinary.Api.UrlImgUp.Secure(true)
                .Transform(new Transformation().Width(w).Crop("limit").Quality("auto").FetchFormat("auto"))
                .BuildUrl(result.PublicId));
        var url = result.SecureUrl.ToString();
        variants[result.Width] = url;
        return new StoredImage(ProviderName, result.PublicId, url, variants, result.Width, result.Height, result.Bytes, "image/webp");
    }

    public async Task DeleteAsync(string storageKey, CancellationToken ct)
    {
        var result = await _cloudinary.DestroyAsync(new DeletionParams(storageKey) { ResourceType = ResourceType.Image });
        if (result.Error is not null) throw new InvalidOperationException($"Image deletion failed: {result.Error.Message}");
    }
}

/// <summary>Private files (payment proofs) stored outside the web root and streamed only via authorized endpoints.</summary>
public sealed class LocalPrivateFileStorage(IOptions<StorageOptions> options, Microsoft.Extensions.Hosting.IHostEnvironment env) : IPrivateFileStorage
{
    private string Root => Path.GetFullPath(Path.Combine(env.ContentRootPath, options.Value.PrivateRoot));

    public async Task<string> SaveAsync(Stream content, string extension, string folder, CancellationToken ct)
    {
        if (!System.Text.RegularExpressions.Regex.IsMatch(folder, "^[a-z0-9-]+$")) throw new ArgumentException("Invalid folder", nameof(folder));
        var key = $"{folder}/{Guid.NewGuid():N}{extension}";
        var path = Resolve(key);
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        content.Position = 0;
        await using var file = File.Create(path);
        await content.CopyToAsync(file, ct);
        return key;
    }

    public Task<Stream?> OpenReadAsync(string storageKey, CancellationToken ct)
    {
        var path = Resolve(storageKey);
        return Task.FromResult<Stream?>(File.Exists(path) ? File.OpenRead(path) : null);
    }

    public Task DeleteAsync(string storageKey, CancellationToken ct)
    {
        var path = Resolve(storageKey);
        if (File.Exists(path)) File.Delete(path);
        return Task.CompletedTask;
    }

    private string Resolve(string key)
    {
        var path = Path.GetFullPath(Path.Combine(Root, key));
        if (!path.StartsWith(Root + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase))
            throw new UnauthorizedAccessException("Invalid storage key.");
        return path;
    }
}

