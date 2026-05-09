using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.RateLimiting;
using Smakosz.Application.Features.Admin.Commands.UploadIngredientIcon;

namespace Smakosz.API.Controllers;

[Authorize(Roles = "Admin")]
[Route("api/admin/ingredients")]
[DisableRateLimiting]
public class AdminIngredientsController : ApiController
{
    private readonly IMediator _mediator;

    public AdminIngredientsController(IMediator mediator)
    {
        _mediator = mediator;
    }

    [HttpPost("icon")]
    [Consumes("multipart/form-data")]
    [EnableRateLimiting("upload")]
    public async Task<IActionResult> UploadIcon(IFormFile file)
    {
        using var stream = file.OpenReadStream();
        var result = await _mediator.Send(new UploadIngredientIconCommand(stream, file.FileName));
        return ToActionResult(result);
    }
}
