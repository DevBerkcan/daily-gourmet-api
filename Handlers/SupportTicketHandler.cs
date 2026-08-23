using DailyGourmet.Api.Authentication;
using DailyGourmet.Api.Data;
using DailyGourmet.Api.Helpers;
using DailyGourmet.Api.Models.DTOs;
using DailyGourmet.Api.Models.DTOs.Support;
using DailyGourmet.Api.Models.Entities;
using DailyGourmet.Api.Models.Enums;
using DailyGourmet.Api.Services;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;

namespace DailyGourmet.Api.Handlers;

public class SupportTicketHandler(DailyGourmetDbContext db, ITenantContext tenantContext, IFileStorageService fileStorage)
{
    private static IQueryable<SupportTicket> FullQuery(DailyGourmetDbContext db) => db.SupportTickets
        .Include(t => t.CreatedByUser)
        .Include(t => t.Tenant)
        .Include(t => t.Replies).ThenInclude(r => r.AuthorUser)
        .Include(t => t.Attachments);

    public async Task<PagedResult<SupportTicketDto>> ListAsync(string? status, int page, int pageSize, CancellationToken ct = default)
    {
        var query = FullQuery(db).AsQueryable();
        if (!string.IsNullOrWhiteSpace(status) && Enum.TryParse<SupportStatus>(status, out var s)) query = query.Where(t => t.Status == s);

        var total = await query.CountAsync(ct);
        var items = await query.OrderByDescending(t => t.CreatedAt).Skip((page - 1) * pageSize).Take(pageSize).ToListAsync(ct);
        return new PagedResult<SupportTicketDto> { Items = items.Select(ToDto).ToList(), Total = total, Page = page, PageSize = pageSize };
    }

    public async Task<SupportTicketDto> GetByIdAsync(Guid id, CancellationToken ct = default) =>
        ToDto(await FullQuery(db).FirstOrDefaultAsync(t => t.Id == id, ct) ?? throw new NotFoundException(nameof(SupportTicket), id));

    public async Task<SupportTicketDto> CreateAsync(CreateSupportTicketDto dto, CancellationToken ct = default)
    {
        if (!Enum.TryParse<SupportCategory>(dto.Category, out var category)) throw new ValidationException("Ungültige Kategorie.");
        if (!Enum.TryParse<SupportPriority>(dto.Priority, out var priority)) throw new ValidationException("Ungültige Priorität.");

        var count = await db.Set<SupportTicket>().IgnoreQueryFilters().CountAsync(ct);
        var ticket = new SupportTicket
        {
            Id = Guid.NewGuid(), TenantId = tenantContext.TenantId!.Value, CreatedByUserId = tenantContext.UserId!.Value,
            TicketNumber = $"SUP-{1042 + count}", Category = category, Priority = priority, Title = dto.Title, Message = dto.Message, PageUrl = dto.PageUrl, Status = SupportStatus.OFFEN,
        };
        db.SupportTickets.Add(ticket);
        await db.SaveChangesAsync(ct);
        return await GetByIdAsync(ticket.Id, ct);
    }

    public async Task<SupportTicketDto> AddReplyAsync(Guid ticketId, AddReplyDto dto, CancellationToken ct = default)
    {
        var ticket = await db.SupportTickets.FirstOrDefaultAsync(t => t.Id == ticketId, ct) ?? throw new NotFoundException(nameof(SupportTicket), ticketId);
        var role = tenantContext.IsSuperAdmin ? SupportReplyRole.SUPER_ADMIN : SupportReplyRole.TENANT_OWNER;

        db.SupportTicketReplies.Add(new SupportTicketReply { Id = Guid.NewGuid(), TicketId = ticketId, AuthorUserId = tenantContext.UserId!.Value, Role = role, Text = dto.Text, CreatedAt = DateTime.UtcNow });

        if (role == SupportReplyRole.SUPER_ADMIN && ticket.Status == SupportStatus.OFFEN)
            ticket.Status = SupportStatus.IN_BEARBEITUNG;

        await db.SaveChangesAsync(ct);
        return await GetByIdAsync(ticketId, ct);
    }

