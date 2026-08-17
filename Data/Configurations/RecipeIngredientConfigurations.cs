using DailyGourmet.Api.Models.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace DailyGourmet.Api.Data.Configurations;

public class RecipeCategoryConfiguration : IEntityTypeConfiguration<RecipeCategory>
{
    public void Configure(EntityTypeBuilder<RecipeCategory> b)
    {
        b.Property(c => c.Name).HasMaxLength(100).IsRequired();
        b.HasIndex(c => c.Name).IsUnique();
    }
}

public class IngredientCategoryConfiguration : IEntityTypeConfiguration<IngredientCategory>
{
    public void Configure(EntityTypeBuilder<IngredientCategory> b)
    {
        b.Property(c => c.Name).HasMaxLength(100).IsRequired();
        b.HasIndex(c => c.Name).IsUnique();
        b.HasOne(c => c.DefaultStorageLocation).WithMany()
            .HasForeignKey(c => c.DefaultStorageLocationId).OnDelete(DeleteBehavior.SetNull);
    }
}

public class AllergenConfiguration : IEntityTypeConfiguration<Allergen>
{
    public void Configure(EntityTypeBuilder<Allergen> b)
    {
        b.Property(a => a.Name).HasMaxLength(100).IsRequired();
        b.HasIndex(a => a.Name).IsUnique();
    }
}

public class TargetAudienceGroupConfiguration : IEntityTypeConfiguration<TargetAudienceGroup>
{
    public void Configure(EntityTypeBuilder<TargetAudienceGroup> b)
    {
        b.Property(g => g.Name).HasMaxLength(100).IsRequired();
        b.HasIndex(g => g.Name).IsUnique();
    }
}

public class SupplierConfiguration : IEntityTypeConfiguration<Supplier>
{
    public void Configure(EntityTypeBuilder<Supplier> b)
    {
        b.Property(s => s.Name).HasMaxLength(200).IsRequired();
        b.Property(s => s.ContactPerson).HasMaxLength(200);
        b.Property(s => s.Phone).HasMaxLength(50);
        b.Property(s => s.Email).HasMaxLength(256);

        b.HasOne(s => s.Tenant).WithMany()
            .HasForeignKey(s => s.TenantId).OnDelete(DeleteBehavior.Restrict);
    }
}

public class IngredientConfiguration : IEntityTypeConfiguration<Ingredient>
{
    public void Configure(EntityTypeBuilder<Ingredient> b)
    {
        b.Property(i => i.Name).HasMaxLength(200).IsRequired();
        b.Property(i => i.ArticleNumber).HasMaxLength(50).IsRequired();
        b.HasIndex(i => new { i.TenantId, i.ArticleNumber }).IsUnique();
        b.Property(i => i.BaseUnit).HasConversion<string>().HasMaxLength(10);
        b.Property(i => i.PurchaseUnit).HasMaxLength(100).IsRequired();
        b.Property(i => i.ConversionFactor).HasPrecision(12, 4);
        b.Property(i => i.PurchasePrice).HasPrecision(12, 2);

        b.OwnsOne(i => i.Nutrition, n =>
        {
            n.Property(x => x.Kcal).HasPrecision(10, 2);
            n.Property(x => x.ProteinG).HasPrecision(10, 2);
            n.Property(x => x.FatG).HasPrecision(10, 2);
            n.Property(x => x.CarbsG).HasPrecision(10, 2);
            n.Property(x => x.SugarG).HasPrecision(10, 2);
            n.Property(x => x.SaltG).HasPrecision(10, 2);
            n.Property(x => x.Source).HasConversion<string>().HasMaxLength(20);
        });

        b.HasOne(i => i.Tenant).WithMany()
            .HasForeignKey(i => i.TenantId).OnDelete(DeleteBehavior.Restrict);
        b.HasOne(i => i.Category).WithMany()
            .HasForeignKey(i => i.CategoryId).OnDelete(DeleteBehavior.Restrict);
        b.HasOne(i => i.Supplier).WithMany()
            .HasForeignKey(i => i.SupplierId).OnDelete(DeleteBehavior.SetNull);
    }
}

public class IngredientAllergenConfiguration : IEntityTypeConfiguration<IngredientAllergen>
{
    public void Configure(EntityTypeBuilder<IngredientAllergen> b)
    {
        b.HasKey(x => new { x.IngredientId, x.AllergenId });
        b.HasOne(x => x.Ingredient).WithMany(i => i.Allergens)
            .HasForeignKey(x => x.IngredientId).OnDelete(DeleteBehavior.Cascade);
        b.HasOne(x => x.Allergen).WithMany()
            .HasForeignKey(x => x.AllergenId).OnDelete(DeleteBehavior.Restrict);
    }
}

public class IngredientAdditiveConfiguration : IEntityTypeConfiguration<IngredientAdditive>
{
    public void Configure(EntityTypeBuilder<IngredientAdditive> b)
    {
        b.Property(x => x.Text).HasMaxLength(100).IsRequired();
        b.HasOne(x => x.Ingredient).WithMany(i => i.Additives)
            .HasForeignKey(x => x.IngredientId).OnDelete(DeleteBehavior.Cascade);
    }
}

