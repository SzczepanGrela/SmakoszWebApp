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
            "moderation.toxic_threshold_approve",
            "moderation.toxic_threshold_reject",
            "moderation.nsfw_threshold_approve",
            "moderation.nsfw_threshold_reject",
            "moderation.on_topic_threshold",
            "moderation.herbert_version",
            "moderation.nsfw_version",
            "moderation.clip_version"
        };

        var configs = await _db.SystemConfigs
            .Where(c => configKeys.Contains(c.Key))
            .ToDictionaryAsync(c => c.Key, c => c.Value, cancellationToken);

        return new WorkerConfigDto
        {
            ToxicThresholdApprove = GetDecimal(configs, "moderation.toxic_threshold_approve", 0.3m),
            ToxicThresholdReject = GetDecimal(configs, "moderation.toxic_threshold_reject", 0.8m),
            NsfwThresholdApprove = GetDecimal(configs, "moderation.nsfw_threshold_approve", 0.2m),
            NsfwThresholdReject = GetDecimal(configs, "moderation.nsfw_threshold_reject", 0.7m),
            OnTopicThreshold = GetDecimal(configs, "moderation.on_topic_threshold", 0.3m),
            HerbertModelVersion = GetString(configs, "moderation.herbert_version", "v1"),
            NsfwModelVersion = GetString(configs, "moderation.nsfw_version", "v1"),
            ClipModelVersion = GetString(configs, "moderation.clip_version", "v1")
        };
    }

    private static decimal GetDecimal(Dictionary<string, string> configs, string key, decimal defaultValue) =>
        configs.TryGetValue(key, out var value) && decimal.TryParse(value, out var result) ? result : defaultValue;

    private static string GetString(Dictionary<string, string> configs, string key, string defaultValue) =>
        configs.TryGetValue(key, out var value) ? value : defaultValue;
}
