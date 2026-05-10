using Prometheus;
using Smakosz.Application.Common.Interfaces;

namespace Smakosz.Infrastructure.Metrics;

public class BusinessMetrics : IBusinessMetrics
{
    private static readonly IReadOnlyList<string> RegistrationOutcomes = new[]
    {
        "success", "email_taken", "username_taken", "username_forbidden",
        "identifier_banned", "captcha_failed"
    };

    private static readonly IReadOnlyList<string> LoginOutcomes = new[]
    {
        "success", "wrong_password", "account_locked", "account_inactive",
        "email_not_verified", "2fa_required", "captcha_failed"
    };

    private static readonly IReadOnlyList<string> JwtRefreshOutcomes = new[]
    {
        "success", "invalid_session", "user_inactive"
    };

    private static readonly IReadOnlyList<string> RecommendationCacheOutcomes = new[]
    {
        "hit", "anonymous", "provider_unavailable", "ncf_disabled",
        "newcomer", "cold_computed", "compute_failed", "empty_after_filter"
    };

    private static readonly IReadOnlyList<(string Kind, string Verdict)> ModerationDecisionLabels = new[]
    {
        ("review", "approved"), ("review", "rejected"),
        ("photo", "approved"), ("photo", "rejected")
    };

    private static readonly IReadOnlyList<string> PhotoUploadTargets = new[]
    {
        "restaurant", "dish", "review", "user", "hero", "other"
    };

    private readonly Counter _registrations;
    private readonly Counter _logins;
    private readonly Counter _reviewsSubmitted;
    private readonly Counter _photoUploads;
    private readonly Gauge _moderationQueueDepth;
    private readonly Counter _validationFailures;
    private readonly Counter _moderationDecisions;
    private readonly Counter _recommendationCacheLookups;
    private readonly Counter _jwtRefreshes;

    public BusinessMetrics(IMetricFactory factory)
    {
        _registrations = factory.CreateCounter(
            "app_registrations_total",
            "Total user registration attempts.",
            new CounterConfiguration { LabelNames = new[] { "outcome" } });
        foreach (var outcome in RegistrationOutcomes)
            _registrations.WithLabels(outcome).IncTo(0);

        _logins = factory.CreateCounter(
            "app_logins_total",
            "Total login attempts.",
            new CounterConfiguration { LabelNames = new[] { "outcome" } });
        foreach (var outcome in LoginOutcomes)
            _logins.WithLabels(outcome).IncTo(0);

        _reviewsSubmitted = factory.CreateCounter(
            "app_reviews_submitted_total",
            "Total reviews successfully submitted.");

        _photoUploads = factory.CreateCounter(
            "app_photo_uploads_total",
            "Total photos uploaded.",
            new CounterConfiguration { LabelNames = new[] { "target" } });
        foreach (var target in PhotoUploadTargets)
            _photoUploads.WithLabels(target).IncTo(0);

        _moderationQueueDepth = factory.CreateGauge(
            "app_moderation_queue_depth",
            "Pending items awaiting moderation.",
            new GaugeConfiguration { LabelNames = new[] { "kind" } });

        _validationFailures = factory.CreateCounter(
            "app_validation_failed_total",
            "Requests rejected by FluentValidation pipeline before the handler ran.",
            new CounterConfiguration { LabelNames = new[] { "request_type" } });

        _moderationDecisions = factory.CreateCounter(
            "app_moderation_decisions_total",
            "Moderator verdicts applied to user generated content.",
            new CounterConfiguration { LabelNames = new[] { "kind", "verdict" } });
        foreach (var (kind, verdict) in ModerationDecisionLabels)
            _moderationDecisions.WithLabels(kind, verdict).IncTo(0);

        _recommendationCacheLookups = factory.CreateCounter(
            "app_recommendation_cache_lookups_total",
            "Outcomes of recommendation cache lookups served from the home and recommendations endpoints.",
            new CounterConfiguration { LabelNames = new[] { "outcome" } });
        foreach (var outcome in RecommendationCacheOutcomes)
            _recommendationCacheLookups.WithLabels(outcome).IncTo(0);

        _jwtRefreshes = factory.CreateCounter(
            "app_jwt_refresh_total",
            "Refresh token rotation attempts.",
            new CounterConfiguration { LabelNames = new[] { "outcome" } });
        foreach (var outcome in JwtRefreshOutcomes)
            _jwtRefreshes.WithLabels(outcome).IncTo(0);
    }

    public void RecordRegistration(string outcome) => _registrations.WithLabels(outcome).Inc();
    public void RecordLogin(string outcome) => _logins.WithLabels(outcome).Inc();
    public void RecordReviewSubmitted() => _reviewsSubmitted.Inc();
    public void RecordPhotoUpload(string target) => _photoUploads.WithLabels(target).Inc();
    public void SetModerationQueueDepth(string kind, int depth) => _moderationQueueDepth.WithLabels(kind).Set(depth);
    public void RecordValidationFailure(string requestType) => _validationFailures.WithLabels(requestType).Inc();
    public void RecordModerationDecision(string kind, string verdict) => _moderationDecisions.WithLabels(kind, verdict).Inc();
    public void RecordRecommendationCacheLookup(string outcome) => _recommendationCacheLookups.WithLabels(outcome).Inc();
    public void RecordJwtRefresh(string outcome) => _jwtRefreshes.WithLabels(outcome).Inc();
}
