namespace DailyGourmet.Api.Services;

public interface IEmailService
{
    /// <summary>Fire-and-forget from the caller's perspective — failures are logged, never thrown
    /// into the calling request, so email delivery never blocks the primary transaction.</summary>
    Task SendAsync(string toEmail, string toName, string subject, string htmlBody, string plainTextBody);
}
