namespace DailyGourmet.Api.Services;

public record ImageHostingResult(string Url, string? DeleteUrl);

/// <summary>Uploads an image to a public, external host (imgbb) and returns its public URL — unlike
/// IFileStorageService, the result is a directly link-accessible URL, not an opaque key that only
/// resolves via an authenticated download endpoint. Used for support-ticket screenshot attachments,
/// which the product intentionally wants public-URL-simple rather than privately stored.</summary>
public interface IImageHostingService
{
    Task<ImageHostingResult> UploadAsync(Stream content, string fileName, CancellationToken ct = default);
}
