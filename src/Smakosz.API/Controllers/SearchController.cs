using Smakosz.Application.Common.Models;
using Smakosz.Application.Features.Search.Queries.GetSearchFilters;
using SearchQueryRequest = Smakosz.Application.Features.Search.Queries.SearchQuery.SearchQuery;

namespace Smakosz.API.Controllers;

[Route("api/search")]
public class SearchController : ApiController
{
    private readonly IMediator _mediator;

    public SearchController(IMediator mediator)
    {
        _mediator = mediator;
    }

    [HttpGet]
    public async Task<IActionResult> Search(
        [FromQuery] string type = "restaurants",
        [FromQuery(Name = "q")] string? query = null,
        [FromQuery] string? location = null,
        [FromQuery] string? cuisines = null,
        [FromQuery] int? minPrice = null,
        [FromQuery] int? maxPrice = null,
        [FromQuery] string? dietary = null,
        [FromQuery] decimal? lat = null,
        [FromQuery] decimal? lng = null,
        [FromQuery] int radius = 5,
        [FromQuery] string sortBy = "rating",
        [FromQuery] string sortDir = "desc",
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20)
    {
        var searchQuery = new SearchQueryRequest(
            new PaginationParams(page, pageSize),
            type,
            query,
            location,
            cuisines,
            minPrice,
            maxPrice,
            dietary,
            lat,
            lng,
            radius,
            sortBy,
            sortDir);

        var result = await _mediator.Send(searchQuery);
        return ToActionResult(result);
    }

    [HttpGet("filters")]
    public async Task<IActionResult> GetFilters()
    {
        var result = await _mediator.Send(new GetSearchFiltersQuery());
        return ToActionResult(result);
    }
}

