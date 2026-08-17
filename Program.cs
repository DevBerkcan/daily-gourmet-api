using System.Text;
using DailyGourmet.Api.Authentication;
using DailyGourmet.Api.Data;
using DailyGourmet.Api.Extensions;
using DailyGourmet.Api.Handlers;
using DailyGourmet.Api.Middleware;
using DailyGourmet.Api.Models.Entities;
using DailyGourmet.Api.Options;
using DailyGourmet.Api.Repositories.Implementations;
using DailyGourmet.Api.Repositories.Interfaces;
using DailyGourmet.Api.Services;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi.Models;

var builder = WebApplication.CreateBuilder(args);

// ---- Options ----
builder.Services.Configure<JwtOptions>(builder.Configuration.GetSection(JwtOptions.SectionName));
builder.Services.Configure<SmtpOptions>(builder.Configuration.GetSection(SmtpOptions.SectionName));
builder.Services.Configure<CorsOptions>(builder.Configuration.GetSection(CorsOptions.SectionName));

// ---- Data ----
builder.Services.AddScoped<ITenantContext, TenantContext>();
builder.Services.AddDbContext<DailyGourmetDbContext>(options =>
    options.UseSqlServer(builder.Configuration.GetConnectionString("DefaultConnection")));

// ---- Auth ----
builder.Services.AddSingleton<IJwtTokenService, JwtTokenService>();
builder.Services.AddSingleton<IPasswordHasher<User>, PasswordHasher<User>>();

var jwtOptions = builder.Configuration.GetSection(JwtOptions.SectionName).Get<JwtOptions>() ?? new JwtOptions();
builder.Services.AddAuthentication(options =>
    {
        options.DefaultAuthenticateScheme = JwtBearerDefaults.AuthenticationScheme;
        options.DefaultChallengeScheme = JwtBearerDefaults.AuthenticationScheme;
    })
    .AddJwtBearer(options =>
    {
        // Keep claim types exactly as issued (e.g. "sub" stays "sub") instead of ASP.NET Core's
        // legacy inbound remapping to XML-schema URIs — simpler and predictable for
        // TenantContextMiddleware to read.
        options.MapInboundClaims = false;
        options.RequireHttpsMetadata = !builder.Environment.IsDevelopment();
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidIssuer = jwtOptions.Issuer,
            ValidateAudience = true,
            ValidAudience = jwtOptions.Audience,
            ValidateLifetime = true,
            ValidateIssuerSigningKey = true,
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(
                string.IsNullOrWhiteSpace(jwtOptions.Secret) ? new string('0', 32) : jwtOptions.Secret)),
            ClockSkew = TimeSpan.FromMinutes(1),
        };
    });
builder.Services.AddAuthorization();

// ---- CORS ----
var corsOptions = builder.Configuration.GetSection(CorsOptions.SectionName).Get<CorsOptions>() ?? new CorsOptions();
const string CorsPolicyName = "Frontend";
builder.Services.AddCors(options =>
{
    options.AddPolicy(CorsPolicyName, policy =>
    {
        if (corsOptions.AllowedOrigins.Length > 0)
            policy.WithOrigins(corsOptions.AllowedOrigins).AllowAnyHeader().AllowAnyMethod().AllowCredentials();
        else
            policy.SetIsOriginAllowed(_ => builder.Environment.IsDevelopment()).AllowAnyHeader().AllowAnyMethod().AllowCredentials();
    });
});

// ---- Services ----
builder.Services.AddScoped<IEmailService, EmailService>();

// ---- Repositories ----
// Open-generic registration covers every entity that doesn't need custom queries; entities that
// do (e.g. User's IgnoreQueryFilters lookups) get a dedicated interface + implementation instead.
builder.Services.AddScoped(typeof(IRepository<>), typeof(Repository<>));
builder.Services.AddScoped<IUserRepository, UserRepository>();
builder.Services.AddScoped<ITenantSettingsRepository, TenantSettingsRepository>();

// ---- Handlers ----
builder.Services.AddScoped<AuthHandler>();
builder.Services.AddScoped<FacilityHandler>();
builder.Services.AddRecipesIngredientsModule();
builder.Services.AddMealPlansOrdersModule();
builder.Services.AddProductionKitchenModule();
builder.Services.AddProcurementLogisticsModule();
builder.Services.AddPlatformAdminModule();
builder.Services.AddSupportDashboardModule();

// ---- MVC / Swagger ----
builder.Services.AddControllers()
    .AddJsonOptions(options => options.JsonSerializerOptions.Converters.Add(new System.Text.Json.Serialization.JsonStringEnumConverter()));
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(options =>
{
    options.SwaggerDoc("v1", new OpenApiInfo { Title = "DailyGourmet API", Version = "v1" });
    var jwtScheme = new OpenApiSecurityScheme
    {
        Scheme = "bearer",
        BearerFormat = "JWT",
        Name = "Authorization",
        In = ParameterLocation.Header,
        Type = SecuritySchemeType.Http,
        Description = "JWT Bearer-Token. Beispiel: \"Bearer {token}\"",
        Reference = new OpenApiReference { Type = ReferenceType.SecurityScheme, Id = "Bearer" },
    };
    options.AddSecurityDefinition("Bearer", jwtScheme);
    options.AddSecurityRequirement(new OpenApiSecurityRequirement { { jwtScheme, [] } });
});

var app = builder.Build();

if (args.Contains("--seed"))
{
    using var scope = app.Services.CreateScope();
    var db = scope.ServiceProvider.GetRequiredService<DailyGourmetDbContext>();
    await DbSeeder.SeedAsync(db);
    return;
}

app.UseMiddleware<ExceptionMiddleware>();

// CORS must run before UseHttpsRedirection: a redirect response to a CORS preflight (OPTIONS)
// request is invalid per the Fetch spec — browsers refuse to follow it and report it as a CORS
// failure, even though the real cause is the redirect. Also skip the redirect entirely in
// Development, since the local frontend talks to the API over plain HTTP (see .env.example).
app.UseCors(CorsPolicyName);

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI(options => options.SwaggerEndpoint("/swagger/v1/swagger.json", "DailyGourmet API v1"));
}
else
{
    app.UseHttpsRedirection();
}

app.UseAuthentication();
app.UseMiddleware<TenantContextMiddleware>();
app.UseAuthorization();
app.MapControllers();
app.MapGet("/api/health", () => Results.Ok(new { status = "ok" })).AllowAnonymous();

app.Run();
