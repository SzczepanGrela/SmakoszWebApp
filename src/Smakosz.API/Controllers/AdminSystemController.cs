using Microsoft.AspNetCore.Authorization;
using Smakosz.Application.Common.Models;
using Smakosz.Application.Features.Admin.Commands.TriggerJob;
using Smakosz.Application.Features.Admin.Commands.UpdateSystemConfig;
using Smakosz.Application.Features.Admin.Queries.GetAiModels;
using Smakosz.Application.Features.Admin.Queries.GetJobs;
using Smakosz.Application.Features.Admin.Queries.GetSystemConfig;
using Smakosz.Application.Features.Admin.Queries.GetSystemLogs;

namespace Smakosz.API.Controllers;

[Authorize(Roles = "Admin")]
[Route("api/admin")]
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

    [HttpPost("jobs/{jobId:int}/trigger")]
    public async Task<IActionResult> TriggerJob(int jobId)
    {
        var result = await _mediator.Send(new TriggerJobCommand(jobId));
        return ToNoContentResult(result);
    }

    [HttpGet("ai-models")]
    public async Task<IActionResult> GetAiModels()
    {
        var result = await _mediator.Send(new GetAiModelsQuery());
        return ToActionResult(result);
    }
}

public record UpdateSystemConfigRequest(string Key, string Value);
