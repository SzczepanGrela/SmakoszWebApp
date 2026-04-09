using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.RateLimiting;
using Smakosz.Application.Common.Models;
using Smakosz.Application.Features.Admin.Commands.CancelJob;
using Smakosz.Application.Features.Admin.Commands.CreateJob;
using Smakosz.Application.Features.Admin.Commands.ScheduleModeration;
using Smakosz.Application.Features.Admin.Commands.ScheduleNcfTraining;
using Smakosz.Application.Features.Admin.Commands.TriggerJob;
using Smakosz.Application.Features.Admin.Commands.UpdateSystemConfig;
using Smakosz.Application.Features.Admin.Queries.GetAiModels;
using Smakosz.Application.Features.Admin.Queries.GetHeroImages;
using Smakosz.Application.Features.Admin.Queries.GetJobs;
using Smakosz.Application.Features.Admin.Queries.GetSystemConfig;
using Smakosz.Application.Features.Admin.Queries.GetAuditLogs;
using Smakosz.Application.Features.Admin.Queries.GetSecurityLogs;
using Smakosz.Application.Features.Admin.Queries.GetSystemLogs;
using Smakosz.Application.Features.Admin.Queries.GetSystemNodes;

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
    public async Task<IActionResult> ScheduleNcfTraining()
    {
        var result = await _mediator.Send(new ScheduleNcfTrainingCommand());
        return ToNoContentResult(result);
    }

    [HttpPost("moderation/aggregate")]
    public async Task<IActionResult> ScheduleModeration()
    {
        var result = await _mediator.Send(new ScheduleModerationCommand());
        return ToNoContentResult(result);
    }

    [HttpGet("hero-images")]
    public async Task<IActionResult> GetHeroImages()
    {
        var result = await _mediator.Send(new GetHeroImagesQuery());
        return ToActionResult(result);
    }

    [HttpGet("ai-models")]
    public async Task<IActionResult> GetAiModels()
    {
        var result = await _mediator.Send(new GetAiModelsQuery());
        return ToActionResult(result);
    }

    [HttpGet("audit-logs")]
    public async Task<IActionResult> GetAuditLogs(
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 50,
        [FromQuery] string? tableName = null)
    {
        var result = await _mediator.Send(new GetAuditLogsQuery(new PaginationParams(page, pageSize), tableName));
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

    [HttpGet("nodes")]
    public async Task<IActionResult> GetSystemNodes()
    {
        var result = await _mediator.Send(new GetSystemNodesQuery());
        return ToActionResult(result);
    }
}

public record UpdateSystemConfigRequest(string Key, string Value);
public record CreateJobRequest(string Type, int Priority, string? Payload, string? EntityId, string? EntityType);
