using Hangfire;
using Hangfire.PostgreSql;
using Microsoft.AspNetCore.Authentication;
using Smakosz.Infrastructure;
using Smakosz.Infrastructure.Logging;
using Smakosz.Infrastructure.Persistence;
using Smakosz.Application.Common.Interfaces;
using Smakosz.Orchestrator.Configuration;
using Smakosz.Orchestrator.Jobs;
using Smakosz.Orchestrator.Middleware;
using Smakosz.Orchestrator.Services;

var builder = WebApplication.CreateBuilder(args);

var connectionString = builder.Configuration.GetConnectionString("DefaultConnection")!;

builder.Services.AddInfrastructureCore(connectionString);
builder.Services.AddInfrastructureStorage(builder.Configuration);
builder.Services.AddInfrastructureMessaging(builder.Configuration);
builder.Services.AddInfrastructureModels(builder.Configuration);

builder.Services.AddMediatR(cfg =>
{
    cfg.RegisterServicesFromAssembly(typeof(Smakosz.Application.DependencyInjection).Assembly);
    cfg.RegisterServicesFromAssembly(typeof(Program).Assembly);
});

builder.Services.AddScoped<ICurrentUserService, SystemCurrentUserService>();
if (string.IsNullOrEmpty(builder.Configuration.GetSection("R2Models")["AccountId"]))
    builder.Services.AddSingleton<INcfModelStorageService, StubNcfModelStorageService>();

builder.Logging.AddDatabaseLogger(opts => opts.IgnoredPrefixes = ["Microsoft", "System"]);

builder.Services.AddScoped<SmakoszDbContext>();

builder.Services.Configure<GpuWorkerOptions>(builder.Configuration.GetSection(GpuWorkerOptions.SectionName));
builder.Services.Configure<RpiGatewayOptions>(builder.Configuration.GetSection(RpiGatewayOptions.SectionName));
builder.Services.Configure<NcfTrainingOptions>(builder.Configuration.GetSection(NcfTrainingOptions.SectionName));

var gpuUrl = builder.Configuration.GetSection("GpuWorker")["Url"] ?? "http://localhost:8000";
var rpiSection = builder.Configuration.GetSection("RpiGateway");
var rpiUrl = rpiSection["Url"] ?? "http://localhost:5000";
var rpiToken = rpiSection["ApiToken"] ?? "";

builder.Services.AddHttpClient("GpuWorker", c => c.BaseAddress = new Uri(gpuUrl))
    .ConfigureHttpClient(c => c.Timeout = TimeSpan.FromSeconds(10));
builder.Services.AddHttpClient("RpiGateway", c =>
    {
        c.BaseAddress = new Uri(rpiUrl);
        if (!string.IsNullOrEmpty(rpiToken))
            c.DefaultRequestHeaders.Add("X-API-Token", rpiToken);
    })
    .ConfigureHttpClient(c => c.Timeout = TimeSpan.FromSeconds(10));

builder.Services.AddHangfire(cfg => cfg
    .SetDataCompatibilityLevel(CompatibilityLevel.Version_180)
    .UseSimpleAssemblyNameTypeSerializer()
    .UseRecommendedSerializerSettings()
    .UsePostgreSqlStorage(o => o.UseNpgsqlConnection(connectionString)));

builder.Services.AddHangfireServer(options =>
{
    options.WorkerCount = 4;
    options.Queues = ["default"];
});

builder.Services.AddHealthChecks()
    .AddDbContextCheck<SmakoszDbContext>();

builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

builder.Services.AddAuthentication("WorkerApiKey")
    .AddScheme<AuthenticationSchemeOptions, WorkerApiKeyAuthHandler>("WorkerApiKey", null);

builder.Services.AddAuthorization(options =>
    options.AddPolicy("Worker", p => p.RequireRole("Worker")));

