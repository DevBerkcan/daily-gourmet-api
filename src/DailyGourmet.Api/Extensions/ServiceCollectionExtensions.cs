using System.Text.Json;
using System.Threading.RateLimiting;
using DailyGourmet.Api.ExceptionHandling;
using DailyGourmet.Api.RateLimiting;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.RateLimiting;

namespace DailyGourmet.Api.Extensions;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddApiServices(this IServiceCollection services, IConfiguration configuration)
    {
        services.ConfigureHttpJsonOptions(options =>
            options.SerializerOptions.PropertyNamingPolicy = JsonNamingPolicy.CamelCase);

        services.AddExceptionHandler<GlobalExceptionHandler>();
        services.AddProblemDetails();

        services.AddOpenApi();

        services.AddCors(options => options.AddPolicy(CorsPolicies.Frontend, policy =>
        {
            var frontendOrigin = configuration["Cors:FrontendOrigin"]
                ?? throw new InvalidOperationException("Cors:FrontendOrigin ist nicht konfiguriert.");

            policy.WithOrigins(frontendOrigin)
                .AllowAnyHeader()
                .AllowAnyMethod()
                .AllowCredentials();
        }));

        services.AddRateLimiter(options =>
        {
            options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;

            options.AddFixedWindowLimiter(RateLimiterPolicies.Auth, limiterOptions =>
            {
                limiterOptions.PermitLimit = 5;
                limiterOptions.Window = TimeSpan.FromMinutes(1);
                limiterOptions.QueueProcessingOrder = QueueProcessingOrder.OldestFirst;
                limiterOptions.QueueLimit = 0;
            });
        });

        services.AddAuthentication(CookieAuthenticationDefaults.AuthenticationScheme)
            .AddCookie(options =>
            {
                options.Cookie.Name = "dg_session";
                options.Cookie.HttpOnly = true;
                options.Cookie.SecurePolicy = CookieSecurePolicy.Always;
                options.Cookie.SameSite = SameSiteMode.Lax;
                options.ExpireTimeSpan = TimeSpan.FromHours(12);
                options.SlidingExpiration = true;

                // Cookie-Auth ohne Redirect-basierte Login-Seite — API antwortet mit 401/403 statt Redirect.
                options.Events.OnRedirectToLogin = context =>
                {
                    context.Response.StatusCode = StatusCodes.Status401Unauthorized;
                    return Task.CompletedTask;
                };
                options.Events.OnRedirectToAccessDenied = context =>
                {
                    context.Response.StatusCode = StatusCodes.Status403Forbidden;
                    return Task.CompletedTask;
                };
            });

        // Konkrete Permission-Policies folgen in Phase 2, sobald das Rollen-/Permission-Modell existiert.
        services.AddAuthorization();

        return services;
    }
}
