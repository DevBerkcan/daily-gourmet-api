using DailyGourmet.Api.Authentication;
using DailyGourmet.Api.Models.Entities;
using Microsoft.EntityFrameworkCore;

namespace DailyGourmet.Api.Data;

public class DailyGourmetDbContext : DbContext
{
    private readonly ITenantContext _tenantContext;

    public DailyGourmetDbContext(DbContextOptions<DailyGourmetDbContext> options, ITenantContext tenantContext)
        : base(options)
    {
        _tenantContext = tenantContext;
    }

    // Identity & tenancy
    public DbSet<Tenant> Tenants => Set<Tenant>();
    public DbSet<TenantProfile> TenantProfiles => Set<TenantProfile>();
    public DbSet<TenantSettings> TenantSettings => Set<TenantSettings>();
    public DbSet<TenantNotificationSetting> TenantNotificationSettings => Set<TenantNotificationSetting>();
    public DbSet<FeatureFlag> FeatureFlags => Set<FeatureFlag>();
    public DbSet<TenantFeatureFlag> TenantFeatureFlags => Set<TenantFeatureFlag>();
    public DbSet<User> Users => Set<User>();
    public DbSet<Driver> Drivers => Set<Driver>();

    // Locations & facilities
    public DbSet<Location> Locations => Set<Location>();
    public DbSet<Facility> Facilities => Set<Facility>();

    // Recipes & ingredients
    public DbSet<RecipeCategory> RecipeCategories => Set<RecipeCategory>();
    public DbSet<IngredientCategory> IngredientCategories => Set<IngredientCategory>();
    public DbSet<Allergen> Allergens => Set<Allergen>();
    public DbSet<TargetAudienceGroup> TargetAudienceGroups => Set<TargetAudienceGroup>();
    public DbSet<Supplier> Suppliers => Set<Supplier>();
    public DbSet<Ingredient> Ingredients => Set<Ingredient>();
    public DbSet<IngredientAllergen> IngredientAllergens => Set<IngredientAllergen>();
    public DbSet<IngredientAdditive> IngredientAdditives => Set<IngredientAdditive>();
    public DbSet<Recipe> Recipes => Set<Recipe>();
    public DbSet<RecipePrepStep> RecipePrepSteps => Set<RecipePrepStep>();
    public DbSet<RecipeIngredient> RecipeIngredients => Set<RecipeIngredient>();
    public DbSet<RecipeAllergenOverride> RecipeAllergenOverrides => Set<RecipeAllergenOverride>();
    public DbSet<RecipeAdditiveOverride> RecipeAdditiveOverrides => Set<RecipeAdditiveOverride>();
    public DbSet<RecipeNutritionClaim> RecipeNutritionClaims => Set<RecipeNutritionClaim>();
    public DbSet<RecipeTargetGroup> RecipeTargetGroups => Set<RecipeTargetGroup>();

    // Meal plans & orders
    public DbSet<MealPlan> MealPlans => Set<MealPlan>();
    public DbSet<MealPlanLocation> MealPlanLocations => Set<MealPlanLocation>();
    public DbSet<MealPlanFacility> MealPlanFacilities => Set<MealPlanFacility>();
    public DbSet<MealPlanDay> MealPlanDays => Set<MealPlanDay>();
    public DbSet<MealPlanItem> MealPlanItems => Set<MealPlanItem>();
    public DbSet<Order> Orders => Set<Order>();
    public DbSet<OrderItem> OrderItems => Set<OrderItem>();

    // Production & kitchen
    public DbSet<ProductionPlan> ProductionPlans => Set<ProductionPlan>();
    public DbSet<ProductionPlanItem> ProductionPlanItems => Set<ProductionPlanItem>();
    public DbSet<ProductionAdjustment> ProductionAdjustments => Set<ProductionAdjustment>();
    public DbSet<Deviation> Deviations => Set<Deviation>();
    public DbSet<QualityControl> QualityControls => Set<QualityControl>();
    public DbSet<StorageLocation> StorageLocations => Set<StorageLocation>();

    // Procurement & logistics
    public DbSet<ProcurementList> ProcurementLists => Set<ProcurementList>();
    public DbSet<ProcurementListItem> ProcurementListItems => Set<ProcurementListItem>();
    public DbSet<DeliveryRoute> Routes => Set<DeliveryRoute>();
    public DbSet<RouteStop> RouteStops => Set<RouteStop>();
    public DbSet<RouteStopItem> RouteStopItems => Set<RouteStopItem>();

    // Support & platform
    public DbSet<SupportTicket> SupportTickets => Set<SupportTicket>();
    public DbSet<SupportTicketReply> SupportTicketReplies => Set<SupportTicketReply>();
    public DbSet<SupportSession> SupportSessions => Set<SupportSession>();
    public DbSet<AuditLog> AuditLogs => Set<AuditLog>();
    public DbSet<Notification> Notifications => Set<Notification>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        modelBuilder.ApplyConfigurationsFromAssembly(typeof(DailyGourmetDbContext).Assembly);

