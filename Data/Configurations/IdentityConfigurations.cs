using DailyGourmet.Api.Models.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace DailyGourmet.Api.Data.Configurations;

public class TenantConfiguration : IEntityTypeConfiguration<Tenant>
{
    public void Configure(EntityTypeBuilder<Tenant> b)
    {
        b.Property(t => t.Name).HasMaxLength(200).IsRequired();
        b.Property(t => t.MainContactName).HasMaxLength(200).IsRequired();
        b.Property(t => t.MainContactEmail).HasMaxLength(256).IsRequired();
        b.Property(t => t.Status).HasConversion<string>().HasMaxLength(20);

        b.HasOne(t => t.Profile).WithOne(p => p.Tenant)
            .HasForeignKey<TenantProfile>(p => p.TenantId).OnDelete(DeleteBehavior.Cascade);
        b.HasOne(t => t.Settings).WithOne(s => s.Tenant)
            .HasForeignKey<TenantSettings>(s => s.TenantId).OnDelete(DeleteBehavior.Cascade);
    }
}

public class TenantProfileConfiguration : IEntityTypeConfiguration<TenantProfile>
{
    public void Configure(EntityTypeBuilder<TenantProfile> b)
    {
        b.HasKey(p => p.TenantId);
        b.Property(p => p.VatId).HasMaxLength(50);
        b.Property(p => p.Street).HasMaxLength(200);
        b.Property(p => p.PostalCode).HasMaxLength(10);
        b.Property(p => p.City).HasMaxLength(100);
        b.Property(p => p.Phone).HasMaxLength(50);
        b.Property(p => p.Email).HasMaxLength(256);
        b.Property(p => p.Timezone).HasMaxLength(50).IsRequired();
        b.Property(p => p.Currency).HasMaxLength(3).IsRequired();
        b.Property(p => p.LogoUrl).HasMaxLength(500);
    }
}

public class TenantSettingsConfiguration : IEntityTypeConfiguration<TenantSettings>
{
    public void Configure(EntityTypeBuilder<TenantSettings> b)
    {
        b.HasKey(s => s.TenantId);
        b.Property(s => s.FacilityNumberPrefix).HasMaxLength(10).IsRequired();
        b.Property(s => s.ArticleNumberPrefix).HasMaxLength(10).IsRequired();
    }
}

public class TenantNotificationSettingConfiguration : IEntityTypeConfiguration<TenantNotificationSetting>
{
    public void Configure(EntityTypeBuilder<TenantNotificationSetting> b)
    {
        b.Property(s => s.EventKey).HasMaxLength(50).IsRequired();
        b.HasIndex(s => new { s.TenantId, s.EventKey }).IsUnique();
        b.HasOne(s => s.Tenant).WithMany(t => t.NotificationSettings)
            .HasForeignKey(s => s.TenantId).OnDelete(DeleteBehavior.Cascade);
    }
}

public class FeatureFlagConfiguration : IEntityTypeConfiguration<FeatureFlag>
{
    public void Configure(EntityTypeBuilder<FeatureFlag> b)
    {
        b.Property(f => f.Key).HasMaxLength(50).IsRequired();
        b.HasIndex(f => f.Key).IsUnique();
        b.Property(f => f.Name).HasMaxLength(200).IsRequired();
        b.Property(f => f.Description).HasMaxLength(500);
    }
}

public class TenantFeatureFlagConfiguration : IEntityTypeConfiguration<TenantFeatureFlag>
{
    public void Configure(EntityTypeBuilder<TenantFeatureFlag> b)
    {
        b.HasKey(x => new { x.TenantId, x.FeatureFlagId });
        b.HasOne(x => x.Tenant).WithMany(t => t.FeatureFlags)
            .HasForeignKey(x => x.TenantId).OnDelete(DeleteBehavior.Cascade);
        b.HasOne(x => x.FeatureFlag).WithMany(f => f.TenantOverrides)
            .HasForeignKey(x => x.FeatureFlagId).OnDelete(DeleteBehavior.Cascade);
    }
}

public class UserConfiguration : IEntityTypeConfiguration<User>
{
    public void Configure(EntityTypeBuilder<User> b)
    {
        b.Property(u => u.Name).HasMaxLength(200).IsRequired();
        b.Property(u => u.Email).HasMaxLength(256).IsRequired();
        b.HasIndex(u => u.Email).IsUnique();
        b.Property(u => u.PasswordHash).IsRequired();
        b.Property(u => u.Role).HasConversion<string>().HasMaxLength(20);
        b.Property(u => u.Status).HasConversion<string>().HasMaxLength(20);
        b.Property(u => u.InvitationToken).HasMaxLength(200);

        b.HasOne(u => u.Tenant).WithMany(t => t.Users)
            .HasForeignKey(u => u.TenantId).OnDelete(DeleteBehavior.Restrict);
        b.HasOne(u => u.Facility).WithMany(f => f.Users)
            .HasForeignKey(u => u.FacilityId).OnDelete(DeleteBehavior.Restrict);
    }
}

public class DriverConfiguration : IEntityTypeConfiguration<Driver>
{
    public void Configure(EntityTypeBuilder<Driver> b)
    {
        b.Property(d => d.Phone).HasMaxLength(50).IsRequired();
        b.Property(d => d.VehicleDescription).HasMaxLength(200).IsRequired();
        b.Property(d => d.LicensePlate).HasMaxLength(20).IsRequired();
        b.HasIndex(d => d.UserId).IsUnique();

        b.HasOne(d => d.Tenant).WithMany()
            .HasForeignKey(d => d.TenantId).OnDelete(DeleteBehavior.Restrict);
        b.HasOne(d => d.User).WithOne(u => u.Driver)
            .HasForeignKey<Driver>(d => d.UserId).OnDelete(DeleteBehavior.Cascade);
    }
}
