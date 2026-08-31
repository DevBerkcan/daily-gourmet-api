namespace DailyGourmet.Api.Services;

/// <summary>Wraps a handler's plain content HTML in the shared branded email layout — the one place
/// email markup/branding lives, so every outgoing mail looks consistent without each handler
/// hand-rolling its own &lt;html&gt; shell. <paramref name="bodyHtml"/> stays semantic
/// (plain &lt;p&gt;/&lt;strong&gt; produced by the caller); this only supplies the header, an
/// optional call-to-action button and the footer around it. Colors are inlined (not classes) because
/// most email clients strip &lt;style&gt; blocks or ignore external CSS.</summary>
public static class EmailTemplate
{
    private const string Ink = "#1e2937";
    private const string Muted = "#667085";
    private const string Paper = "#f6f7f9";
    private const string Line = "#e3e7ed";
    private const string Basil = "#1b5fa0";
    private const string BasilDeep = "#1b3350";

    /// <param name="preheader">Short summary shown by mail clients next to the subject — not rendered visibly in the body.</param>
    /// <param name="bodyHtml">The message content as simple HTML (&lt;p&gt;, &lt;strong&gt;, ...). No links — use <paramref name="ctaText"/>/<paramref name="ctaUrl"/> for the primary action instead, so it renders as a button.</param>
    public static string Render(string preheader, string bodyHtml, string? ctaText = null, string? ctaUrl = null)
    {
        var cta = ctaText is not null && ctaUrl is not null
            ? $"""
                <tr><td style="padding:20px 0 0;">
                  <a href="{ctaUrl}" style="display:inline-block;background:{Basil};color:#ffffff;text-decoration:none;font-weight:600;font-size:15px;padding:12px 28px;border-radius:8px;">{ctaText}</a>
                </td></tr>
                """
            : "";

        return $"""
            <!DOCTYPE html>
            <html lang="de">
            <head>
              <meta charset="utf-8">
              <meta name="viewport" content="width=device-width, initial-scale=1">
              <meta name="color-scheme" content="light">
              <title>Daily Gourmet</title>
            </head>
            <body style="margin:0;padding:0;background:{Paper};font-family:'Work Sans',Arial,sans-serif;color:{Ink};">
              <span style="display:none;font-size:1px;color:{Paper};line-height:1px;max-height:0;max-width:0;opacity:0;overflow:hidden;">{preheader}</span>
              <table role="presentation" width="100%" cellpadding="0" cellspacing="0" style="background:{Paper};padding:32px 16px;">
                <tr><td align="center">
                  <table role="presentation" width="100%" cellpadding="0" cellspacing="0" style="max-width:520px;background:#ffffff;border-radius:14px;overflow:hidden;border:1px solid {Line};">
                    <tr><td style="background:{BasilDeep};padding:22px 32px;">
                      <span style="font-family:'Outfit',Arial,sans-serif;font-size:19px;font-weight:700;color:#ffffff;letter-spacing:.2px;">Daily Gourmet</span>
                    </td></tr>
                    <tr><td style="padding:32px;">
                      <table role="presentation" width="100%" cellpadding="0" cellspacing="0" style="font-size:15px;line-height:1.65;color:{Ink};">
                        <tr><td style="padding:0;">{bodyHtml}</td></tr>
                        {cta}
                      </table>
                    </td></tr>
                    <tr><td style="padding:18px 32px;border-top:1px solid {Line};background:{Paper};">
                      <p style="margin:0;font-size:12px;color:{Muted};">Diese E-Mail wurde automatisch von Daily Gourmet versendet. Bitte antworten Sie nicht direkt auf diese Nachricht.</p>
                    </td></tr>
                  </table>
                </td></tr>
              </table>
            </body>
            </html>
            """;
    }
}
