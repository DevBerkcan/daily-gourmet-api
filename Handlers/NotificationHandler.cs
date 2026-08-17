using DailyGourmet.Api.Authentication;
using DailyGourmet.Api.Data;
using DailyGourmet.Api.Helpers;
using DailyGourmet.Api.Models.DTOs;
using DailyGourmet.Api.Models.DTOs.Dashboard;
using DailyGourmet.Api.Models.Entities;
using Microsoft.EntityFrameworkCore;

namespace DailyGourmet.Api.Handlers;

public class NotificationHandler(DailyGourmetDbContext db, ITenantContext tenantContext)
{
    public async Task<PagedResult<NotificationDto>> ListAsync(int page, int pageSize, CancellationToken ct = default)
    {
        var query = db.Notifications.Where(n => n.RecipientUserId == null || n.RecipientUserId == tenantContext.UserId);
        var total = await query.CountAsync(ct);
        var items = await query.OrderByDescending(n => n.CreatedAt).Skip((page - 1) * pageSize).Take(pageSize).ToListAsync(ct);
        return new PagedResult<NotificationDto>
        {
            Items = items.Select(n => new NotificationDto { Id = n.Id, Title = n.Title, Text = n.Text, IsRead = n.IsRead, CreatedAt = n.CreatedAt }).ToList(),
            Total = total, Page = page, PageSize = pageSize,
        };
    }

    public async Task MarkReadAsync(Guid id, CancellationToken ct = default)
    {
        var notification = await db.Notifications.FirstOrDefaultAsync(n => n.Id == id, ct) ?? throw new NotFoundException(nameof(Notification), id);
        notification.IsRead = true;
        notification.UpdatedAt = DateTime.UtcNow;
        await db.SaveChangesAsync(ct);
    }
}
