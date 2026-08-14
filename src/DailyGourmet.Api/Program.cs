using DailyGourmet.Api.Endpoints;
using DailyGourmet.Api.Extensions;
using DailyGourmet.Infrastructure;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddInfrastructure(builder.Configuration);
builder.Services.AddApiServices(builder.Configuration);

var app = builder.Build();

app.UseApiPipeline();

app.MapHealthEndpoints();
app.MapVersionEndpoint();

app.Run();

// Ermöglicht WebApplicationFactory<Program> in DailyGourmet.Api.IntegrationTests.
public partial class Program;
