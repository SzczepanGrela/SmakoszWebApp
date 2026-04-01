using Microsoft.AspNetCore.Authorization;
using Smakosz.Application.Common.Models;
using Smakosz.Application.Features.Admin.Commands.ModeratePhoto;
using Smakosz.Application.Features.Admin.Commands.ModerateReview;
using Smakosz.Application.Features.Admin.Commands.ProcessEditRequest;
using Smakosz.Application.Features.Admin.Commands.RespondToContact;
using Smakosz.Application.Features.Admin.Commands.UpdateTicketStatus;
using Smakosz.Application.Features.Admin.Commands.UpdateReportStatus;
using Smakosz.Application.Features.Admin.Queries.GetAdminDashboard;
using Smakosz.Application.Features.Admin.Queries.GetEditRequests;
using Smakosz.Application.Features.Admin.Queries.GetPendingPhotos;
using Smakosz.Application.Features.Admin.Queries.GetPendingReviews;
using Smakosz.Application.Features.Admin.Queries.GetReports;
using Smakosz.Application.Features.Admin.Queries.GetTicketDetail;
using Smakosz.Application.Features.Admin.Queries.GetTickets;

namespace Smakosz.API.Controllers;

[Authorize(Roles = "Admin,Moderator")]
[Route("api/admin")]
public class AdminModerationController : ApiController
{
    private readonly IMediator _mediator;

    public AdminModerationController(IMediator mediator)
    {
        _mediator = mediator;
    }

    [HttpGet("dashboard")]
    public async Task<IActionResult> GetDashboard()
    {
        var result = await _mediator.Send(new GetAdminDashboardQuery());
        return ToActionResult(result);
    }

    [HttpGet("tickets")]
    public async Task<IActionResult> GetTickets(
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20,
        [FromQuery] string? status = null,
        [FromQuery] string? ticketType = null)
    {
        var result = await _mediator.Send(new GetTicketsQuery(new PaginationParams(page, pageSize), status, ticketType));
        return ToActionResult(result);
    }

    [HttpGet("tickets/{ticketId:int}")]
    public async Task<IActionResult> GetTicketDetail(int ticketId)
    {
        var result = await _mediator.Send(new GetTicketDetailQuery(ticketId));
        return ToActionResult(result);
    }

    [HttpPut("tickets/{ticketId:int}/status")]
    public async Task<IActionResult> UpdateTicketStatus(int ticketId, [FromBody] UpdateTicketStatusRequest request)
    {
        var result = await _mediator.Send(new UpdateTicketStatusCommand(ticketId, request.Status));
        return ToNoContentResult(result);
    }

    [HttpPost("tickets/{ticketId:int}/respond")]
    public async Task<IActionResult> RespondToContact(int ticketId, [FromBody] RespondToContactRequest request)
    {
        var result = await _mediator.Send(new RespondToContactCommand(ticketId, request.Response));
        return ToNoContentResult(result);
    }

    [HttpGet("photos/pending")]
    public async Task<IActionResult> GetPendingPhotos(
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20)
    {
        var result = await _mediator.Send(new GetPendingPhotosQuery(new PaginationParams(page, pageSize)));
        return ToActionResult(result);
    }

    [HttpPost("photos/{publicId:guid}/moderate")]
    public async Task<IActionResult> ModeratePhoto(Guid publicId, [FromBody] ModeratePhotoRequest request)
    {
        var result = await _mediator.Send(new ModeratePhotoCommand(publicId, request.Approve, request.RejectionReason));
        return ToNoContentResult(result);
    }

    [HttpGet("reviews/pending")]
    public async Task<IActionResult> GetPendingReviews(
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20)
    {
        var result = await _mediator.Send(new GetPendingReviewsQuery(new PaginationParams(page, pageSize)));
        return ToActionResult(result);
    }

    [HttpPost("reviews/{publicId:guid}/moderate")]
    public async Task<IActionResult> ModerateReview(Guid publicId, [FromBody] ModerateReviewRequest request)
    {
        var result = await _mediator.Send(new ModerateReviewCommand(publicId, request.Approve, request.RejectionReason));
        return ToNoContentResult(result);
    }

    [HttpGet("reports")]
    public async Task<IActionResult> GetReports(
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20,
        [FromQuery] string? status = null)
    {
        var result = await _mediator.Send(new GetReportsQuery(new PaginationParams(page, pageSize), status));
        return ToActionResult(result);
    }

    [HttpPut("reports/{reportId:int}/status")]
    public async Task<IActionResult> UpdateReportStatus(int reportId, [FromBody] UpdateReportStatusRequest request)
    {
        var result = await _mediator.Send(new UpdateReportStatusCommand(reportId, request.Status));
        return ToNoContentResult(result);
    }

    [HttpGet("edit-requests")]
    public async Task<IActionResult> GetEditRequests(
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20)
    {
        var result = await _mediator.Send(new GetEditRequestsQuery(new PaginationParams(page, pageSize)));
        return ToActionResult(result);
    }

    [HttpPost("edit-requests/{requestId:int}/process")]
    public async Task<IActionResult> ProcessEditRequest(int requestId, [FromBody] ProcessEditRequestRequest request)
    {
        var result = await _mediator.Send(new ProcessEditRequestCommand(requestId, request.Approve, request.RejectionReason));
        return ToNoContentResult(result);
    }
}

public record UpdateReportStatusRequest(string Status);
public record UpdateTicketStatusRequest(string Status);
public record RespondToContactRequest(string Response);
public record ModeratePhotoRequest(bool Approve, string? RejectionReason);
public record ModerateReviewRequest(bool Approve, string? RejectionReason);
public record ProcessEditRequestRequest(bool Approve, string? RejectionReason);
