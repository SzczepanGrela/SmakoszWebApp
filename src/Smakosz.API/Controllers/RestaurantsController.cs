using Microsoft.AspNetCore.Authorization;
using Smakosz.Application.Common.Models;
using Smakosz.Application.Features.DataCorrections.Commands.CreateDataCorrection;
using Smakosz.Application.Features.Dishes.Queries.GetDishesByRestaurant;
using Smakosz.Application.Features.IngredientSuggestions.Commands.CreateIngredientSuggestion;
using Smakosz.Application.Features.Restaurants.Commands.RequestNewRestaurant;
using Smakosz.Application.Features.Restaurants.Commands.RequestRestaurantClaim;
using Smakosz.Application.Features.Restaurants.Queries.GetRestaurantBySlug;
using Smakosz.Application.Features.Restaurants.Queries.GetRestaurants;

namespace Smakosz.API.Controllers;

[Route("api/restaurants")]
public class RestaurantsController : ApiController
{
    private readonly IMediator _mediator;

    public RestaurantsController(IMediator mediator)
    {
        _mediator = mediator;
    }

    [HttpGet]
    public async Task<IActionResult> GetRestaurants(
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20,
        [FromQuery] int? cityId = null,
        [FromQuery] int? cuisineTypeId = null,
        [FromQuery] int? minPrice = null,
        [FromQuery] int? maxPrice = null,
        [FromQuery] string sortBy = "trending")
    {
        var query = new GetRestaurantsQuery(
            new PaginationParams(page, pageSize),
            cityId,
            cuisineTypeId,
            minPrice,
            maxPrice,
            sortBy);

        var result = await _mediator.Send(query);
        return ToActionResult(result);
    }

    [HttpGet("{slug}")]
    public async Task<IActionResult> GetRestaurantBySlug(string slug)
    {
        var result = await _mediator.Send(new GetRestaurantBySlugQuery(slug));
        return ToActionResult(result);
    }

    [HttpGet("{slug}/dishes")]
    public async Task<IActionResult> GetDishesByRestaurant(
        string slug,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20)
    {
        var query = new GetDishesByRestaurantQuery(slug, new PaginationParams(page, pageSize));
        var result = await _mediator.Send(query);
        return ToActionResult(result);
    }

    [Authorize]
    [HttpPost("{slug}/corrections")]
    public async Task<IActionResult> CreateCorrection(string slug, [FromBody] CreateCorrectionRequest request)
    {
        var result = await _mediator.Send(new CreateDataCorrectionCommand(slug, request.IssueType, request.Description, request.ProposedValue));
        return ToNoContentResult(result);
    }

    [Authorize]
    [HttpPost("{slug}/ingredient-suggestions")]
    public async Task<IActionResult> CreateIngredientSuggestion(string slug, [FromBody] CreateIngredientSuggestionRequest request)
    {
        var result = await _mediator.Send(new CreateIngredientSuggestionCommand(
            slug, request.SuggestedName));
        return ToNoContentResult(result);
    }

    [Authorize]
    [HttpPost("{publicId:guid}/claim")]
    public async Task<IActionResult> ClaimRestaurant(Guid publicId, [FromBody] ClaimRestaurantRequest request)
    {
        var result = await _mediator.Send(new RequestRestaurantClaimCommand(publicId, request.Justification));
        return ToCreatedResult(result, $"/api/me/tickets/{(result.IsError ? 0 : result.Value)}");
    }

    [Authorize]
    [HttpPost("request")]
    public async Task<IActionResult> RequestNewRestaurant([FromBody] RequestNewRestaurantRequest request)
    {
        var result = await _mediator.Send(new RequestNewRestaurantCommand(
            request.Name,
            request.Address,
            request.Phone,
            request.Email,
            request.Description,
            request.CityId,
            request.CuisineTypeId));
        return ToCreatedResult(result, $"/api/me/tickets/{(result.IsError ? 0 : result.Value)}");
    }
}

public record CreateCorrectionRequest(string IssueType, string? Description, string? ProposedValue);
public record CreateIngredientSuggestionRequest(string SuggestedName);
public record ClaimRestaurantRequest(string Justification);
public record RequestNewRestaurantRequest(
    string Name,
    string Address,
    string? Phone,
    string? Email,
    string? Description,
    int? CityId,
    int? CuisineTypeId);
