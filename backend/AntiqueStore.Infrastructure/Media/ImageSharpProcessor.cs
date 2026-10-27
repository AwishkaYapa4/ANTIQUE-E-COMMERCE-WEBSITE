using AntiqueStore.Application.Common;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Formats;
using SixLabors.ImageSharp.Formats.Webp;
using SixLabors.ImageSharp.Processing;

namespace AntiqueStore.Infrastructure.Media;

/// <summary>
/// Decodes uploads with ImageSharp (rejecting anything that isn't a real JPEG/PNG/WEBP), guards against
/// decompression bombs, auto-orients, strips all metadata (EXIF GPS etc.), downsizes and re-encodes to WebP.
/// Note: ImageSharp is under the Six Labors Split License (free below USD 1M annual revenue).
/// </summary>
public sealed class ImageSharpProcessor : IImageProcessor
{
    private const int MaxSourcePixels = 50_000_000;

    public async Task<ProcessedImage> ProcessAsync(Stream input, long length, int maxDimension, CancellationToken ct)
    {
        await using var buffer = new MemoryStream((int)Math.Min(length, int.MaxValue));
        await input.CopyToAsync(buffer, ct);
        buffer.Position = 0;

        ImageInfo info;
        try
        {
            info = await Image.IdentifyAsync(buffer, ct);
        }
        catch (Exception ex) when (ex is UnknownImageFormatException or InvalidImageContentException or NotSupportedException)
        {
            throw RequestValidationException.For("file", "The file is not a valid image.");
        }
        var format = info.Metadata.DecodedImageFormat?.Name?.ToUpperInvariant();
        if (format is not ("JPEG" or "PNG" or "WEBP"))
            throw RequestValidationException.For("file", "Only JPG, PNG and WEBP images are allowed.");
        if ((long)info.Width * info.Height > MaxSourcePixels)
            throw RequestValidationException.For("file", "The image resolution is too large.");

        buffer.Position = 0;
        using var image = await Image.LoadAsync(new DecoderOptions(), buffer, ct);
        image.Mutate(x =>
        {
            x.AutoOrient();
            if (image.Width > maxDimension || image.Height > maxDimension)
                x.Resize(new ResizeOptions { Mode = ResizeMode.Max, Size = new Size(maxDimension, maxDimension) });
        });
        image.Metadata.ExifProfile = null;
        image.Metadata.XmpProfile = null;
        image.Metadata.IptcProfile = null;

        var output = new MemoryStream();
        await image.SaveAsWebpAsync(output, new WebpEncoder { Quality = 85, Method = WebpEncodingMethod.Default }, ct);
        output.Position = 0;
        return new ProcessedImage(output, image.Width, image.Height, "image/webp", ".webp");
    }
}
