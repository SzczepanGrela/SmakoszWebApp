using Smakosz.Application.Common.Models;
using Smakosz.Application.Features.Dishes.Queries.GetDishesByRestaurant;
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
        [FromQuery] string? cuisineType = null,
        [FromQuery] int? minPrice = null,
        [FromQuery] int? maxPrice = null,
        [FromQuery] string sortBy = "trending")
    {
        var query = new GetRestaurantsQuery(
            new PaginationParams(page, pageSize),
            cityId,
            cuisineType,
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
}
