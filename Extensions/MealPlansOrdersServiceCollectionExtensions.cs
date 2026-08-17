using DailyGourmet.Api.Handlers;

namespace DailyGourmet.Api.Extensions;

public static class MealPlansOrdersServiceCollectionExtensions
{
    public static IServiceCollection AddMealPlansOrdersModule(this IServiceCollection services)
    {
        services.AddScoped<MealPlanHandler>();
        services.AddScoped<OrderHandler>();
        return services;
    }
}
