using Hangfire;
using Hangfire.PostgreSql;
using Smakosz.Infrastructure;
using Smakosz.Infrastructure.Logging;
using Smakosz.Infrastructure.Persistence;
using Smakosz.Application.Common.Interfaces;
using Smakosz.Orchestrator.Configuration;
using Smakosz.Orchestrator.Jobs;

var builder = WebApplication.CreateBuilder(args);

var connectionString = builder.Configuration.GetConnectionString("DefaultConnection")!;

// Infrastructure (DbContext, IFileStorageService, IEmailService, IDateTimeProvider)
builder.Services.AddInfrastructure(connectionString, builder.Configuration);

// Database logging (Hangfire not ignored - we want job error logs)
builder.Logging.AddDatabaseLogger(opts => opts.IgnoredPrefixes = ["Microsoft", "System"]);

// Concrete DbContext for UserReaperService (needs IgnoreQueryFilters)
builder.Services.AddScoped<SmakoszDbContext>();

// Configuration
builder.Services.Configure<GpuWorkerOptions>(builder.Configuration.GetSection(GpuWorkerOptions.SectionName));
builder.Services.Configure<RpiGatewayOptions>(builder.Configuration.GetSection(RpiGatewayOptions.SectionName));
builder.Services.Configure<NcfTrainingOptions>(builder.Configuration.GetSection(NcfTrainingOptions.SectionName));

// HttpClients
var gpuUrl = builder.Configuration.GetSection("GpuWorker")["Url"] ?? "http://localhost:8000";
var rpiUrl = builder.Configuration.GetSection("RpiGateway")["Url"] ?? "http://localhost:5000";

builder.Services.AddHttpClient("GpuWorker", c => c.BaseAddress = new Uri(gpuUrl))
    .ConfigureHttpClient(c => c.Timeout = TimeSpan.FromSeconds(10));
builder.Services.AddHttpClient("RpiGateway", c => c.BaseAddress = new Uri(rpiUrl))
    .ConfigureHttpClient(c => c.Timeout = TimeSpan.FromSeconds(10));

// Hangfire
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

// Job services
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
builder.Services.AddScoped<SiteStatsService>();

var app = builder.Build();

app.MapHangfireDashboard("/hangfire");

// Recurring jobs - UTC timezone
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

RecurringJob.AddOrUpdate<SiteStatsService>(
    "site-stats", x => x.UpdateAsync(CancellationToken.None), "*/10 * * * *", utc);

await app.RunAsync();
