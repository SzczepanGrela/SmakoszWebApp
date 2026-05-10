using FluentAssertions;
using Prometheus;
using Smakosz.Infrastructure.Metrics;

namespace Smakosz.UnitTests.Metrics;

public class BusinessMetricsTests
{
    private static async Task<string> CollectAsync(CollectorRegistry registry)
    {
        using var stream = new MemoryStream();
        await registry.CollectAndExportAsTextAsync(stream);
        stream.Position = 0;
        using var reader = new StreamReader(stream);
        return await reader.ReadToEndAsync();
    }

    [Fact]
    public async Task RecordRegistration_IncrementsCounter_WithCorrectLabel()
    {
        var registry = Prometheus.Metrics.NewCustomRegistry();
        var sut = new BusinessMetrics(Prometheus.Metrics.WithCustomRegistry(registry));

        sut.RecordRegistration("success");
        sut.RecordRegistration("success");
        sut.RecordRegistration("email_taken");

        var output = await CollectAsync(registry);
        output.Should().Contain("app_registrations_total{outcome=\"success\"} 2");
        output.Should().Contain("app_registrations_total{outcome=\"email_taken\"} 1");
    }

    [Fact]
    public async Task RecordLogin_AcceptsAllOutcomes()
    {
        var registry = Prometheus.Metrics.NewCustomRegistry();
        var sut = new BusinessMetrics(Prometheus.Metrics.WithCustomRegistry(registry));

        sut.RecordLogin("success");
        sut.RecordLogin("wrong_password");
        sut.RecordLogin("email_not_verified");
        sut.RecordLogin("2fa_required");
        sut.RecordLogin("account_locked");

        var output = await CollectAsync(registry);
        output.Should().Contain("app_logins_total{outcome=\"success\"} 1");
        output.Should().Contain("app_logins_total{outcome=\"wrong_password\"} 1");
        output.Should().Contain("app_logins_total{outcome=\"2fa_required\"} 1");
        output.Should().Contain("app_logins_total{outcome=\"account_locked\"} 1");
    }

    [Fact]
    public async Task RecordReviewSubmitted_IncrementsUnlabeledCounter()
    {
        var registry = Prometheus.Metrics.NewCustomRegistry();
        var sut = new BusinessMetrics(Prometheus.Metrics.WithCustomRegistry(registry));

        sut.RecordReviewSubmitted();
        sut.RecordReviewSubmitted();

        var output = await CollectAsync(registry);
        output.Should().Contain("app_reviews_submitted_total 2");
    }

    [Fact]
    public async Task RecordPhotoUpload_DistinguishesTargets()
    {
        var registry = Prometheus.Metrics.NewCustomRegistry();
        var sut = new BusinessMetrics(Prometheus.Metrics.WithCustomRegistry(registry));

        sut.RecordPhotoUpload("review");
        sut.RecordPhotoUpload("dish");
        sut.RecordPhotoUpload("dish");
        sut.RecordPhotoUpload("restaurant");

        var output = await CollectAsync(registry);
        output.Should().Contain("app_photo_uploads_total{target=\"review\"} 1");
        output.Should().Contain("app_photo_uploads_total{target=\"dish\"} 2");
        output.Should().Contain("app_photo_uploads_total{target=\"restaurant\"} 1");
    }

    [Fact]
    public async Task SetModerationQueueDepth_SetsGaugeValue()
    {
        var registry = Prometheus.Metrics.NewCustomRegistry();
        var sut = new BusinessMetrics(Prometheus.Metrics.WithCustomRegistry(registry));

        sut.SetModerationQueueDepth("review", 12);
        sut.SetModerationQueueDepth("photo", 5);
        sut.SetModerationQueueDepth("report", 0);
        sut.SetModerationQueueDepth("review", 8);

        var output = await CollectAsync(registry);
        output.Should().Contain("app_moderation_queue_depth{kind=\"review\"} 8");
        output.Should().Contain("app_moderation_queue_depth{kind=\"photo\"} 5");
        output.Should().Contain("app_moderation_queue_depth{kind=\"report\"} 0");
    }

    [Fact]
    public async Task Constructor_PreInitializesAllKnownLabels_ExposesZeroValuedSeries()
    {
        var registry = Prometheus.Metrics.NewCustomRegistry();
        _ = new BusinessMetrics(Prometheus.Metrics.WithCustomRegistry(registry));

        var output = await CollectAsync(registry);

        var registrationOutcomes = new[]
        {
            "success", "email_taken", "username_taken", "username_forbidden",
            "identifier_banned", "captcha_failed"
        };
        foreach (var outcome in registrationOutcomes)
            output.Should().Contain($"app_registrations_total{{outcome=\"{outcome}\"}} 0");

        var loginOutcomes = new[]
        {
            "success", "wrong_password", "account_locked", "account_inactive",
            "email_not_verified", "2fa_required", "captcha_failed"
        };
        foreach (var outcome in loginOutcomes)
            output.Should().Contain($"app_logins_total{{outcome=\"{outcome}\"}} 0");

        foreach (var outcome in new[] { "success", "invalid_session", "user_inactive" })
            output.Should().Contain($"app_jwt_refresh_total{{outcome=\"{outcome}\"}} 0");

        var recommendationOutcomes = new[]
        {
            "hit", "anonymous", "provider_unavailable", "ncf_disabled",
            "newcomer", "cold_computed", "compute_failed", "empty_after_filter"
        };
        foreach (var outcome in recommendationOutcomes)
            output.Should().Contain($"app_recommendation_cache_lookups_total{{outcome=\"{outcome}\"}} 0");

        var moderationLabels = new[]
        {
            ("review", "approved"), ("review", "rejected"),
            ("photo", "approved"), ("photo", "rejected")
        };
        foreach (var (kind, verdict) in moderationLabels)
            output.Should().Contain($"app_moderation_decisions_total{{kind=\"{kind}\",verdict=\"{verdict}\"}} 0");

        var photoTargets = new[] { "restaurant", "dish", "review", "user", "hero", "other" };
        foreach (var target in photoTargets)
            output.Should().Contain($"app_photo_uploads_total{{target=\"{target}\"}} 0");
    }

    [Fact]
    public async Task RecordRegistration_AfterPreInit_IncrementsFromZeroBaseline()
    {
        var registry = Prometheus.Metrics.NewCustomRegistry();
        var sut = new BusinessMetrics(Prometheus.Metrics.WithCustomRegistry(registry));

        sut.RecordRegistration("username_forbidden");

        var output = await CollectAsync(registry);
        output.Should().Contain("app_registrations_total{outcome=\"username_forbidden\"} 1");
        output.Should().Contain("app_registrations_total{outcome=\"success\"} 0");
        output.Should().Contain("app_registrations_total{outcome=\"identifier_banned\"} 0");
    }
}
