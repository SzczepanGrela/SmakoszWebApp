using Microsoft.AspNetCore.Authorization;
using Smakosz.Application.Features.Reviews.Commands.CreateReview;
using Smakosz.Application.Features.Reviews.Commands.DeleteReview;
using Smakosz.Application.Features.Reviews.Commands.UpdateReview;

namespace Smakosz.API.Controllers;

[Route("api/reviews")]
public class ReviewsController : ApiController
{
    private readonly IMediator _mediator;

    public ReviewsController(IMediator mediator)
    {
        _mediator = mediator;
    }

    [Authorize]
    [HttpPost]
    public async Task<IActionResult> CreateReview([FromBody] CreateReviewCommand command)
    {
        var result = await _mediator.Send(command);
        return ToCreatedResult(result);
    }

    [Authorize]
    [HttpPut("{id:guid}")]
    public async Task<IActionResult> UpdateReview(Guid id, [FromBody] UpdateReviewRequest request)
    {
        var command = new UpdateReviewCommand(
            id,
            request.DishRating,
            request.ServiceRating,
            request.CleanlinessRating,
            request.AmbianceRating,
            request.Content,
            request.VisitDate);

        var result = await _mediator.Send(command);
        return ToActionResult(result);
    }

    [Authorize]
    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> DeleteReview(Guid id)
    {
        var result = await _mediator.Send(new DeleteReviewCommand(id));
        return ToNoContentResult(result);
    }
}

public record UpdateReviewRequest(
    int DishRating,
    int ServiceRating,
    int CleanlinessRating,
    int AmbianceRating,
    string? Content,
    DateOnly VisitDate);
