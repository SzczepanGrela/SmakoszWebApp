using ErrorOr;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Smakosz.Application.Common.Interfaces;
using Smakosz.Application.Features.Worker.DTOs;

namespace Smakosz.Application.Features.Worker.Queries.GetWorkerConfig;

public class GetWorkerConfigHandler : IRequestHandler<GetWorkerConfigQuery, ErrorOr<WorkerConfigDto>>
{
    private readonly ISmakoszDbContext _db;

    public GetWorkerConfigHandler(ISmakoszDbContext db)
    {
        _db = db;
    }

    public async Task<ErrorOr<WorkerConfigDto>> Handle(GetWorkerConfigQuery request, CancellationToken cancellationToken)
    {
        var configKeys = new[]
        {
            "moderation_toxic_threshold_approve",
            "moderation_toxic_threshold_reject",
            "moderation_nsfw_threshold_approve",
            "moderation_nsfw_threshold_reject",
            "moderation_on_topic_threshold",
            "herbert_model_version",
            "nsfw_model_version",
            "clip_model_version"
        };

        var configs = await _db.SystemConfigs
            .Where(c => configKeys.Contains(c.Key))
            .ToDictionaryAsync(c => c.Key, c => c.Value, cancellationToken);

        return new WorkerConfigDto
        {
            ToxicThresholdApprove = GetDecimal(configs, "moderation_toxic_threshold_approve", 0.3m),
            ToxicThresholdReject = GetDecimal(configs, "moderation_toxic_threshold_reject", 0.8m),
            NsfwThresholdApprove = GetDecimal(configs, "moderation_nsfw_threshold_approve", 0.2m),
            NsfwThresholdReject = GetDecimal(configs, "moderation_nsfw_threshold_reject", 0.7m),
            OnTopicThreshold = GetDecimal(configs, "moderation_on_topic_threshold", 0.3m),
            HerbertModelVersion = GetString(configs, "herbert_model_version", "v1"),
            NsfwModelVersion = GetString(configs, "nsfw_model_version", "v1"),
            ClipModelVersion = GetString(configs, "clip_model_version", "v1")
        };
    }

    private static decimal GetDecimal(Dictionary<string, string> configs, string key, decimal defaultValue) =>
        configs.TryGetValue(key, out var value) && decimal.TryParse(value, out var result) ? result : defaultValue;

    private static string GetString(Dictionary<string, string> configs, string key, string defaultValue) =>
        configs.TryGetValue(key, out var value) ? value : defaultValue;
}