        // Hard multi-tenant isolation: every ITenantScoped entity is filtered to the caller's
        // tenant. SUPER_ADMIN sees across tenants without needing IgnoreQueryFilters() on every
        // call site, since forgetting that call is an easy way to accidentally leak data.
        foreach (var entityType in modelBuilder.Model.GetEntityTypes())
        {
            if (typeof(ITenantScoped).IsAssignableFrom(entityType.ClrType))
            {
                var method = typeof(DailyGourmetDbContext)
                    .GetMethod(nameof(BuildTenantFilter), System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)!
                    .MakeGenericMethod(entityType.ClrType);
                var filter = method.Invoke(this, null);
                entityType.SetQueryFilter((System.Linq.Expressions.LambdaExpression)filter!);
            }
        }

        // User.TenantId is nullable (SUPER_ADMIN has none) — filtered separately, matching users
        // and platform users through.
        modelBuilder.Entity<User>().HasQueryFilter(u =>
            _tenantContext.IsSuperAdmin || u.TenantId == null || u.TenantId == _tenantContext.TenantId);

        // Dependent entities that carry no TenantId of their own (junction/child rows) still need
        // a filter — otherwise querying them directly (not through their tenant-scoped parent)
        // would leak cross-tenant rows. Cascaded through the required parent navigation.
        modelBuilder.Entity<IngredientAllergen>().HasQueryFilter(x => _tenantContext.IsSuperAdmin || x.Ingredient.TenantId == _tenantContext.TenantId);
        modelBuilder.Entity<IngredientAdditive>().HasQueryFilter(x => _tenantContext.IsSuperAdmin || x.Ingredient.TenantId == _tenantContext.TenantId);
        modelBuilder.Entity<RecipeIngredient>().HasQueryFilter(x => _tenantContext.IsSuperAdmin || x.Recipe.TenantId == _tenantContext.TenantId);
        modelBuilder.Entity<RecipePrepStep>().HasQueryFilter(x => _tenantContext.IsSuperAdmin || x.Recipe.TenantId == _tenantContext.TenantId);
        modelBuilder.Entity<RecipeAllergenOverride>().HasQueryFilter(x => _tenantContext.IsSuperAdmin || x.Recipe.TenantId == _tenantContext.TenantId);
        modelBuilder.Entity<RecipeAdditiveOverride>().HasQueryFilter(x => _tenantContext.IsSuperAdmin || x.Recipe.TenantId == _tenantContext.TenantId);
        modelBuilder.Entity<RecipeNutritionClaim>().HasQueryFilter(x => _tenantContext.IsSuperAdmin || x.Recipe.TenantId == _tenantContext.TenantId);
        modelBuilder.Entity<RecipeTargetGroup>().HasQueryFilter(x => _tenantContext.IsSuperAdmin || x.Recipe.TenantId == _tenantContext.TenantId);
        modelBuilder.Entity<MealPlanDay>().HasQueryFilter(x => _tenantContext.IsSuperAdmin || x.MealPlan.TenantId == _tenantContext.TenantId);
        modelBuilder.Entity<MealPlanItem>().HasQueryFilter(x => _tenantContext.IsSuperAdmin || x.Recipe.TenantId == _tenantContext.TenantId);
        modelBuilder.Entity<MealPlanLocation>().HasQueryFilter(x => _tenantContext.IsSuperAdmin || x.Location.TenantId == _tenantContext.TenantId);
        modelBuilder.Entity<MealPlanFacility>().HasQueryFilter(x => _tenantContext.IsSuperAdmin || x.Facility.TenantId == _tenantContext.TenantId);
        modelBuilder.Entity<OrderItem>().HasQueryFilter(x => _tenantContext.IsSuperAdmin || x.Order.TenantId == _tenantContext.TenantId);
        modelBuilder.Entity<ProductionPlanItem>().HasQueryFilter(x => _tenantContext.IsSuperAdmin || x.ProductionPlan.TenantId == _tenantContext.TenantId);
        modelBuilder.Entity<ProductionAdjustment>().HasQueryFilter(x => _tenantContext.IsSuperAdmin || x.ProductionPlanItem.ProductionPlan.TenantId == _tenantContext.TenantId);
        modelBuilder.Entity<ProcurementListItem>().HasQueryFilter(x => _tenantContext.IsSuperAdmin || x.ProcurementList.TenantId == _tenantContext.TenantId);
        modelBuilder.Entity<RouteStop>().HasQueryFilter(x => _tenantContext.IsSuperAdmin || x.DeliveryRoute.TenantId == _tenantContext.TenantId);
        modelBuilder.Entity<RouteStopItem>().HasQueryFilter(x => _tenantContext.IsSuperAdmin || x.RouteStop.DeliveryRoute.TenantId == _tenantContext.TenantId);
        modelBuilder.Entity<SupportTicketReply>().HasQueryFilter(x => _tenantContext.IsSuperAdmin || x.Ticket.TenantId == _tenantContext.TenantId);
    }

    private System.Linq.Expressions.Expression<Func<TEntity, bool>> BuildTenantFilter<TEntity>()
        where TEntity : class, ITenantScoped
    {
        return e => _tenantContext.IsSuperAdmin || e.TenantId == _tenantContext.TenantId;
    }
}
