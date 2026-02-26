using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Smakosz.API.Common;
using Smakosz.Application.Features.Worker.Commands.ClaimJob;
using Smakosz.Application.Features.Worker.Commands.CompleteJob;
using Smakosz.Application.Features.Worker.Commands.FailJob;
using Smakosz.Application.Features.Worker.Commands.ReportProgress;
using Smakosz.Application.Features.Worker.Commands.SendHeartbeat;
using Smakosz.Application.Features.Worker.DTOs;
using Smakosz.Application.Features.Worker.Queries.GetNextJob;
using Smakosz.Application.Features.Worker.Queries.GetWorkerConfig;

namespace Smakosz.API.Controllers;

[Authorize(AuthenticationSchemes = "WorkerApiKey", Policy = "Worker")]
[Route("api/worker")]
public class WorkerController : ApiController
{
    private readonly IMediator _mediator;

    public WorkerController(IMediator mediator) => _mediator = mediator;

    private string GetWorkerId() =>
        User.FindFirst("worker_id")?.Value ?? "unknown";

    [HttpPost("heartbeat")]
    public async Task<IActionResult> Heartbeat([FromBody] HeartbeatRequest request)
    {
        var result = await _mediator.Send(new SendHeartbeatCommand(
            request.NodeId,
            request.IpAddress,
            request.GpuName,
            request.GpuMemoryTotal,
            request.GpuMemoryUsed,
            request.CurrentJobId,
            request.Metadata));

        return ToNoContentResult(result);
    }

    [HttpGet("jobs/next")]
    public async Task<IActionResult> GetNextJob([FromQuery] string? type)
    {
        var result = await _mediator.Send(new GetNextJobQuery(type, GetWorkerId()));

        if (result.IsError)
            return ToActionResult(result);

        if (result.Value is null)
            return NoContent();

        return ToActionResult(result!);
    }

    [HttpPut("jobs/{id:int}/claim")]
    public async Task<IActionResult> ClaimJob(int id)
    {
        var result = await _mediator.Send(new ClaimJobCommand(id, GetWorkerId()));
        return ToNoContentResult(result);
    }

    [HttpPut("jobs/{id:int}/complete")]
    public async Task<IActionResult> CompleteJob(int id, [FromBody] CompleteJobRequest request)
    {
        var result = await _mediator.Send(new CompleteJobCommand(id, request.Result, request.ProcessingTimeMs));
        return ToNoContentResult(result);
    }

    [HttpPut("jobs/{id:int}/fail")]
    public async Task<IActionResult> FailJob(int id, [FromBody] FailJobRequest request)
    {
        var result = await _mediator.Send(new FailJobCommand(
            id,
            request.ErrorMessage,
            request.ErrorLog,
            request.Retryable));

        return ToNoContentResult(result);
    }

    [HttpPost("jobs/{id:int}/progress")]
    public async Task<IActionResult> ReportProgress(int id, [FromBody] ProgressRequest request)
    {
        var result = await _mediator.Send(new ReportProgressCommand(
            id,
            request.Epoch,
            request.Loss,
            request.Accuracy,
            request.LearningRate,
            request.CurrentStep,
            request.TotalSteps,
            request.Message));

        return ToNoContentResult(result);
    }

    [HttpGet("config")]
    public async Task<IActionResult> GetConfig()
    {
        var result = await _mediator.Send(new GetWorkerConfigQuery());
        return ToActionResult(result);
    }
}
