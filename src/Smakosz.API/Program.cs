using System.Security.Cryptography;
using System.Threading.RateLimiting;
using Hangfire;
using Hangfire.PostgreSql;
using HealthChecks.UI.Client;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi.Models;
using Prometheus;
using Smakosz.API.Common;
using Smakosz.API.Middleware;
using Smakosz.API.Services;
using Smakosz.Application;
using Smakosz.Application.Common.Interfaces;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Smakosz.Infrastructure;
using Smakosz.Infrastructure.Configuration;
using Smakosz.Infrastructure.HealthChecks;
using Smakosz.Infrastructure.Logging;
using Smakosz.Infrastructure.Persistence;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddApplication();
builder.Services.AddInfrastructureCore(
    builder.Configuration.GetConnectionString("DefaultConnection")!);
builder.Services.AddInfrastructureAuth(builder.Configuration);
builder.Services.AddInfrastructureStorage(builder.Configuration);
builder.Services.AddInfrastructureRecommendations(builder.Configuration);
builder.Services.AddInfrastructureMessaging(builder.Configuration);
builder.Services.AddInfrastructureExternalServices(builder.Configuration);

if (!builder.Environment.IsEnvironment("Testing"))
{
    builder.Services.AddHangfire(cfg => cfg
        .SetDataCompatibilityLevel(CompatibilityLevel.Version_180)
        .UseSimpleAssemblyNameTypeSerializer()
        .UseRecommendedSerializerSettings()
        .UsePostgreSqlStorage(o => o.UseNpgsqlConnection(
            builder.Configuration.GetConnectionString("DefaultConnection")!)));
    builder.Logging.AddDatabaseLogger();
}

builder.Services.AddScoped<INcfTrainingService, HangfireNcfTrainingProxy>();
builder.Services.AddScoped<IModerationAggregationService, HangfireModerationProxy>();

builder.Services.AddHttpContextAccessor();
builder.Services.AddScoped<ICurrentUserService, CurrentUserService>();

builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer();

builder.Services.AddOptions<JwtBearerOptions>(JwtBearerDefaults.AuthenticationScheme)
    .Configure<IOptions<JwtOptions>>((options, jwtOptions) =>
    {
        var jwt = jwtOptions.Value;
        var rsa = RSA.Create();
        rsa.ImportFromPem(jwt.ResolvePublicKey());
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidateAudience = true,
            ValidateLifetime = true,
            ValidateIssuerSigningKey = true,
            ValidIssuer = jwt.Issuer,
            ValidAudience = jwt.Audience,
            IssuerSigningKey = new RsaSecurityKey(rsa),
            ClockSkew = TimeSpan.Zero
        };
    });

builder.Services.AddAuthorization(options =>
{
    options.AddPolicy("AdminOnly", p => p.RequireRole("Admin", "Moderator"));
    options.AddPolicy("RestaurantOwner", p => p.RequireRole("Restaurant"));
});

builder.Services.AddHealthChecks()
    .AddDbContextCheck<SmakoszDbContext>("database", tags: new[] { "ready" })
    .AddCheck<R2HealthCheck>("r2_photos", timeout: TimeSpan.FromSeconds(3), tags: new[] { "ready" });

builder.Services.AddControllers();

builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(options =>
{
    options.SwaggerDoc("v1", new OpenApiInfo
    {
        Title = "Smakosz API",
        Version = "v1",
        Description = "API systemu recenzji restauracji i dań Smakosz"
    });

    options.AddSecurityDefinition("Bearer", new OpenApiSecurityScheme
    {
        Name = "Authorization",
        Type = SecuritySchemeType.Http,
        Scheme = "bearer",
        BearerFormat = "JWT",
        In = ParameterLocation.Header,
        Description = "Wprowadz token JWT"
    });

    options.AddSecurityRequirement(new OpenApiSecurityRequirement
    {
        {
            new OpenApiSecurityScheme
            {
                Reference = new OpenApiReference
                {
                    Type = ReferenceType.SecurityScheme,
                    Id = "Bearer"
                }
            },
            Array.Empty<string>()
        }
    });
});

