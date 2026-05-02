using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.RateLimiting;
using Smakosz.Application.Features.Admin.Commands.DeleteHeroImage;
using Smakosz.Application.Features.Admin.Commands.UploadHeroImage;
using Smakosz.Application.Features.Admin.Queries.GetHeroImages;

namespace Smakosz.API.Controllers;

[Authorize(Roles = "Admin,Moderator")]
[Route("api/admin/hero-images")]
[DisableRateLimiting]
public class AdminHeroImagesController : ApiController
{
    private readonly IMediator _mediator;

    public AdminHeroImagesController(IMediator mediator)
    {
        _mediator = mediator;
    }

    [HttpGet]
    public async Task<IActionResult> GetAll()
    {
        var result = await _mediator.Send(new GetHeroImagesQuery());
        return ToActionResult(result);
    }

    [HttpPost]
    [Consumes("multipart/form-data")]
    [EnableRateLimiting("upload")]
    public async Task<IActionResult> Upload(IFormFile file, [FromForm] string? creditText)
    {
        using var stream = file.OpenReadStream();
        var result = await _mediator.Send(new UploadHeroImageCommand(stream, file.FileName, creditText));
        return ToCreatedResult(result);
    }

    [HttpDelete("{publicId:guid}")]
    public async Task<IActionResult> Delete(Guid publicId)
    {
        var result = await _mediator.Send(new DeleteHeroImageCommand(publicId));
        return ToNoContentResult(result);
    }
}
