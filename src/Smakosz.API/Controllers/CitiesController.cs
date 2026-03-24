using Smakosz.Application.Features.Cities.Queries.GetPublicCities;

namespace Smakosz.API.Controllers;

[Route("api/cities")]
public class CitiesController : ApiController
{
    private readonly IMediator _mediator;

    public CitiesController(IMediator mediator)
    {
        _mediator = mediator;
    }

    [HttpGet]
    public async Task<IActionResult> GetCities()
    {
        var result = await _mediator.Send(new GetPublicCitiesQuery());
        return ToActionResult(result);
    }
}
