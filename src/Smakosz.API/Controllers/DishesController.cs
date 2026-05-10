using Smakosz.Application.Common.Models;
using Smakosz.Application.Features.Dishes.Queries.GetDishBySlug;
using Smakosz.Application.Features.Dishes.Queries.GetRandomDish;
using Smakosz.Application.Features.Reviews.Queries.GetReviewsByDish;

namespace Smakosz.API.Controllers;

[Route("api/dishes")]
public class DishesController : ApiController
{
    private readonly IMediator _mediator;

    public DishesController(IMediator mediator)
    {
        _mediator = mediator;
    }

    [HttpGet("random")]
    public async Task<IActionResult> GetRandomDish()
    {
        var result = await _mediator.Send(new GetRandomDishQuery());
        return ToActionResult(result);
    }

    [HttpGet("{slug}")]
    public async Task<IActionResult> GetDishBySlug(string slug)
    {
        var result = await _mediator.Send(new GetDishBySlugQuery(slug));
        return ToActionResult(result);
    }

    [HttpGet("{slug}/reviews")]
    public async Task<IActionResult> GetReviewsByDish(
        string slug,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20,
        [FromQuery] string sortBy = "helpful")
    {
        var query = new GetReviewsByDishQuery(slug, new PaginationParams(page, pageSize), sortBy);
        var result = await _mediator.Send(query);
        return ToActionResult(result);
    }
}
