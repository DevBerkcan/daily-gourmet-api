namespace DailyGourmet.Api.Models.Entities;

/// <summary>Base for every entity: surrogate GUID key + UTC audit timestamps.</summary>
public abstract class BaseEntity
{
    public Guid Id { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime? UpdatedAt { get; set; }
}

/// <summary>Marks an entity as tenant-scoped so the EF global query filter applies to it.</summary>
public interface ITenantScoped
{
    Guid TenantId { get; set; }
}