builder.Services.AddScoped<SessionCleanupService>();
builder.Services.AddScoped<NotificationCleanupService>();
builder.Services.AddScoped<StuckJobsRecoveryService>();
builder.Services.AddScoped<UserReaperService>();
builder.Services.AddScoped<RatingService>();
builder.Services.AddScoped<TrendingService>();
builder.Services.AddScoped<R2CleanupService>();
builder.Services.AddScoped<HeartbeatMonitorService>();
builder.Services.AddScoped<NcfTrainingService>();
builder.Services.AddScoped<INcfTrainingService>(sp => sp.GetRequiredService<NcfTrainingService>());
builder.Services.AddScoped<NotificationDigestService>();
builder.Services.AddScoped<PushNotificationDispatchService>();
builder.Services.AddScoped<SiteStatsService>();
builder.Services.AddScoped<NcfModelActivationService>();
builder.Services.AddScoped<ModerationBatchAggregatorService>();
builder.Services.AddScoped<IModerationAggregationService>(sp => sp.GetRequiredService<ModerationBatchAggregatorService>());
builder.Services.AddScoped<ModerationAggregationSchedulerService>();
builder.Services.AddScoped<SystemJobsCleanupService>();
builder.Services.AddScoped<SystemLogsCleanupService>();
builder.Services.AddScoped<SoftDeletedReviewsCleanupService>();

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseAuthentication();
app.UseAuthorization();
app.MapControllers();
app.MapHealthChecks("/health");

app.MapHangfireDashboard("/hangfire");

var utc = new RecurringJobOptions { TimeZone = TimeZoneInfo.Utc };

RecurringJob.AddOrUpdate<SessionCleanupService>(
    "session-cleanup", x => x.CleanupAsync(CancellationToken.None), Cron.Hourly, utc);

RecurringJob.AddOrUpdate<NotificationCleanupService>(
    "prune-notifications", x => x.PruneAsync(CancellationToken.None), Cron.Daily(3), utc);

RecurringJob.AddOrUpdate<StuckJobsRecoveryService>(
    "stuck-jobs-recovery", x => x.RecoverAsync(CancellationToken.None), Cron.Hourly, utc);

RecurringJob.AddOrUpdate<UserReaperService>(
    "user-reaper", x => x.ReapAsync(CancellationToken.None), Cron.Daily(2), utc);

RecurringJob.AddOrUpdate<RatingService>(
    "avg-ratings", x => x.UpdateAsync(CancellationToken.None), Cron.Hourly, utc);

RecurringJob.AddOrUpdate<TrendingService>(
    "trending-scores", x => x.RecalculateAsync(CancellationToken.None), Cron.Daily(4), utc);

RecurringJob.AddOrUpdate<R2CleanupService>(
    "r2-cleanup", x => x.CleanupAsync(CancellationToken.None), "*/15 * * * *", utc);

RecurringJob.AddOrUpdate<HeartbeatMonitorService>(
    "heartbeat-monitor", x => x.CheckAsync(CancellationToken.None), "*/5 * * * *", utc);

RecurringJob.AddOrUpdate<NcfTrainingService>(
    "ncf-training", x => x.ScheduleAsync(CancellationToken.None), Cron.Daily(22), utc);

RecurringJob.AddOrUpdate<NotificationDigestService>(
    "notification-digest", x => x.SendAsync(CancellationToken.None), Cron.Daily(8), utc);

RecurringJob.AddOrUpdate<PushNotificationDispatchService>(
    "push-dispatch", x => x.SendAsync(CancellationToken.None), Cron.Minutely, utc);

RecurringJob.AddOrUpdate<SiteStatsService>(
    "site-stats", x => x.UpdateAsync(CancellationToken.None), "*/10 * * * *", utc);

RecurringJob.AddOrUpdate<HomePageCacheService>(
    "home-page-cache", x => x.RefreshAsync(CancellationToken.None), "*/5 * * * *", utc);

RecurringJob.AddOrUpdate<ModerationAggregationSchedulerService>(
    "moderation-aggregation", x => x.RunAsync(CancellationToken.None), Cron.Minutely, utc);

RecurringJob.AddOrUpdate<SystemJobsCleanupService>(
    "system-jobs-cleanup", x => x.CleanupAsync(CancellationToken.None), Cron.Daily(2, 30), utc);

RecurringJob.AddOrUpdate<SystemLogsCleanupService>(
    "system-logs-cleanup", x => x.CleanupAsync(CancellationToken.None), Cron.Daily(3, 15), utc);

RecurringJob.AddOrUpdate<SoftDeletedReviewsCleanupService>(
    "soft-deleted-reviews-cleanup", x => x.CleanupAsync(CancellationToken.None), Cron.Daily(3, 30), utc);

await app.RunAsync();
