using Microsoft.AspNetCore.Authorization;
using Smakosz.Application.Common.Models;
using Smakosz.Application.Features.Me.Commands.ChangePassword;
using Smakosz.Application.Features.Me.Commands.FavoriteRestaurant;
using Smakosz.Application.Features.Me.Commands.FollowUser;
using Smakosz.Application.Features.Me.Commands.MarkAllNotificationsRead;
using Smakosz.Application.Features.Me.Commands.MarkNotificationRead;
using Smakosz.Application.Features.Me.Commands.RevokeAllSessions;
using Smakosz.Application.Features.Me.Commands.RevokeSession;
using Smakosz.Application.Features.Me.Commands.SaveDish;
using Smakosz.Application.Features.Me.Commands.UnfavoriteRestaurant;
using Smakosz.Application.Features.Me.Commands.UnfollowUser;
using Smakosz.Application.Features.Me.Commands.UnsaveDish;
using Smakosz.Application.Features.Me.Commands.UpdateNotificationSettings;
using Smakosz.Application.Features.Me.Commands.UpdateProfile;
using Smakosz.Application.Features.Me.Queries.GetFavoriteRestaurants;
using Smakosz.Application.Features.Me.Queries.GetMyFollowers;
using Smakosz.Application.Features.Me.Queries.GetMyFollowing;
using Smakosz.Application.Features.Me.Queries.GetMyNotifications;
using Smakosz.Application.Features.Me.Queries.GetMyProfile;
using Smakosz.Application.Features.Me.Queries.GetMyReviews;
using Smakosz.Application.Features.Me.Queries.GetMySessions;
using Smakosz.Application.Features.Me.Queries.GetNotificationSettings;
using Smakosz.Application.Features.Me.Queries.GetSavedDishes;
using Smakosz.Application.Features.Me.Queries.GetUnreadNotificationCount;

namespace Smakosz.API.Controllers;

[Authorize]
[Route("api/me")]
public class MeController : ApiController
{
    private readonly IMediator _mediator;

    public MeController(IMediator mediator)
    {
        _mediator = mediator;
    }

    [HttpGet]
    public async Task<IActionResult> GetMyProfile()
    {
        var result = await _mediator.Send(new GetMyProfileQuery());
        return ToActionResult(result);
    }

    [HttpPut]
    public async Task<IActionResult> UpdateProfile([FromBody] UpdateProfileCommand command)
    {
        var result = await _mediator.Send(command);
        return ToNoContentResult(result);
    }

    [HttpPut("password")]
    public async Task<IActionResult> ChangePassword([FromBody] ChangePasswordCommand command)
    {
        var result = await _mediator.Send(command);
        return ToNoContentResult(result);
    }

    [HttpGet("following")]
    public async Task<IActionResult> GetMyFollowing(
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20)
    {
        var result = await _mediator.Send(new GetMyFollowingQuery(new PaginationParams(page, pageSize)));
        return ToActionResult(result);
    }

    [HttpGet("followers")]
    public async Task<IActionResult> GetMyFollowers(
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20)
    {
        var result = await _mediator.Send(new GetMyFollowersQuery(new PaginationParams(page, pageSize)));
        return ToActionResult(result);
    }

    [HttpPost("following/{slug}")]
    public async Task<IActionResult> FollowUser(string slug)
    {
        var result = await _mediator.Send(new FollowUserCommand(slug));
        return ToNoContentResult(result);
    }

    [HttpDelete("following/{slug}")]
    public async Task<IActionResult> UnfollowUser(string slug)
    {
        var result = await _mediator.Send(new UnfollowUserCommand(slug));
        return ToNoContentResult(result);
    }

    [HttpGet("saved-dishes")]
    public async Task<IActionResult> GetSavedDishes(
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20)
    {
        var result = await _mediator.Send(new GetSavedDishesQuery(new PaginationParams(page, pageSize)));
        return ToActionResult(result);
    }

    [HttpPost("saved-dishes/{dishSlug}")]
    public async Task<IActionResult> SaveDish(string dishSlug)
    {
        var result = await _mediator.Send(new SaveDishCommand(dishSlug));
        return ToNoContentResult(result);
    }

    [HttpDelete("saved-dishes/{dishSlug}")]
    public async Task<IActionResult> UnsaveDish(string dishSlug)
    {
        var result = await _mediator.Send(new UnsaveDishCommand(dishSlug));
        return ToNoContentResult(result);
    }

    [HttpGet("favorite-restaurants")]
    public async Task<IActionResult> GetFavoriteRestaurants(
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20)
    {
        var result = await _mediator.Send(new GetFavoriteRestaurantsQuery(new PaginationParams(page, pageSize)));
        return ToActionResult(result);
    }

    [HttpPost("favorite-restaurants/{restaurantSlug}")]
    public async Task<IActionResult> FavoriteRestaurant(string restaurantSlug)
    {
        var result = await _mediator.Send(new FavoriteRestaurantCommand(restaurantSlug));
        return ToNoContentResult(result);
    }

    [HttpDelete("favorite-restaurants/{restaurantSlug}")]
    public async Task<IActionResult> UnfavoriteRestaurant(string restaurantSlug)
    {
        var result = await _mediator.Send(new UnfavoriteRestaurantCommand(restaurantSlug));
        return ToNoContentResult(result);
    }

    [HttpGet("sessions")]
    public async Task<IActionResult> GetSessions()
    {
        var result = await _mediator.Send(new GetMySessionsQuery());
        return ToActionResult(result);
    }

    [HttpDelete("sessions/{sessionId:long}")]
    public async Task<IActionResult> RevokeSession(long sessionId)
    {
        var result = await _mediator.Send(new RevokeSessionCommand(sessionId));
        return ToNoContentResult(result);
    }

    [HttpDelete("sessions")]
    public async Task<IActionResult> RevokeAllSessions()
    {
        var result = await _mediator.Send(new RevokeAllSessionsCommand());
        return ToNoContentResult(result);
    }

    [HttpGet("reviews")]
    public async Task<IActionResult> GetMyReviews(
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20)
    {
        var result = await _mediator.Send(new GetMyReviewsQuery(new PaginationParams(page, pageSize)));
        return ToActionResult(result);
    }

    [HttpGet("notifications")]
    public async Task<IActionResult> GetNotifications(
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20)
    {
        var result = await _mediator.Send(new GetMyNotificationsQuery(new PaginationParams(page, pageSize)));
        return ToActionResult(result);
    }

    [HttpGet("notifications/unread-count")]
    public async Task<IActionResult> GetUnreadNotificationCount()
    {
        var result = await _mediator.Send(new GetUnreadNotificationCountQuery());
        return ToActionResult(result);
    }

    [HttpGet("notification-settings")]
    public async Task<IActionResult> GetNotificationSettings()
    {
        var result = await _mediator.Send(new GetNotificationSettingsQuery());
        return ToActionResult(result);
    }

    [HttpPut("notification-settings")]
    public async Task<IActionResult> UpdateNotificationSettings([FromBody] UpdateNotificationSettingsCommand command)
    {
        var result = await _mediator.Send(command);
        return ToNoContentResult(result);
    }

    [HttpPut("notifications/{publicId:guid}/read")]
    public async Task<IActionResult> MarkNotificationRead(Guid publicId)
    {
        var result = await _mediator.Send(new MarkNotificationReadCommand(publicId));
        return ToNoContentResult(result);
    }

    [HttpPut("notifications/read-all")]
    public async Task<IActionResult> MarkAllNotificationsRead()
    {
        var result = await _mediator.Send(new MarkAllNotificationsReadCommand());
        return ToNoContentResult(result);
    }
}
