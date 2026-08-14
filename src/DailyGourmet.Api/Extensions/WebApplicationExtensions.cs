namespace DailyGourmet.Api.Extensions;

public static class WebApplicationExtensions
{
    public static WebApplication UseApiPipeline(this WebApplication app)
    {
        app.Use(async (context, next) =>
        {
            context.Response.OnStarting(() =>
            {
                context.Response.Headers["X-Request-Id"] = context.TraceIdentifier;
                return Task.CompletedTask;
            });

            await next();
        });

        app.UseExceptionHandler();

        if (!app.Environment.IsDevelopment())
        {
            app.UseHsts();
            app.UseHttpsRedirection();
        }

        app.UseCors(CorsPolicies.Frontend);

        app.UseRateLimiter();

        app.UseAuthentication();
        app.UseAuthorization();

        app.MapOpenApi();

        return app;
    }
}
