using Microsoft.AspNetCore.RateLimiting;
using Smakosz.Application.Common.Models;
using Smakosz.Application.Features.Search.Queries.GetSearchFilters;
using Smakosz.Application.Features.Search.Queries.SearchSuggest;
using SearchQueryRequest = Smakosz.Application.Features.Search.Queries.SearchQuery.SearchQuery;

namespace Smakosz.API.Controllers;

[Route("api/search")]
[EnableRateLimiting("search")]
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
        [FromQuery] string sortBy = "rating",
        [FromQuery] string sortDir = "desc",
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20,
        [FromQuery] string? tags = null,
        [FromQuery] string? dishCategories = null,
        [FromQuery] string? features = null,
        [FromQuery] string? moods = null,
        [FromQuery] string? occasions = null,
        [FromQuery] string? spiceLevels = null)
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
            sortBy,
            sortDir,
            tags,
            dishCategories,
            features,
            moods,
            occasions,
            spiceLevels);

        var result = await _mediator.Send(searchQuery);
        return ToActionResult(result);
    }

    [HttpGet("filters")]
    public async Task<IActionResult> GetFilters()
    {
        var result = await _mediator.Send(new GetSearchFiltersQuery());
        return ToActionResult(result);
    }

    [HttpGet("suggest")]
    public async Task<IActionResult> Suggest([FromQuery(Name = "q")] string? query, [FromQuery] int limit = 7)
    {
        var result = await _mediator.Send(new SearchSuggestQuery(query ?? "", limit));
        return ToActionResult(result);
    }
}

