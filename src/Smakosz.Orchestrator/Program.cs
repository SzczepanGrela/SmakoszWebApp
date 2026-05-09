using Hangfire;
using Hangfire.PostgreSql;
using HealthChecks.UI.Client;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Prometheus;
using Smakosz.Infrastructure;
using Smakosz.Infrastructure.Logging;
using Smakosz.Infrastructure.Persistence;
using Smakosz.Application.Common.Interfaces;
using Smakosz.Orchestrator.Authorization;
using Smakosz.Orchestrator.Configuration;
using Smakosz.Orchestrator.HealthChecks;
using Smakosz.Orchestrator.Jobs;
using Smakosz.Orchestrator.Middleware;
using Smakosz.Orchestrator.Services;

var builder = WebApplication.CreateBuilder(args);

var connectionString = builder.Configuration.GetConnectionString("DefaultConnection")!;

builder.Services.AddInfrastructureCore(connectionString);
builder.Services.AddInfrastructureStorage(builder.Configuration);
builder.Services.AddInfrastructureMessaging(builder.Configuration);
builder.Services.AddInfrastructureModels(builder.Configuration);
builder.Services.AddInfrastructureRecommendations(builder.Configuration);
builder.Services.AddInfrastructureExternalServices(builder.Configuration);

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

builder.Services.Configure<NcfTrainingOptions>(builder.Configuration.GetSection(NcfTrainingOptions.SectionName));
builder.Services.Configure<NodesOptions>(builder.Configuration.GetSection(NodesOptions.SectionName));
builder.Services.AddHostedService<Smakosz.Orchestrator.Services.NodeRegistrarHostedService>();

builder.Services.AddHangfire(cfg => cfg
    .SetDataCompatibilityLevel(CompatibilityLevel.Version_180)
    .UseSimpleAssemblyNameTypeSerializer()
    .UseRecommendedSerializerSettings()
    .UsePostgreSqlStorage(o => o.UseNpgsqlConnection(connectionString), new PostgreSqlStorageOptions
    {
        QueuePollInterval = TimeSpan.FromSeconds(60),
    }));

builder.Services.AddHangfireServer(options =>
{
    options.WorkerCount = 2;
    options.Queues = ["default"];
    options.SchedulePollingInterval = TimeSpan.FromSeconds(60);
});

builder.Services.AddHealthChecks()
    .AddCheck<HangfireServerHealthCheck>("hangfire_server", timeout: TimeSpan.FromSeconds(3), tags: new[] { "ready" });

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
builder.Services.AddScoped<INcfTrainingDatasetBuilder, NcfTrainingDatasetBuilder>();
builder.Services.AddScoped<NcfTrainingService>();
builder.Services.AddScoped<INcfTrainingService>(sp => sp.GetRequiredService<NcfTrainingService>());
builder.Services.AddScoped<NotificationDigestService>();
builder.Services.AddScoped<PushNotificationDispatchService>();
builder.Services.AddScoped<NcfModelActivationService>();
builder.Services.AddScoped<SendSecurityEmailJob>();
builder.Services.AddScoped<ISendSecurityEmailJob>(sp => sp.GetRequiredService<SendSecurityEmailJob>());
builder.Services.AddScoped<ModerationBatchAggregatorService>();
builder.Services.AddScoped<IModerationAggregationService>(sp => sp.GetRequiredService<ModerationBatchAggregatorService>());
builder.Services.AddScoped<ModerationAggregationSchedulerService>();
builder.Services.AddScoped<ModerationQueueDepthSamplerService>();
builder.Services.AddScoped<SystemJobsCleanupService>();
builder.Services.AddScoped<SystemLogsCleanupService>();
builder.Services.AddScoped<SoftDeletedReviewsCleanupService>();
builder.Services.AddScoped<DataCorrectionEscalationService>();
builder.Services.AddScoped<LogRetentionService>();

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseAuthentication();
app.UseAuthorization();
app.UseHttpMetrics();
app.MapControllers();
app.MapMetrics("/metrics");
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

