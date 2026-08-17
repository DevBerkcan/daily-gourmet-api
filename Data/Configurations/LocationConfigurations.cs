using DailyGourmet.Api.Models.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace DailyGourmet.Api.Data.Configurations;

public class LocationConfiguration : IEntityTypeConfiguration<Location>
{
    public void Configure(EntityTypeBuilder<Location> b)
    {
        b.Property(l => l.Name).HasMaxLength(200).IsRequired();
        b.Property(l => l.Address).HasMaxLength(300).IsRequired();
        b.Property(l => l.ContactPerson).HasMaxLength(200).IsRequired();
        b.Property(l => l.Status).HasConversion<string>().HasMaxLength(20);

        b.HasOne(l => l.Tenant).WithMany()
            .HasForeignKey(l => l.TenantId).OnDelete(DeleteBehavior.Restrict);
    }
}

public class FacilityConfiguration : IEntityTypeConfiguration<Facility>
{
    public void Configure(EntityTypeBuilder<Facility> b)
    {
        b.Property(f => f.Name).HasMaxLength(200).IsRequired();
        b.Property(f => f.CustomerNumber).HasMaxLength(20).IsRequired();
        b.HasIndex(f => new { f.TenantId, f.CustomerNumber }).IsUnique();
        b.Property(f => f.Address).HasMaxLength(300).IsRequired();
        b.Property(f => f.ContactPerson).HasMaxLength(200).IsRequired();
        b.Property(f => f.Email).HasMaxLength(256).IsRequired();
        b.Property(f => f.Phone).HasMaxLength(50).IsRequired();
        b.Property(f => f.ActiveWeekdays).HasMaxLength(20).IsRequired();
        b.Property(f => f.PortionPrice).HasPrecision(10, 2);
        b.Property(f => f.Status).HasConversion<string>().HasMaxLength(20);
        b.Property(f => f.Notes).HasMaxLength(1000);

        b.HasOne(f => f.Tenant).WithMany()
            .HasForeignKey(f => f.TenantId).OnDelete(DeleteBehavior.Restrict);
        b.HasOne(f => f.Location).WithMany(l => l.Facilities)
            .HasForeignKey(f => f.LocationId).OnDelete(DeleteBehavior.Restrict);
    }
}
