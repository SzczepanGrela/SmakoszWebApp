using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.RateLimiting;
using Smakosz.Application.Common.Models;
using Smakosz.Application.Features.Admin.Commands.CancelJob;
using Smakosz.Application.Features.Admin.Commands.CreateJob;
using Smakosz.Application.Features.Admin.Commands.ScheduleModeration;
using Smakosz.Application.Features.Admin.Commands.ScheduleNcfTraining;
using Smakosz.Application.Features.Admin.Commands.TriggerJob;
using Smakosz.Application.Features.Admin.Commands.UpdateSystemConfig;
using Smakosz.Application.Features.Admin.Queries.GetAiLogs;
using Smakosz.Application.Features.Admin.Queries.GetEmailLogs;
using Smakosz.Application.Features.Admin.Queries.GetJobs;
using Smakosz.Application.Features.Admin.Queries.GetModerationLogs;
using Smakosz.Application.Features.Admin.Queries.GetNcfStatus;
using Smakosz.Application.Features.Admin.Queries.GetSystemConfig;
using Smakosz.Application.Features.Admin.Queries.GetAuditLogs;
using Smakosz.Application.Features.Admin.Queries.GetSecurityLogs;
using Smakosz.Application.Features.Admin.Queries.GetSystemLogs;
using Smakosz.Application.Features.Admin.Queries.GetSystemNodes;
using Smakosz.Application.Features.Admin.Commands.WakeGpu;

namespace Smakosz.API.Controllers;

[Authorize(Roles = "Admin")]
[Route("api/admin")]
[DisableRateLimiting]
public class AdminSystemController : ApiController
{
    private readonly IMediator _mediator;

    public AdminSystemController(IMediator mediator)
    {
        _mediator = mediator;
    }

    [HttpGet("system-config")]
    public async Task<IActionResult> GetSystemConfig()
    {
        var result = await _mediator.Send(new GetSystemConfigQuery());
        return ToActionResult(result);
    }

    [HttpPut("system-config")]
    public async Task<IActionResult> UpdateSystemConfig([FromBody] UpdateSystemConfigRequest request)
    {
        var result = await _mediator.Send(new UpdateSystemConfigCommand(request.Key, request.Value));
        return ToNoContentResult(result);
    }

    [HttpGet("logs")]
    public async Task<IActionResult> GetSystemLogs(
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 50,
        [FromQuery] string? level = null)
    {
        var result = await _mediator.Send(new GetSystemLogsQuery(new PaginationParams(page, pageSize), level));
        return ToActionResult(result);
    }

    [HttpGet("jobs")]
    public async Task<IActionResult> GetJobs(
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20)
    {
        var result = await _mediator.Send(new GetJobsQuery(new PaginationParams(page, pageSize)));
        return ToActionResult(result);
    }

    [HttpPost("jobs")]
    public async Task<IActionResult> CreateJob([FromBody] CreateJobRequest request)
    {
        var result = await _mediator.Send(new CreateJobCommand(
            request.Type, request.Priority, request.Payload, request.EntityId, request.EntityType));
        return ToCreatedResult(result);
    }

    [HttpPost("jobs/{jobId:int}/trigger")]
    public async Task<IActionResult> TriggerJob(int jobId)
    {
        var result = await _mediator.Send(new TriggerJobCommand(jobId));
        return ToNoContentResult(result);
    }

    [HttpPut("jobs/{jobId:int}/cancel")]
    public async Task<IActionResult> CancelJob(int jobId)
    {
        var result = await _mediator.Send(new CancelJobCommand(jobId));
        return ToNoContentResult(result);
    }

    [HttpPost("ncf-training/schedule")]
    public async Task<IActionResult> ScheduleNcfTraining([FromBody] ScheduleNcfTrainingRequest? request = null)
    {
        var command = request is null
            ? new ScheduleNcfTrainingCommand()
            : new ScheduleNcfTrainingCommand(request.Priority);
        var result = await _mediator.Send(command);
        return ToNoContentResult(result);
    }

    [HttpPost("moderation/aggregate")]
    public async Task<IActionResult> ScheduleModeration()
    {
        var result = await _mediator.Send(new ScheduleModerationCommand());
        return ToNoContentResult(result);
    }

    [HttpGet("audit-logs")]
    public async Task<IActionResult> GetAuditLogs(
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 50,
        [FromQuery] string? tableName = null,
        [FromQuery] int? recordId = null)
    {
        var result = await _mediator.Send(new GetAuditLogsQuery(new PaginationParams(page, pageSize), tableName, recordId));
        return ToActionResult(result);
    }

    [HttpGet("security-logs")]
    public async Task<IActionResult> GetSecurityLogs(
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 50,
        [FromQuery] string? eventType = null)
    {
        var result = await _mediator.Send(new GetSecurityLogsQuery(new PaginationParams(page, pageSize), eventType));
        return ToActionResult(result);
    }

    [HttpGet("email-logs")]
    public async Task<IActionResult> GetEmailLogs(
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 50,
        [FromQuery] string? status = null,
        [FromQuery] string? type = null)
    {
        var result = await _mediator.Send(new GetEmailLogsQuery(new PaginationParams(page, pageSize), status, type));
        return ToActionResult(result);
    }

    [HttpGet("moderation-logs")]
    public async Task<IActionResult> GetModerationLogs(
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 50,
        [FromQuery] string? actor = null,
        [FromQuery] string? entityType = null)
    {
        var result = await _mediator.Send(new GetModerationLogsQuery(new PaginationParams(page, pageSize), actor, entityType));
        return ToActionResult(result);
    }

    [HttpGet("ai-logs")]
    public async Task<IActionResult> GetAiLogs(
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 50,
        [FromQuery] string? modelType = null,
        [FromQuery] bool? fallback = null)
    {
        var result = await _mediator.Send(new GetAiLogsQuery(new PaginationParams(page, pageSize), modelType, fallback));
        return ToActionResult(result);
    }

    [HttpGet("nodes")]
    public async Task<IActionResult> GetSystemNodes()
    {
        var result = await _mediator.Send(new GetSystemNodesQuery());
        return ToActionResult(result);
    }

    [HttpGet("ncf")]
    public async Task<IActionResult> GetNcfStatus(CancellationToken ct)
    {
        var result = await _mediator.Send(new GetNcfStatusQuery(), ct);
        return ToActionResult(result);
    }

    [HttpPost("nodes/gpu/wake")]
    public async Task<IActionResult> WakeGpu(CancellationToken ct)
    {
        var result = await _mediator.Send(new WakeGpuCommand(), ct);
        return ToActionResult(result);
    }
}

public record UpdateSystemConfigRequest(string Key, string Value);
public record CreateJobRequest(string Type, int Priority, string? Payload, string? EntityId, string? EntityType);
public record ScheduleNcfTrainingRequest(int Priority);
