using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.RateLimiting;
using Smakosz.Application.Features.Media.Commands.DeleteMedia;
using Smakosz.Application.Features.Media.Commands.UploadMedia;

namespace Smakosz.API.Controllers;

[Authorize]
[Route("api/media")]
public class MediaController : ApiController
{
    private readonly IMediator _mediator;

    public MediaController(IMediator mediator)
    {
        _mediator = mediator;
    }

    [HttpPost("upload")]
    [Consumes("multipart/form-data")]
    [EnableRateLimiting("upload")]
    public async Task<IActionResult> Upload(
        IFormFile file,
        [FromForm] string entityType,
        [FromForm] int? entityId)
    {
        using var stream = file.OpenReadStream();
        var result = await _mediator.Send(new UploadMediaCommand(
            stream, file.FileName, entityType, entityId));
        return ToCreatedResult(result);
    }

    [HttpDelete("{publicId:guid}")]
    public async Task<IActionResult> Delete(Guid publicId)
    {
        var result = await _mediator.Send(new DeleteMediaCommand(publicId));
        return ToNoContentResult(result);
    }
}
