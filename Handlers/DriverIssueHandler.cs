using DailyGourmet.Api.Authentication;
using DailyGourmet.Api.Data;
using DailyGourmet.Api.Helpers;
using DailyGourmet.Api.Models.Entities;
using Microsoft.EntityFrameworkCore;

namespace DailyGourmet.Api.Handlers;

/// <summary>A driver's free-form question/problem report from their dashboard — general, not tied to
/// a specific route or stop (that case already exists via RouteStop.ProblemNote). Surfaced to the
/// tenant's admins through the existing broadcast Notification mechanism (RecipientUserId == null),
/// which already renders on the admin dashboard's "Letzte Änderungen" panel — no separate inbox/list
/// UI needed for this to be visible.</summary>
public class DriverIssueHandler(DailyGourmetDbContext db, ITenantContext tenantContext)
{
    public async Task ReportAsync(string message, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(message)) throw new ValidationException("Bitte geben Sie eine Nachricht ein.");

        var driverName = await db.Users.Where(u => u.Id == tenantContext.UserId).Select(u => u.Name).FirstOrDefaultAsync(ct) ?? "Ein Fahrer";
        db.Notifications.Add(new Notification
        {
            Id = Guid.NewGuid(),
            TenantId = tenantContext.TenantId!.Value,
            RecipientUserId = null,
            Title = $"Meldung von {driverName}",
            Text = message.Trim(),
            IsRead = false,
            CreatedAt = DateTime.UtcNow,
        });
        await db.SaveChangesAsync(ct);
    }
}
