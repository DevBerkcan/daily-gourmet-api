using DailyGourmet.Api.Handlers;

namespace DailyGourmet.Api.Extensions;

public static class PlatformAdminServiceCollectionExtensions
{
    public static IServiceCollection AddPlatformAdminModule(this IServiceCollection services)
    {
        services.AddScoped<SuperAdminHandler>();
        services.AddScoped<TenantHandler>();
        services.AddScoped<UserManagementHandler>();
        services.AddScoped<LocationHandler>();
        services.AddScoped<AuditLogHandler>();
        return services;
    }
}