public class RecipeConfiguration : IEntityTypeConfiguration<Recipe>
{
    public void Configure(EntityTypeBuilder<Recipe> b)
    {
        b.Property(r => r.Name).HasMaxLength(200).IsRequired();
        b.Property(r => r.Description).HasMaxLength(2000).IsRequired();
        b.Property(r => r.RecipeNumber).HasMaxLength(50);
        b.Property(r => r.PortionWeightG).HasPrecision(10, 2);
        b.Property(r => r.Difficulty).HasConversion<string>().HasMaxLength(20);
        b.Property(r => r.ProductionNotes).HasMaxLength(1000);
        b.Property(r => r.ImageUrl).HasMaxLength(500);
        b.Property(r => r.CoreTemperatureC).HasPrecision(5, 2);
        b.Property(r => r.StorageNote).HasMaxLength(500);
        b.Property(r => r.ShelfLifeAfterPrep).HasMaxLength(200);
        b.Property(r => r.NutriScore).HasConversion<string>().HasMaxLength(1);
        b.Property(r => r.NutriScoreCategory).HasMaxLength(100);

        b.OwnsOne(r => r.Nutrition, n =>
        {
            n.Property(x => x.Kcal).HasPrecision(10, 2);
            n.Property(x => x.Kj).HasPrecision(10, 2);
            n.Property(x => x.FatG).HasPrecision(10, 2);
            n.Property(x => x.SaturatedFatG).HasPrecision(10, 2);
            n.Property(x => x.CarbsG).HasPrecision(10, 2);
            n.Property(x => x.SugarG).HasPrecision(10, 2);
            n.Property(x => x.FiberG).HasPrecision(10, 2);
            n.Property(x => x.ProteinG).HasPrecision(10, 2);
            n.Property(x => x.SaltG).HasPrecision(10, 2);
            n.Property(x => x.AlcoholG).HasPrecision(10, 2);
        });

        b.HasOne(r => r.Tenant).WithMany()
            .HasForeignKey(r => r.TenantId).OnDelete(DeleteBehavior.Restrict);
        b.HasOne(r => r.Category).WithMany()
            .HasForeignKey(r => r.CategoryId).OnDelete(DeleteBehavior.Restrict);
        b.HasOne(r => r.CreatedByUser).WithMany()
            .HasForeignKey(r => r.CreatedByUserId).OnDelete(DeleteBehavior.Restrict);
    }
}

public class RecipePrepStepConfiguration : IEntityTypeConfiguration<RecipePrepStep>
{
    public void Configure(EntityTypeBuilder<RecipePrepStep> b)
    {
        b.Property(s => s.Text).HasMaxLength(1000).IsRequired();
        b.HasOne(s => s.Recipe).WithMany(r => r.PrepSteps)
            .HasForeignKey(s => s.RecipeId).OnDelete(DeleteBehavior.Cascade);
    }
}

public class RecipeIngredientConfiguration : IEntityTypeConfiguration<RecipeIngredient>
{
    public void Configure(EntityTypeBuilder<RecipeIngredient> b)
    {
        b.Property(x => x.Quantity).HasPrecision(12, 3);
        b.Property(x => x.Unit).HasConversion<string>().HasMaxLength(10);

        b.HasOne(x => x.Recipe).WithMany(r => r.Ingredients)
            .HasForeignKey(x => x.RecipeId).OnDelete(DeleteBehavior.Cascade);
        b.HasOne(x => x.Ingredient).WithMany(i => i.UsedInRecipes)
            .HasForeignKey(x => x.IngredientId).OnDelete(DeleteBehavior.Restrict);
    }
}

public class RecipeAllergenOverrideConfiguration : IEntityTypeConfiguration<RecipeAllergenOverride>
{
    public void Configure(EntityTypeBuilder<RecipeAllergenOverride> b)
    {
        b.Property(x => x.Text).HasMaxLength(100).IsRequired();
        b.HasOne(x => x.Recipe).WithMany(r => r.AllergenOverrides)
            .HasForeignKey(x => x.RecipeId).OnDelete(DeleteBehavior.Cascade);
    }
}

public class RecipeAdditiveOverrideConfiguration : IEntityTypeConfiguration<RecipeAdditiveOverride>
{
    public void Configure(EntityTypeBuilder<RecipeAdditiveOverride> b)
    {
        b.Property(x => x.Text).HasMaxLength(100).IsRequired();
        b.HasOne(x => x.Recipe).WithMany(r => r.AdditiveOverrides)
            .HasForeignKey(x => x.RecipeId).OnDelete(DeleteBehavior.Cascade);
    }
}

public class RecipeNutritionClaimConfiguration : IEntityTypeConfiguration<RecipeNutritionClaim>
{
    public void Configure(EntityTypeBuilder<RecipeNutritionClaim> b)
    {
        b.Property(x => x.Text).HasMaxLength(200).IsRequired();
        b.HasOne(x => x.Recipe).WithMany(r => r.NutritionClaims)
            .HasForeignKey(x => x.RecipeId).OnDelete(DeleteBehavior.Cascade);
    }
}

public class RecipeTargetGroupConfiguration : IEntityTypeConfiguration<RecipeTargetGroup>
{
    public void Configure(EntityTypeBuilder<RecipeTargetGroup> b)
    {
        b.HasKey(x => new { x.RecipeId, x.TargetAudienceGroupId });
        b.HasOne(x => x.Recipe).WithMany(r => r.TargetGroups)
            .HasForeignKey(x => x.RecipeId).OnDelete(DeleteBehavior.Cascade);
        b.HasOne(x => x.TargetAudienceGroupEntity).WithMany()
            .HasForeignKey(x => x.TargetAudienceGroupId).OnDelete(DeleteBehavior.Restrict);
    }
}
