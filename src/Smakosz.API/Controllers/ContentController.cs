using Smakosz.Application.Features.Content.Commands.SendContactMessage;
using Smakosz.Application.Features.Content.Queries.GetContactInfo;
using Smakosz.Application.Features.Content.Queries.GetContentPage;

namespace Smakosz.API.Controllers;

[Route("api/content")]
public class ContentController : ApiController
{
    private readonly IMediator _mediator;

    public ContentController(IMediator mediator)
    {
        _mediator = mediator;
    }

    [HttpGet("contact-info")]
    public async Task<IActionResult> GetContactInfo()
    {
        var result = await _mediator.Send(new GetContactInfoQuery());
        return ToActionResult(result);
    }

    [HttpPost("contact")]
    public async Task<IActionResult> SendContactMessage([FromBody] SendContactMessageCommand command)
    {
        var result = await _mediator.Send(command);
        return ToNoContentResult(result);
    }

    [HttpGet("{slug}")]
    public async Task<IActionResult> GetContentPage(string slug)
    {
        var result = await _mediator.Send(new GetContentPageQuery(slug));
        return ToActionResult(result);
    }
}
