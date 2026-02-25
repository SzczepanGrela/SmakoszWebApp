using Smakosz.Application.Features.Home.Queries.GetHomeData;

namespace Smakosz.API.Controllers;

[Route("api/home")]
public class HomeController : ApiController
{
    private readonly IMediator _mediator;

    public HomeController(IMediator mediator)
    {
        _mediator = mediator;
    }

    [HttpGet]
    public async Task<IActionResult> GetHomeData()
    {
        var result = await _mediator.Send(new GetHomeDataQuery());
        return ToActionResult(result);
    }
}
