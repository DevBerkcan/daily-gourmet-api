using DailyGourmet.Api.Models.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace DailyGourmet.Api.Data.Configurations;

public class ProcurementListConfiguration : IEntityTypeConfiguration<ProcurementList>
{
    public void Configure(EntityTypeBuilder<ProcurementList> b)
    {
        b.Property(p => p.Label).HasMaxLength(200).IsRequired();
        b.Property(p => p.Status).HasConversion<string>().HasMaxLength(20);

        b.HasOne(p => p.Tenant).WithMany()
            .HasForeignKey(p => p.TenantId).OnDelete(DeleteBehavior.Restrict);
        b.HasOne(p => p.Location).WithMany()
            .HasForeignKey(p => p.LocationId).OnDelete(DeleteBehavior.Restrict);
    }
}

public class ProcurementListItemConfiguration : IEntityTypeConfiguration<ProcurementListItem>
{
    public void Configure(EntityTypeBuilder<ProcurementListItem> b)
    {
        b.Property(i => i.Unit).HasConversion<string>().HasMaxLength(10);
        b.Property(i => i.TotalQuantityBase).HasPrecision(12, 3);
        b.Property(i => i.PurchaseQuantity).HasPrecision(12, 3);
        b.HasIndex(i => new { i.ProcurementListId, i.IngredientId, i.Unit }).IsUnique();

        b.HasOne(i => i.ProcurementList).WithMany(p => p.Items)
            .HasForeignKey(i => i.ProcurementListId).OnDelete(DeleteBehavior.Cascade);
        b.HasOne(i => i.Ingredient).WithMany()
            .HasForeignKey(i => i.IngredientId).OnDelete(DeleteBehavior.Restrict);
    }
}

public class RouteConfiguration : IEntityTypeConfiguration<DeliveryRoute>
{
    public void Configure(EntityTypeBuilder<DeliveryRoute> b)
    {
        b.Property(r => r.Name).HasMaxLength(200).IsRequired();
        b.Property(r => r.DistanceKm).HasPrecision(6, 1);
        b.Property(r => r.Status).HasConversion<string>().HasMaxLength(20);

        b.HasOne(r => r.Tenant).WithMany()
            .HasForeignKey(r => r.TenantId).OnDelete(DeleteBehavior.Restrict);
        b.HasOne(r => r.Driver).WithMany(d => d.Routes)
            .HasForeignKey(r => r.DriverId).OnDelete(DeleteBehavior.Restrict);
        b.HasOne(r => r.Location).WithMany()
            .HasForeignKey(r => r.LocationId).OnDelete(DeleteBehavior.Restrict);
    }
}

public class RouteStopConfiguration : IEntityTypeConfiguration<RouteStop>
{
    public void Configure(EntityTypeBuilder<RouteStop> b)
    {
        b.Property(s => s.ContactName).HasMaxLength(200).IsRequired();
        b.Property(s => s.ContactPhone).HasMaxLength(50).IsRequired();
        b.Property(s => s.Note).HasMaxLength(500);
        b.Property(s => s.ProblemNote).HasMaxLength(500);
        b.Property(s => s.Status).HasConversion<string>().HasMaxLength(20);

        b.HasOne(s => s.DeliveryRoute).WithMany(r => r.Stops)
            .HasForeignKey(s => s.RouteId).OnDelete(DeleteBehavior.Cascade);
        b.HasOne(s => s.Facility).WithMany()
            .HasForeignKey(s => s.FacilityId).OnDelete(DeleteBehavior.Restrict);
    }
}

public class RouteStopItemConfiguration : IEntityTypeConfiguration<RouteStopItem>
{
    public void Configure(EntityTypeBuilder<RouteStopItem> b)
    {
        b.Property(i => i.ContainerDescription).HasMaxLength(100).IsRequired();
        b.Property(i => i.TemperatureRequirement).HasMaxLength(50).IsRequired();
        b.Property(i => i.Note).HasMaxLength(500);

        b.HasOne(i => i.RouteStop).WithMany(s => s.Items)
            .HasForeignKey(i => i.RouteStopId).OnDelete(DeleteBehavior.Cascade);
        b.HasOne(i => i.Recipe).WithMany()
            .HasForeignKey(i => i.RecipeId).OnDelete(DeleteBehavior.Restrict);
        b.HasOne(i => i.Order).WithMany()
            .HasForeignKey(i => i.OrderId).OnDelete(DeleteBehavior.Restrict);
        b.HasOne(i => i.OrderItem).WithMany()
            .HasForeignKey(i => i.OrderItemId).OnDelete(DeleteBehavior.Restrict);
    }
}
