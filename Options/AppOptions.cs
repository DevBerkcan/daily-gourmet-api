namespace DailyGourmet.Api.Options;

public class AppOptions
{
    public const string SectionName = "App";

    /// <summary>The frontend's public URL, used to build links embedded in outbound emails (e.g.
    /// the procurement approval link) — see ProcurementListHandler. Empty in dev is fine; the link
    /// is still emitted, just pointing nowhere until configured.</summary>
    public string PublicBaseUrl { get; set; } = "";
}
