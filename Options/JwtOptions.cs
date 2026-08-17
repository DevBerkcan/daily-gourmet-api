namespace DailyGourmet.Api.Options;

public class JwtOptions
{
    public const string SectionName = "Jwt";

    public string Secret { get; set; } = string.Empty;
    public string Issuer { get; set; } = "DailyGourmet";
    public string Audience { get; set; } = "DailyGourmet.Frontend";
    public int ExpirationMinutes { get; set; } = 1440;
}
