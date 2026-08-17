using DailyGourmet.Api.Models.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace DailyGourmet.Api.Data.Configurations;

public class ProductionPlanConfiguration : IEntityTypeConfiguration<ProductionPlan>
{
    public void Configure(EntityTypeBuilder<ProductionPlan> b)
    {
        b.HasIndex(p => new { p.TenantId, p.Date, p.LocationId }).IsUnique();

        b.HasOne(p => p.Tenant).WithMany()
            .HasForeignKey(p => p.TenantId).OnDelete(DeleteBehavior.Restrict);
        b.HasOne(p => p.Location).WithMany()
            .HasForeignKey(p => p.LocationId).OnDelete(DeleteBehavior.Restrict);
    }
}

public class ProductionPlanItemConfiguration : IEntityTypeConfiguration<ProductionPlanItem>
{
    public void Configure(EntityTypeBuilder<ProductionPlanItem> b)
    {
        b.Property(i => i.AdjustmentReason).HasMaxLength(500);
        b.Property(i => i.Status).HasConversion<string>().HasMaxLength(20);
        b.Property(i => i.WorkStatus).HasConversion<string>().HasMaxLength(20);
        b.Property(i => i.Workstation).HasMaxLength(100);
        b.Property(i => i.Equipment).HasMaxLength(100);
        b.Property(i => i.ResponsiblePerson).HasMaxLength(200);
        b.HasIndex(i => new { i.ProductionPlanId, i.RecipeId }).IsUnique();

        b.HasOne(i => i.ProductionPlan).WithMany(p => p.Items)
            .HasForeignKey(i => i.ProductionPlanId).OnDelete(DeleteBehavior.Cascade);
        b.HasOne(i => i.Recipe).WithMany()
            .HasForeignKey(i => i.RecipeId).OnDelete(DeleteBehavior.Restrict);
    }
}

public class ProductionAdjustmentConfiguration : IEntityTypeConfiguration<ProductionAdjustment>
{
    public void Configure(EntityTypeBuilder<ProductionAdjustment> b)
    {
        b.Property(a => a.Reason).HasMaxLength(500).IsRequired();

        b.HasOne(a => a.ProductionPlanItem).WithMany(i => i.AdjustmentHistory)
            .HasForeignKey(a => a.ProductionPlanItemId).OnDelete(DeleteBehavior.Cascade);
        b.HasOne(a => a.ChangedByUser).WithMany()
            .HasForeignKey(a => a.ChangedByUserId).OnDelete(DeleteBehavior.Restrict);
    }
}

public class DeviationConfiguration : IEntityTypeConfiguration<Deviation>
{
    public void Configure(EntityTypeBuilder<Deviation> b)
    {
        b.Property(d => d.Category).HasConversion<string>().HasMaxLength(30);
        b.Property(d => d.Subject).HasMaxLength(200).IsRequired();
        b.Property(d => d.Quantity).HasMaxLength(100);
        b.Property(d => d.Action).HasMaxLength(500).IsRequired();
        b.Property(d => d.Status).HasConversion<string>().HasMaxLength(20);

        b.HasOne(d => d.Tenant).WithMany()
            .HasForeignKey(d => d.TenantId).OnDelete(DeleteBehavior.Restrict);
        b.HasOne(d => d.ProductionPlan).WithMany(p => p.Deviations)
            .HasForeignKey(d => d.ProductionPlanId).OnDelete(DeleteBehavior.Restrict);
        b.HasOne(d => d.ReportedByUser).WithMany()
            .HasForeignKey(d => d.ReportedByUserId).OnDelete(DeleteBehavior.Restrict);
        b.HasOne(d => d.ResolvedByUser).WithMany()
            .HasForeignKey(d => d.ResolvedByUserId).OnDelete(DeleteBehavior.Restrict);
    }
}

public class QualityControlConfiguration : IEntityTypeConfiguration<QualityControl>
{
    public void Configure(EntityTypeBuilder<QualityControl> b)
    {
        b.Property(c => c.Type).HasConversion<string>().HasMaxLength(30);
        b.Property(c => c.Area).HasMaxLength(200).IsRequired();
        b.Property(c => c.TargetValue).HasMaxLength(50).IsRequired();
        b.Property(c => c.MeasuredValue).HasMaxLength(50).IsRequired();
        b.Property(c => c.Status).HasConversion<string>().HasMaxLength(10);

        b.HasOne(c => c.Tenant).WithMany()
            .HasForeignKey(c => c.TenantId).OnDelete(DeleteBehavior.Restrict);
        b.HasOne(c => c.ProductionPlan).WithMany(p => p.QualityControls)
            .HasForeignKey(c => c.ProductionPlanId).OnDelete(DeleteBehavior.Restrict);
        b.HasOne(c => c.PerformedByUser).WithMany()
            .HasForeignKey(c => c.PerformedByUserId).OnDelete(DeleteBehavior.Restrict);
    }
}

public class StorageLocationConfiguration : IEntityTypeConfiguration<StorageLocation>
{
    public void Configure(EntityTypeBuilder<StorageLocation> b)
    {
        b.Property(s => s.Name).HasMaxLength(100).IsRequired();
        b.HasOne(s => s.Tenant).WithMany()
            .HasForeignKey(s => s.TenantId).OnDelete(DeleteBehavior.Restrict);
    }
}
