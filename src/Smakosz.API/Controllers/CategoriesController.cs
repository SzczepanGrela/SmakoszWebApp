using Smakosz.Application.Features.Categories.Queries.GetCategories;

namespace Smakosz.API.Controllers;

[Route("api/categories")]
public class CategoriesController : ApiController
{
    private readonly IMediator _mediator;

    public CategoriesController(IMediator mediator)
    {
        _mediator = mediator;
    }

    [HttpGet]
    public async Task<IActionResult> GetCategories()
    {
        var result = await _mediator.Send(new GetCategoriesQuery());
        return ToActionResult(result);
    }
}
