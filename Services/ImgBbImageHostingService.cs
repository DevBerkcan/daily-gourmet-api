using System.Net.Http.Json;
using System.Text.Json.Serialization;
using DailyGourmet.Api.Options;
using Microsoft.Extensions.Options;

namespace DailyGourmet.Api.Services;

public class ImgBbImageHostingService(HttpClient http, IOptions<ImgBbOptions> options) : IImageHostingService
{
    public async Task<ImageHostingResult> UploadAsync(Stream content, string fileName, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(options.Value.ApiKey))
            throw new InvalidOperationException("ImgBb:ApiKey is not configured — set a real imgbb API key in appsettings.json before uploading images.");

        using var ms = new MemoryStream();
        await content.CopyToAsync(ms, ct);
        var base64 = Convert.ToBase64String(ms.ToArray());

        using var form = new MultipartFormDataContent
        {
            { new StringContent(base64), "image" },
            { new StringContent(fileName), "name" },
        };

        using var response = await http.PostAsync($"https://api.imgbb.com/1/upload?key={Uri.EscapeDataString(options.Value.ApiKey)}", form, ct);
        response.EnsureSuccessStatusCode();
        var payload = await response.Content.ReadFromJsonAsync<ImgBbResponse>(cancellationToken: ct)
            ?? throw new InvalidOperationException("imgbb hat keine gültige Antwort geliefert.");
        return new ImageHostingResult(payload.Data.Url, payload.Data.DeleteUrl);
    }

    private record ImgBbResponse([property: JsonPropertyName("data")] ImgBbData Data);
    private record ImgBbData([property: JsonPropertyName("url")] string Url, [property: JsonPropertyName("delete_url")] string? DeleteUrl);
}