    public async Task<SupportTicketDto> UpdateStatusAsync(Guid id, UpdateTicketStatusDto dto, CancellationToken ct = default)
    {
        var ticket = await db.SupportTickets.FirstOrDefaultAsync(t => t.Id == id, ct) ?? throw new NotFoundException(nameof(SupportTicket), id);
        if (!Enum.TryParse<SupportStatus>(dto.Status, out var status)) throw new ValidationException("Ungültiger Status.");
        ticket.Status = status;
        ticket.UpdatedAt = DateTime.UtcNow;
        await db.SaveChangesAsync(ct);
        return await GetByIdAsync(id, ct);
    }

    public async Task<SupportTicketAttachmentDto> AddAttachmentAsync(Guid ticketId, IFormFile file, CancellationToken ct = default)
    {
        var ticket = await db.SupportTickets.FirstOrDefaultAsync(t => t.Id == ticketId, ct) ?? throw new NotFoundException(nameof(SupportTicket), ticketId);
        if (file.Length == 0) throw new ValidationException("Keine Datei übermittelt.");

        await using var stream = file.OpenReadStream();
        var storageKey = await fileStorage.SaveAsync(ticketId.ToString(), file.FileName, stream, ct);

        var attachment = new SupportTicketAttachment
        {
            Id = Guid.NewGuid(), TicketId = ticketId, UploadedByUserId = tenantContext.UserId!.Value,
            FileName = file.FileName, ContentType = file.ContentType, SizeBytes = file.Length, StorageKey = storageKey, CreatedAt = DateTime.UtcNow,
        };
        db.Set<SupportTicketAttachment>().Add(attachment);
        ticket.UpdatedAt = DateTime.UtcNow;
        await db.SaveChangesAsync(ct);
        return ToAttachmentDto(attachment);
    }

    public async Task<(Stream Content, string ContentType, string FileName)> GetAttachmentAsync(Guid ticketId, Guid attachmentId, CancellationToken ct = default)
    {
        var attachment = await db.Set<SupportTicketAttachment>().FirstOrDefaultAsync(a => a.Id == attachmentId && a.TicketId == ticketId, ct)
            ?? throw new NotFoundException(nameof(SupportTicketAttachment), attachmentId);
        var stream = await fileStorage.OpenReadAsync(attachment.StorageKey, ct);
        return (stream, attachment.ContentType, attachment.FileName);
    }

    private static SupportTicketAttachmentDto ToAttachmentDto(SupportTicketAttachment a) => new()
    {
        Id = a.Id, FileName = a.FileName, ContentType = a.ContentType, SizeBytes = a.SizeBytes, CreatedAt = a.CreatedAt,
    };

    private static SupportTicketDto ToDto(SupportTicket t) => new()
    {
        Id = t.Id, TenantId = t.TenantId, TenantName = t.Tenant?.Name ?? string.Empty, TicketNumber = t.TicketNumber, Category = t.Category.ToString(), Priority = t.Priority.ToString(),
        Title = t.Title, Message = t.Message, PageUrl = t.PageUrl, Status = t.Status.ToString(),
        CreatedByUserName = t.CreatedByUser?.Name ?? string.Empty, CreatedAt = t.CreatedAt,
        Replies = t.Replies.OrderBy(r => r.CreatedAt).Select(r => new SupportTicketReplyDto { Id = r.Id, AuthorName = r.AuthorUser?.Name ?? string.Empty, Role = r.Role.ToString(), Text = r.Text, CreatedAt = r.CreatedAt }).ToList(),
        Attachments = t.Attachments.OrderBy(a => a.CreatedAt).Select(ToAttachmentDto).ToList(),
    };
}
