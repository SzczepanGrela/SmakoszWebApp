using Prometheus;
using Smakosz.Application.Common.Interfaces;

namespace Smakosz.Infrastructure.Metrics;

public class BusinessMetrics : IBusinessMetrics
{
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

        _logins = factory.CreateCounter(
            "app_logins_total",
            "Total login attempts.",
            new CounterConfiguration { LabelNames = new[] { "outcome" } });

        _reviewsSubmitted = factory.CreateCounter(
            "app_reviews_submitted_total",
            "Total reviews successfully submitted.");

        _photoUploads = factory.CreateCounter(
            "app_photo_uploads_total",
            "Total photos uploaded.",
            new CounterConfiguration { LabelNames = new[] { "target" } });

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

        _recommendationCacheLookups = factory.CreateCounter(
            "app_recommendation_cache_lookups_total",
            "Outcomes of recommendation cache lookups served from the home and recommendations endpoints.",
            new CounterConfiguration { LabelNames = new[] { "outcome" } });

        _jwtRefreshes = factory.CreateCounter(
            "app_jwt_refresh_total",
            "Refresh token rotation attempts.",
            new CounterConfiguration { LabelNames = new[] { "outcome" } });
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
