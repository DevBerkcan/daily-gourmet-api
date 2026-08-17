using System.ComponentModel.DataAnnotations;

namespace DailyGourmet.Api.Models.DTOs.Support;

public class SupportTicketReplyDto
{
    public Guid Id { get; set; }
    public string AuthorName { get; set; } = string.Empty;
    public string Role { get; set; } = string.Empty;
    public string Text { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; }
}

public class SupportTicketDto
{
    public Guid Id { get; set; }
    public string TicketNumber { get; set; } = string.Empty;
    public string Category { get; set; } = string.Empty;
    public string Priority { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public string Message { get; set; } = string.Empty;
    public string? PageUrl { get; set; }
    public string Status { get; set; } = string.Empty;
    public string CreatedByUserName { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; }
    public List<SupportTicketReplyDto> Replies { get; set; } = [];
}

public class CreateSupportTicketDto
{
    [Required] public string Category { get; set; } = string.Empty;
    [Required] public string Priority { get; set; } = string.Empty;
    [Required, MaxLength(200)] public string Title { get; set; } = string.Empty;
    [Required] public string Message { get; set; } = string.Empty;
    public string? PageUrl { get; set; }
}

public class AddReplyDto
{
    [Required, MinLength(1)] public string Text { get; set; } = string.Empty;
}

public class UpdateTicketStatusDto
{
    [Required] public string Status { get; set; } = string.Empty;
}

public class SupportSessionDto
{
    public Guid Id { get; set; }
    public Guid TenantId { get; set; }
    public string TenantName { get; set; } = string.Empty;
    public string StartedByUserName { get; set; } = string.Empty;
    public DateTime StartedAtUtc { get; set; }
    public DateTime ExpiresAtUtc { get; set; }
    public DateTime? EndedAtUtc { get; set; }
    public string? EndedReason { get; set; }
}
