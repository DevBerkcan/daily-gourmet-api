using DailyGourmet.Api.Handlers;

namespace DailyGourmet.Api.Extensions;

public static class ProductionKitchenServiceCollectionExtensions
{
    public static IServiceCollection AddProductionKitchenModule(this IServiceCollection services)
    {
        services.AddScoped<ProductionPlanHandler>();
        services.AddScoped<DeviationHandler>();
        services.AddScoped<QualityControlHandler>();
        services.AddScoped<StorageLocationHandler>();
        return services;
    }
}
