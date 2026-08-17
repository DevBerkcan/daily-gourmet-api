using DailyGourmet.Api.Handlers;

namespace DailyGourmet.Api.Extensions;

public static class RecipesIngredientsServiceCollectionExtensions
{
    public static IServiceCollection AddRecipesIngredientsModule(this IServiceCollection services)
    {
        services.AddScoped<IngredientHandler>();
        services.AddScoped<SupplierHandler>();
        services.AddScoped<LookupHandler>();
        services.AddScoped<RecipeHandler>();
        return services;
    }
}
