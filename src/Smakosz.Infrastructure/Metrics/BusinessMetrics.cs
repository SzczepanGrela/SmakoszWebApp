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
    }

    public void RecordRegistration(string outcome) => _registrations.WithLabels(outcome).Inc();
    public void RecordLogin(string outcome) => _logins.WithLabels(outcome).Inc();
    public void RecordReviewSubmitted() => _reviewsSubmitted.Inc();
    public void RecordPhotoUpload(string target) => _photoUploads.WithLabels(target).Inc();
    public void SetModerationQueueDepth(string kind, int depth) => _moderationQueueDepth.WithLabels(kind).Set(depth);
}
