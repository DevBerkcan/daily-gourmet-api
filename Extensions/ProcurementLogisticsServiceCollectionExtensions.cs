using DailyGourmet.Api.Handlers;

namespace DailyGourmet.Api.Extensions;

public static class ProcurementLogisticsServiceCollectionExtensions
{
    public static IServiceCollection AddProcurementLogisticsModule(this IServiceCollection services)
    {
        services.AddScoped<ProcurementListHandler>();
        services.AddScoped<DeliveryRouteHandler>();
        services.AddScoped<DriverHandler>();
        return services;
    }
}
