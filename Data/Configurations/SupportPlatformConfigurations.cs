using DailyGourmet.Api.Models.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace DailyGourmet.Api.Data.Configurations;

public class SupportTicketConfiguration : IEntityTypeConfiguration<SupportTicket>
{
    public void Configure(EntityTypeBuilder<SupportTicket> b)
    {
        b.Property(t => t.TicketNumber).HasMaxLength(20).IsRequired();
        b.HasIndex(t => t.TicketNumber).IsUnique();
        b.Property(t => t.Category).HasConversion<string>().HasMaxLength(10);
        b.Property(t => t.Priority).HasConversion<string>().HasMaxLength(10);
        b.Property(t => t.Title).HasMaxLength(200).IsRequired();
        b.Property(t => t.Message).IsRequired();
        b.Property(t => t.PageUrl).HasMaxLength(500);
        b.Property(t => t.Status).HasConversion<string>().HasMaxLength(20);

        b.HasOne(t => t.Tenant).WithMany()
            .HasForeignKey(t => t.TenantId).OnDelete(DeleteBehavior.Restrict);
        b.HasOne(t => t.CreatedByUser).WithMany()
            .HasForeignKey(t => t.CreatedByUserId).OnDelete(DeleteBehavior.Restrict);
    }
}

public class SupportTicketReplyConfiguration : IEntityTypeConfiguration<SupportTicketReply>
{
    public void Configure(EntityTypeBuilder<SupportTicketReply> b)
    {
        b.Property(r => r.Role).HasConversion<string>().HasMaxLength(20);
        b.Property(r => r.Text).IsRequired();

        b.HasOne(r => r.Ticket).WithMany(t => t.Replies)
            .HasForeignKey(r => r.TicketId).OnDelete(DeleteBehavior.Cascade);
        b.HasOne(r => r.AuthorUser).WithMany()
            .HasForeignKey(r => r.AuthorUserId).OnDelete(DeleteBehavior.Restrict);
    }
}

public class SupportSessionConfiguration : IEntityTypeConfiguration<SupportSession>
{
    public void Configure(EntityTypeBuilder<SupportSession> b)
    {
        b.Property(s => s.EndedReason).HasConversion<string>().HasMaxLength(10);

        b.HasOne(s => s.Tenant).WithMany()
            .HasForeignKey(s => s.TenantId).OnDelete(DeleteBehavior.Restrict);
        b.HasOne(s => s.StartedByUser).WithMany()
            .HasForeignKey(s => s.StartedByUserId).OnDelete(DeleteBehavior.Restrict);
    }
}

public class AuditLogConfiguration : IEntityTypeConfiguration<AuditLog>
{
    public void Configure(EntityTypeBuilder<AuditLog> b)
    {
        b.Property(a => a.Action).HasMaxLength(200).IsRequired();
        b.Property(a => a.Entity).HasMaxLength(100).IsRequired();
        b.Property(a => a.EntityId).HasMaxLength(100).IsRequired();
        b.Property(a => a.Reason).HasMaxLength(500);
        b.Property(a => a.IpAddress).HasMaxLength(45);
        b.HasIndex(a => new { a.TenantId, a.CreatedAtUtc });

        b.HasOne(a => a.Tenant).WithMany()
            .HasForeignKey(a => a.TenantId).OnDelete(DeleteBehavior.Restrict);
        b.HasOne(a => a.User).WithMany()
            .HasForeignKey(a => a.UserId).OnDelete(DeleteBehavior.Restrict);
    }
}

public class NotificationConfiguration : IEntityTypeConfiguration<Notification>
{
    public void Configure(EntityTypeBuilder<Notification> b)
    {
        b.Property(n => n.Title).HasMaxLength(200).IsRequired();
        b.Property(n => n.Text).HasMaxLength(1000).IsRequired();

        b.HasOne(n => n.Tenant).WithMany()
            .HasForeignKey(n => n.TenantId).OnDelete(DeleteBehavior.Restrict);
        b.HasOne(n => n.RecipientUser).WithMany()
            .HasForeignKey(n => n.RecipientUserId).OnDelete(DeleteBehavior.Restrict);
    }
}
