using DailyGourmet.Api.Handlers;

namespace DailyGourmet.Api.Extensions;

public static class SupportDashboardServiceCollectionExtensions
{
    public static IServiceCollection AddSupportDashboardModule(this IServiceCollection services)
    {
        services.AddScoped<SupportTicketHandler>();
        services.AddScoped<SupportSessionHandler>();
        services.AddScoped<NotificationHandler>();
        services.AddScoped<DashboardHandler>();
        return services;
    }
}
