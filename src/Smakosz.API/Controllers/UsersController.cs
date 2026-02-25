using Smakosz.Application.Common.Models;
using Smakosz.Application.Features.Reviews.Queries.GetReviewsByUser;
using Smakosz.Application.Features.Users.Queries.GetUserFollowers;
using Smakosz.Application.Features.Users.Queries.GetUserFollowing;
using Smakosz.Application.Features.Users.Queries.GetUserProfile;

namespace Smakosz.API.Controllers;

[Route("api/users")]
public class UsersController : ApiController
{
    private readonly IMediator _mediator;

    public UsersController(IMediator mediator)
    {
        _mediator = mediator;
    }

    [HttpGet("{slug}")]
    public async Task<IActionResult> GetUserProfile(string slug)
    {
        var result = await _mediator.Send(new GetUserProfileQuery(slug));
        return ToActionResult(result);
    }

    [HttpGet("{slug}/reviews")]
    public async Task<IActionResult> GetReviewsByUser(
        string slug,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20)
    {
        var query = new GetReviewsByUserQuery(slug, new PaginationParams(page, pageSize));
        var result = await _mediator.Send(query);
        return ToActionResult(result);
    }

    [HttpGet("{slug}/followers")]
    public async Task<IActionResult> GetFollowers(
        string slug,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20)
    {
        var result = await _mediator.Send(new GetUserFollowersQuery(slug, new PaginationParams(page, pageSize)));
        return ToActionResult(result);
    }

    [HttpGet("{slug}/following")]
    public async Task<IActionResult> GetFollowing(
        string slug,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20)
    {
        var result = await _mediator.Send(new GetUserFollowingQuery(slug, new PaginationParams(page, pageSize)));
        return ToActionResult(result);
    }
}