var allowedOrigins = builder.Configuration.GetSection("Cors:AllowedOrigins").Get<string[]>() ?? [];
builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowClient", policy =>
    {
        policy.WithOrigins(allowedOrigins)
              .AllowAnyHeader()
              .AllowAnyMethod()
              .AllowCredentials();
    });
});

builder.Services.Configure<ForwardedHeadersOptions>(opts =>
{
    opts.ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto;
    opts.ForwardLimit = 2;
    opts.KnownIPNetworks.Add(System.Net.IPNetwork.Parse("172.16.0.0/12"));
});

builder.Services.AddRateLimiter(options =>
{
    options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;

    options.OnRejected = async (context, ct) =>
    {
        var retrySeconds = context.Lease.TryGetMetadata(MetadataName.RetryAfter, out var retryAfter)
            ? (int)retryAfter.TotalSeconds
            : 60;

        context.HttpContext.Response.Headers.RetryAfter = retrySeconds.ToString();

        await context.HttpContext.Response.WriteAsJsonAsync(new
        {
            success = false,
            error = new { code = "RATE_LIMIT_EXCEEDED", message = "Zbyt wiele zapytan. Sprobuj ponownie pozniej." }
        }, ct);
    };

    AddFixedWindowPolicy(options, "auth", "ratelimit.auth", 10, 60);
    AddFixedWindowPolicy(options, "upload", "ratelimit.upload", 10, 60);
    AddFixedWindowPolicy(options, "search", "ratelimit.search", 30, 60);
    AddFixedWindowPolicy(options, "general", "ratelimit.general", 60, 60);
});

var healthKey = builder.Configuration["Monitoring:HealthCheckKey"];
if (builder.Environment.IsProduction() && string.IsNullOrWhiteSpace(healthKey))
    throw new InvalidOperationException("Monitoring:HealthCheckKey must be set in production");

var app = builder.Build();

if (!app.Environment.IsEnvironment("Testing"))
{
    using var scope = app.Services.CreateScope();
    var db = scope.ServiceProvider.GetRequiredService<SmakoszDbContext>();
    await db.Database.MigrateAsync();
    await db.ApplySqlObjectsAsync();
}

app.UseForwardedHeaders();

app.UseMiddleware<ExceptionHandlingMiddleware>();

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseCors("AllowClient");
if (!app.Environment.IsEnvironment("E2E"))
    app.UseRateLimiter();
app.UseAuthentication();
app.UseAuthorization();
app.UseHttpMetrics();
app.MapControllers();
app.MapMetrics("/metrics");
app.UseMiddleware<HealthCheckAuthMiddleware>();

app.MapHealthChecks("/health/live", new HealthCheckOptions
{
    Predicate = _ => false
});

app.MapHealthChecks("/health/ready", new HealthCheckOptions
{
    Predicate = r => r.Tags.Contains("ready"),
    ResponseWriter = UIResponseWriter.WriteHealthCheckUIResponse
});

app.MapHealthChecks("/health", new HealthCheckOptions
{
    Predicate = _ => false
});

await app.RunAsync();

public partial class Program
{
    static void AddFixedWindowPolicy(RateLimiterOptions options, string policyName,
        string configPrefix, int defaultPermit, int defaultWindow)
    {
        options.AddPolicy(policyName, context =>
        {
            var config = context.RequestServices.GetRequiredService<IValidationConfigProvider>();
            return RateLimitPartition.GetFixedWindowLimiter(
                partitionKey: context.Connection.RemoteIpAddress?.ToString() ?? "unknown",
                factory: _ => new FixedWindowRateLimiterOptions
                {
                    PermitLimit = config.GetInt($"{configPrefix}.permit_limit", defaultPermit),
                    Window = TimeSpan.FromSeconds(config.GetInt($"{configPrefix}.window_seconds", defaultWindow)),
                    AutoReplenishment = true
                });
        });
    }
}
