using Microsoft.AspNetCore.Authorization;
using Smakosz.Application.Common.Models;
using Smakosz.Application.Features.Business.Commands.CreateDish;
using Smakosz.Application.Features.Business.Commands.CreateMenuSection;
using Smakosz.Application.Features.Business.Commands.DeleteDish;
using Smakosz.Application.Features.Business.Commands.DeleteMenuSection;
using Smakosz.Application.Features.Business.Commands.RegisterBusiness;
using Smakosz.Application.Features.Business.Commands.ReorderMenuSections;
using Smakosz.Application.Features.Business.Commands.UpdateDish;
using Smakosz.Application.Features.Business.Commands.UpdateDishAvailability;
using Smakosz.Application.Features.Business.Commands.UpdateMenuSection;
using Smakosz.Application.Features.Business.Commands.UpdateOpeningHours;
using Smakosz.Application.Features.Business.Commands.UpdateRestaurant;
using Smakosz.Application.Features.Business.Dtos;
using Smakosz.Application.Features.Business.Queries.GetBusinessDishDetail;
using Smakosz.Application.Features.Business.Queries.GetBusinessDishes;
using Smakosz.Application.Features.Business.Queries.GetBusinessEditRequests;
using Smakosz.Application.Features.Business.Queries.GetBusinessReviews;
using Smakosz.Application.Features.Business.Queries.GetBusinessStats;
using Smakosz.Application.Features.Business.Queries.GetDashboard;
using Smakosz.Application.Features.Business.Queries.GetMenuSections;
using Smakosz.Application.Features.Business.Queries.GetMyRestaurant;
using Smakosz.Application.Features.Business.Queries.GetOpeningHours;
using Smakosz.Application.Features.Business.Queries.GetRegistrationStatus;

namespace Smakosz.API.Controllers;

[Authorize(Roles = "Business,Admin")]
[Route("api/business")]
public class BusinessController : ApiController
{
    private readonly IMediator _mediator;

    public BusinessController(IMediator mediator)
    {
        _mediator = mediator;
    }

    [HttpGet("dashboard")]
    public async Task<IActionResult> GetDashboard()
    {
        var result = await _mediator.Send(new GetBusinessDashboardQuery());
        return ToActionResult(result);
    }

    [HttpGet("restaurant")]
    public async Task<IActionResult> GetMyRestaurant()
    {
        var result = await _mediator.Send(new GetMyRestaurantQuery());
        return ToActionResult(result);
    }

    [HttpPut("restaurant")]
    public async Task<IActionResult> UpdateRestaurant([FromBody] UpdateRestaurantCommand command)
    {
        var result = await _mediator.Send(command);
        return ToNoContentResult(result);
    }

    [Authorize]
    [HttpGet("registration-status")]
    public async Task<IActionResult> GetRegistrationStatus()
    {
        var result = await _mediator.Send(new GetRegistrationStatusQuery());
        return ToActionResult(result);
    }

    [Authorize]
    [HttpPost("register")]
    public async Task<IActionResult> RegisterBusiness([FromBody] RegisterBusinessCommand command)
    {
        var result = await _mediator.Send(command);
        return ToCreatedResult(result, $"/api/business/restaurant");
    }

    [HttpGet("opening-hours")]
    public async Task<IActionResult> GetOpeningHours()
    {
        var result = await _mediator.Send(new GetOpeningHoursQuery());
        return ToActionResult(result);
    }

    [HttpPut("opening-hours")]
    public async Task<IActionResult> UpdateOpeningHours([FromBody] UpdateOpeningHoursCommand command)
    {
        var result = await _mediator.Send(command);
        return ToNoContentResult(result);
    }

    [HttpGet("menu-sections")]
    public async Task<IActionResult> GetMenuSections()
    {
        var result = await _mediator.Send(new GetMenuSectionsQuery());
        return ToActionResult(result);
    }

