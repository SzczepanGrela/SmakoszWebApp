using Smakosz.Application.Features.Recommendations.Queries.GetRecommendations;

namespace Smakosz.API.Controllers;

[Route("api/recommendations")]
public class RecommendationsController : ApiController
{
    private readonly IMediator _mediator;

    public RecommendationsController(IMediator mediator)
    {
        _mediator = mediator;
    }

    [HttpGet]
    public async Task<IActionResult> GetRecommendations()
    {
        var result = await _mediator.Send(new GetRecommendationsQuery());
        return ToActionResult(result);
    }
}