app.MapHangfireDashboard("/hangfire", new DashboardOptions
{
    Authorization = new[] { new TailnetDashboardAuthorizationFilter() }
});

var utc = new RecurringJobOptions { TimeZone = TimeZoneInfo.Utc };

RecurringJob.AddOrUpdate<SessionCleanupService>(
    "session-cleanup", x => x.CleanupAsync(CancellationToken.None), Cron.Hourly, utc);

RecurringJob.AddOrUpdate<NotificationCleanupService>(
    "prune-notifications", x => x.PruneAsync(CancellationToken.None), Cron.Daily(3), utc);

RecurringJob.AddOrUpdate<StuckJobsRecoveryService>(
    "stuck-jobs-recovery", x => x.RecoverAsync(CancellationToken.None), "*/15 * * * *", utc);

RecurringJob.AddOrUpdate<UserReaperService>(
    "user-reaper", x => x.ReapAsync(CancellationToken.None), Cron.Daily(2), utc);

RecurringJob.AddOrUpdate<RatingService>(
    "avg-ratings", x => x.UpdateAsync(CancellationToken.None), "0 */6 * * *", utc);

RecurringJob.AddOrUpdate<TrendingService>(
    "trending-scores", x => x.RecalculateAsync(CancellationToken.None), Cron.Daily(4), utc);

RecurringJob.AddOrUpdate<R2CleanupService>(
    "r2-cleanup", x => x.CleanupAsync(CancellationToken.None), Cron.Hourly, utc);

RecurringJob.AddOrUpdate<HeartbeatMonitorService>(
    "heartbeat-monitor", x => x.CheckAsync(CancellationToken.None), "*/5 * * * *", utc);

RecurringJob.AddOrUpdate<NcfTrainingService>(
    "ncf-training", x => x.ScheduleAsync(CancellationToken.None), Cron.Daily(22), utc);

RecurringJob.AddOrUpdate<NotificationDigestService>(
    "notification-digest", x => x.SendAsync(CancellationToken.None), Cron.Daily(8), utc);

RecurringJob.AddOrUpdate<PushNotificationDispatchService>(
    "push-dispatch", x => x.SendAsync(CancellationToken.None), "*/2 * * * *", utc);

RecurringJob.AddOrUpdate<HomePageCacheService>(
    "home-page-cache", x => x.RefreshAsync(CancellationToken.None), "*/30 * * * *", utc);

RecurringJob.AddOrUpdate<ModerationAggregationSchedulerService>(
    "moderation-aggregation", x => x.RunAsync(CancellationToken.None), "*/5 * * * *", utc);

RecurringJob.AddOrUpdate<ModerationQueueDepthSamplerService>(
    "moderation-queue-depth-sampler",
    x => x.SampleAsync(CancellationToken.None),
    "*/1 * * * *",
    utc);

RecurringJob.AddOrUpdate<SystemJobsCleanupService>(
    "system-jobs-cleanup", x => x.CleanupAsync(CancellationToken.None), Cron.Daily(2, 30), utc);

RecurringJob.AddOrUpdate<SystemLogsCleanupService>(
    "system-logs-cleanup", x => x.CleanupAsync(CancellationToken.None), Cron.Daily(3, 15), utc);

RecurringJob.AddOrUpdate<SoftDeletedReviewsCleanupService>(
    "soft-deleted-reviews-cleanup", x => x.CleanupAsync(CancellationToken.None), Cron.Daily(3, 30), utc);

RecurringJob.AddOrUpdate<DataCorrectionEscalationService>(
    "data-correction-escalation", x => x.EscalateAsync(CancellationToken.None), Cron.Hourly, utc);

RecurringJob.AddOrUpdate<LogRetentionService>(
    "log-retention", x => x.CleanupAsync(CancellationToken.None), Cron.Daily(3, 45), utc);

RecurringJob.TriggerJob("home-page-cache");
RecurringJob.TriggerJob("moderation-aggregation");
RecurringJob.TriggerJob("heartbeat-monitor");

await app.RunAsync();