    [HttpPost("menu-sections")]
    public async Task<IActionResult> CreateMenuSection([FromBody] CreateMenuSectionCommand command)
    {
        var result = await _mediator.Send(command);
        return ToCreatedResult(result, $"/api/business/menu-sections/{(result.IsError ? 0 : result.Value)}");
    }

    [HttpPut("menu-sections/reorder")]
    public async Task<IActionResult> ReorderMenuSections([FromBody] ReorderMenuSectionsCommand command)
    {
        var result = await _mediator.Send(command);
        return ToNoContentResult(result);
    }

    [HttpPut("menu-sections/{sectionId:int}")]
    public async Task<IActionResult> UpdateMenuSection(int sectionId, [FromBody] UpdateMenuSectionRequest request)
    {
        var result = await _mediator.Send(new UpdateMenuSectionCommand(sectionId, request.Name));
        return ToNoContentResult(result);
    }

    [HttpDelete("menu-sections/{sectionId:int}")]
    public async Task<IActionResult> DeleteMenuSection(int sectionId)
    {
        var result = await _mediator.Send(new DeleteMenuSectionCommand(sectionId));
        return ToNoContentResult(result);
    }

    [HttpGet("dishes")]
    public async Task<IActionResult> GetDishes([FromQuery] int? menuSectionId = null, [FromQuery] int page = 1, [FromQuery] int pageSize = 20)
    {
        var result = await _mediator.Send(new GetBusinessDishesQuery(menuSectionId, page, pageSize));
        return ToActionResult(result);
    }

    [HttpGet("dishes/{publicId:guid}")]
    public async Task<IActionResult> GetDishDetail(Guid publicId)
    {
        var result = await _mediator.Send(new GetBusinessDishDetailQuery(publicId));
        return ToActionResult(result);
    }

    [HttpPost("dishes")]
    public async Task<IActionResult> CreateDish([FromBody] CreateDishCommand command)
    {
        var result = await _mediator.Send(command);
        return ToCreatedResult(result, $"/api/business/dishes/{(result.IsError ? 0 : result.Value)}");
    }

    [HttpPut("dishes/{publicId:guid}")]
    public async Task<IActionResult> UpdateDish(Guid publicId, [FromBody] UpdateDishRequest request)
    {
        var result = await _mediator.Send(new UpdateDishCommand(
            publicId,
            request.Name,
            request.Price,
            request.Description,
            request.Calories,
            request.IsAvailable));
        return ToNoContentResult(result);
    }

    [HttpDelete("dishes/{publicId:guid}")]
    public async Task<IActionResult> DeleteDish(Guid publicId)
    {
        var result = await _mediator.Send(new DeleteDishCommand(publicId));
        return ToNoContentResult(result);
    }

    [HttpPut("dishes/{publicId:guid}/availability")]
    public async Task<IActionResult> UpdateDishAvailability(Guid publicId, [FromBody] UpdateDishAvailabilityRequest request)
    {
        var result = await _mediator.Send(new UpdateDishAvailabilityCommand(publicId, request.IsAvailable));
        return ToNoContentResult(result);
    }

    [HttpGet("reviews")]
    public async Task<IActionResult> GetReviews([FromQuery] int page = 1, [FromQuery] int pageSize = 20)
    {
        var result = await _mediator.Send(new GetBusinessReviewsQuery(new PaginationParams(page, pageSize)));
        return ToActionResult(result);
    }

    [HttpGet("stats")]
    public async Task<IActionResult> GetStats()
    {
        var result = await _mediator.Send(new GetBusinessStatsQuery());
        return ToActionResult(result);
    }

    [HttpGet("edit-requests")]
    public async Task<IActionResult> GetEditRequests()
    {
        var result = await _mediator.Send(new GetBusinessEditRequestsQuery());
        return ToActionResult(result);
    }
}

public record UpdateDishAvailabilityRequest(bool IsAvailable);
public record UpdateMenuSectionRequest(string Name);
public record UpdateDishRequest(string? Name, decimal? Price, string? Description, int? Calories, bool? IsAvailable);
