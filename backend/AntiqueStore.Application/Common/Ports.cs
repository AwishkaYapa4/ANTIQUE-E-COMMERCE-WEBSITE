using AntiqueStore.Domain.Enums;

namespace AntiqueStore.Application.Common;

/// <summary>The authenticated caller, resolved by the API from a verified Firebase token + MySQL profile.</summary>
public interface ICurrentUser
{
    bool IsAuthenticated { get; }
    int? UserId { get; }
    string? Email { get; }
    UserRole? Role { get; }
    bool IsAdmin => Role == UserRole.Admin;
    string? IpAddress { get; }

    int RequireUserId() => UserId ?? throw new ForbiddenException("Please sign in to continue.");
}

public interface IHtmlSanitizer
{
    /// <summary>Strips scripts, event handlers and unsafe URLs; keeps basic rich-text formatting.</summary>
    string Sanitize(string? html);
}

public sealed record StoredImage(
    string Provider, string StorageKey, string Url, IReadOnlyDictionary<int, string> Variants,
    int Width, int Height, long SizeBytes, string ContentType);

/// <summary>Public image storage (Cloudinary, S3-compatible or local disk for development).</summary>
public interface IImageStorage
{
    string ProviderName { get; }
    Task<StoredImage> UploadAsync(Stream processedImage, string fileName, string folder, int width, int height,
        CancellationToken ct);
    Task DeleteAsync(string storageKey, CancellationToken ct);
}

/// <summary>Private storage for sensitive uploads (payment proofs). Never publicly addressable.</summary>
public interface IPrivateFileStorage
{
    Task<string> SaveAsync(Stream content, string extension, string folder, CancellationToken ct);
    Task<Stream?> OpenReadAsync(string storageKey, CancellationToken ct);
    Task DeleteAsync(string storageKey, CancellationToken ct);
}

public sealed record ProcessedImage(Stream Content, int Width, int Height, string ContentType, string Extension);

/// <summary>Validates and re-encodes uploaded images (strips metadata, rejects non-images and oversize files).</summary>
public interface IImageProcessor
{
    Task<ProcessedImage> ProcessAsync(Stream input, long length, int maxDimension, CancellationToken ct);
}

public interface IAuditLogger
{
    /// <summary>Adds an audit entry to the current unit of work (saved with the caller's SaveChanges).</summary>
    void Log(string action, string entityType, object? entityId, string? summary = null, object? data = null);
}
