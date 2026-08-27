namespace DailyGourmet.Api.Options;

public class ImgBbOptions
{
    public const string SectionName = "ImgBb";

    /// <summary>API key from https://api.imgbb.com/ — required for support-ticket image uploads to
    /// work. Left blank in appsettings.json; set the real value there (or override via environment/
    /// user-secrets) before enabling the support-tenant-attachments feature flag.</summary>
    public string ApiKey { get; set; } = string.Empty;
}
