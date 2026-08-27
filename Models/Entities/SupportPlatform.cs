using DailyGourmet.Api.Models.Enums;

namespace DailyGourmet.Api.Models.Entities;

public class SupportTicket : BaseEntity, ITenantScoped
{
    public Guid TenantId { get; set; }
    public Tenant Tenant { get; set; } = null!;
    public Guid CreatedByUserId { get; set; }
    public User CreatedByUser { get; set; } = null!;

    /// <summary>e.g. "SUP-1042" — generated from a SQL sequence.</summary>
    public string TicketNumber { get; set; } = null!;
    public SupportCategory Category { get; set; }
    public SupportPriority Priority { get; set; }
    public string Title { get; set; } = null!;
    public string Message { get; set; } = null!;
    public string? PageUrl { get; set; }
    public SupportStatus Status { get; set; } = SupportStatus.OFFEN;

    public ICollection<SupportTicketReply> Replies { get; set; } = new List<SupportTicketReply>();
    public ICollection<SupportTicketAttachment> Attachments { get; set; } = new List<SupportTicketAttachment>();
}

/// <summary>An image (screenshot) attached to a ticket. Uploads go through IImageHostingService
/// (imgbb) and are stored as a public ExternalUrl; StorageKey/local disk (IFileStorageService)
/// remains only for any attachment uploaded before that switch, so it stays nullable rather than
/// being repurposed to also hold a URL — keeps "hosted externally" an unambiguous ExternalUrl != null
/// check everywhere this is read.</summary>
public class SupportTicketAttachment : BaseEntity
{
    public Guid TicketId { get; set; }
    public SupportTicket Ticket { get; set; } = null!;
    public Guid UploadedByUserId { get; set; }
    public User UploadedByUser { get; set; } = null!;

    public string FileName { get; set; } = null!;
    public string ContentType { get; set; } = null!;
    public long SizeBytes { get; set; }
    public string? StorageKey { get; set; }
    public string? ExternalUrl { get; set; }
    /// <summary>imgbb's delete_url — kept for future moderation/cleanup tooling, not used yet.</summary>
    public string? DeleteUrl { get; set; }
}

public class SupportTicketReply : BaseEntity
{
    public Guid TicketId { get; set; }
    public SupportTicket Ticket { get; set; } = null!;
    public Guid AuthorUserId { get; set; }
    public User AuthorUser { get; set; } = null!;
    public SupportReplyRole Role { get; set; }
    public string Text { get; set; } = null!;
}

/// <summary>Real, server-expiry-enforced replacement for the frontend's volatile client timer.</summary>
public class SupportSession : BaseEntity, ITenantScoped
{
    public Guid TenantId { get; set; }
    public Tenant Tenant { get; set; } = null!;
    public Guid StartedByUserId { get; set; }
    public User StartedByUser { get; set; } = null!;

    public DateTime StartedAtUtc { get; set; }
    public DateTime ExpiresAtUtc { get; set; }
    public DateTime? EndedAtUtc { get; set; }
    public SupportSessionEndReason? EndedReason { get; set; }
}

/// <summary>Append-only — no update/delete endpoint exists for this entity.</summary>
public class AuditLog : BaseEntity
{
    /// <summary>Null = platform-level action.</summary>
    public Guid? TenantId { get; set; }
    public Tenant? Tenant { get; set; }

    /// <summary>Null for "System" actions.</summary>
    public Guid? UserId { get; set; }
    public User? User { get; set; }

    public string Action { get; set; } = null!;
    public string Entity { get; set; } = null!;
    public string EntityId { get; set; } = null!;
    public string? OldValues { get; set; }
    public string? NewValues { get; set; }
    public string? Reason { get; set; }
    public string? IpAddress { get; set; }
    public DateTime CreatedAtUtc { get; set; }
}

public class Notification : BaseEntity, ITenantScoped
{
    public Guid TenantId { get; set; }
    public Tenant Tenant { get; set; } = null!;

    /// <summary>Null = broadcast to all tenant admins.</summary>
    public Guid? RecipientUserId { get; set; }
    public User? RecipientUser { get; set; }

    public string Title { get; set; } = null!;
    public string Text { get; set; } = null!;
    public bool IsRead { get; set; }
}
