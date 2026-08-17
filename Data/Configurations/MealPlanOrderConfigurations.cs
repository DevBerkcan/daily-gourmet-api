using DailyGourmet.Api.Models.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace DailyGourmet.Api.Data.Configurations;

public class MealPlanConfiguration : IEntityTypeConfiguration<MealPlan>
{
    public void Configure(EntityTypeBuilder<MealPlan> b)
    {
        b.Property(m => m.Status).HasConversion<string>().HasMaxLength(20);
        b.HasIndex(m => new { m.TenantId, m.Year, m.CalendarWeek }).IsUnique();

        b.HasOne(m => m.Tenant).WithMany()
            .HasForeignKey(m => m.TenantId).OnDelete(DeleteBehavior.Restrict);
    }
}

public class MealPlanLocationConfiguration : IEntityTypeConfiguration<MealPlanLocation>
{
    public void Configure(EntityTypeBuilder<MealPlanLocation> b)
    {
        b.HasKey(x => new { x.MealPlanId, x.LocationId });
        b.HasOne(x => x.MealPlan).WithMany(m => m.Locations)
            .HasForeignKey(x => x.MealPlanId).OnDelete(DeleteBehavior.Cascade);
        b.HasOne(x => x.Location).WithMany()
            .HasForeignKey(x => x.LocationId).OnDelete(DeleteBehavior.Restrict);
    }
}

public class MealPlanFacilityConfiguration : IEntityTypeConfiguration<MealPlanFacility>
{
    public void Configure(EntityTypeBuilder<MealPlanFacility> b)
    {
        b.HasKey(x => new { x.MealPlanId, x.FacilityId });
        b.HasOne(x => x.MealPlan).WithMany(m => m.Facilities)
            .HasForeignKey(x => x.MealPlanId).OnDelete(DeleteBehavior.Cascade);
        b.HasOne(x => x.Facility).WithMany()
            .HasForeignKey(x => x.FacilityId).OnDelete(DeleteBehavior.Restrict);
    }
}

public class MealPlanDayConfiguration : IEntityTypeConfiguration<MealPlanDay>
{
    public void Configure(EntityTypeBuilder<MealPlanDay> b)
    {
        b.Property(d => d.Weekday).HasMaxLength(20).IsRequired();
        b.Property(d => d.Note).HasMaxLength(500);

        b.HasOne(d => d.MealPlan).WithMany(m => m.Days)
            .HasForeignKey(d => d.MealPlanId).OnDelete(DeleteBehavior.Cascade);
    }
}

public class MealPlanItemConfiguration : IEntityTypeConfiguration<MealPlanItem>
{
    public void Configure(EntityTypeBuilder<MealPlanItem> b)
    {
        b.HasOne(i => i.MealPlanDay).WithMany(d => d.Items)
            .HasForeignKey(i => i.MealPlanDayId).OnDelete(DeleteBehavior.Cascade);
        b.HasOne(i => i.Recipe).WithMany()
            .HasForeignKey(i => i.RecipeId).OnDelete(DeleteBehavior.Restrict);
    }
}

public class OrderConfiguration : IEntityTypeConfiguration<Order>
{
    public void Configure(EntityTypeBuilder<Order> b)
    {
        b.Property(o => o.Status).HasConversion<string>().HasMaxLength(20);
        b.HasIndex(o => new { o.FacilityId, o.MealPlanId }).IsUnique();

        b.HasOne(o => o.Tenant).WithMany()
            .HasForeignKey(o => o.TenantId).OnDelete(DeleteBehavior.Restrict);
        b.HasOne(o => o.Facility).WithMany(f => f.Orders)
            .HasForeignKey(o => o.FacilityId).OnDelete(DeleteBehavior.Restrict);
        b.HasOne(o => o.MealPlan).WithMany(m => m.Orders)
            .HasForeignKey(o => o.MealPlanId).OnDelete(DeleteBehavior.Restrict);
    }
}

public class OrderItemConfiguration : IEntityTypeConfiguration<OrderItem>
{
    public void Configure(EntityTypeBuilder<OrderItem> b)
    {
        b.Property(i => i.Note).HasMaxLength(500);
        b.ToTable(t => t.HasCheckConstraint("CK_OrderItem_Portions", "[Portions] >= 0"));

        b.HasOne(i => i.Order).WithMany(o => o.Items)
            .HasForeignKey(i => i.OrderId).OnDelete(DeleteBehavior.Cascade);
        b.HasOne(i => i.Recipe).WithMany()
            .HasForeignKey(i => i.RecipeId).OnDelete(DeleteBehavior.Restrict);
    }
}
