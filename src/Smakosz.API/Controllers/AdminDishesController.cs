using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.RateLimiting;
using Smakosz.Application.Common.Models;
using Smakosz.Application.Features.Admin.Commands.AdminDeleteDish;
using Smakosz.Application.Features.Admin.Commands.ChangeDishModerationStatus;
using Smakosz.Application.Features.Admin.Commands.ToggleDishAvailability;
using Smakosz.Application.Features.Admin.Queries.GetAdminDishes;

namespace Smakosz.API.Controllers;

[Authorize(Roles = "Admin")]
[Route("api/admin/dishes")]
[DisableRateLimiting]
public class AdminDishesController : ApiController
{
    private readonly IMediator _mediator;

    public AdminDishesController(IMediator mediator)
    {
        _mediator = mediator;
    }

    [HttpGet]
    public async Task<IActionResult> GetDishes(
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20,
        [FromQuery] string? search = null,
        [FromQuery] int? restaurantId = null,
        [FromQuery] string? moderationStatus = null,
        [FromQuery] bool? isAvailable = null)
    {
        var result = await _mediator.Send(
            new GetAdminDishesQuery(new PaginationParams(page, pageSize), search, restaurantId, moderationStatus, isAvailable));
        return ToActionResult(result);
    }

    [HttpDelete("{publicId:guid}")]
    public async Task<IActionResult> DeleteDish(Guid publicId)
    {
        var result = await _mediator.Send(new AdminDeleteDishCommand(publicId));
        return ToNoContentResult(result);
    }

    [HttpPut("{publicId:guid}/moderation-status")]
    public async Task<IActionResult> ChangeModerationStatus(Guid publicId, [FromBody] ChangeDishModerationStatusRequest request)
    {
        var result = await _mediator.Send(new ChangeDishModerationStatusCommand(publicId, request.Status));
        return ToNoContentResult(result);
    }

    [HttpPut("{publicId:guid}/availability")]
    public async Task<IActionResult> ToggleAvailability(Guid publicId, [FromBody] ToggleDishAvailabilityRequest request)
    {
        var result = await _mediator.Send(new ToggleDishAvailabilityCommand(publicId, request.IsAvailable));
        return ToNoContentResult(result);
    }
}

public record ChangeDishModerationStatusRequest(string Status);
public record ToggleDishAvailabilityRequest(bool IsAvailable);
