using Smakosz.Application.Features.Ingredients.Queries.GetIngredients;

namespace Smakosz.API.Controllers;

[Route("api/ingredients")]
public class IngredientsController : ApiController
{
    private readonly IMediator _mediator;

    public IngredientsController(IMediator mediator)
    {
        _mediator = mediator;
    }

    [HttpGet]
    public async Task<IActionResult> GetIngredients()
    {
        var result = await _mediator.Send(new GetIngredientsQuery());
        return ToActionResult(result);
    }
}
