using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.RateLimiting;
using Smakosz.Application.Common.Models;
using Smakosz.Application.Features.Admin.Commands.BanUser;
using Smakosz.Application.Features.Admin.Commands.CreateCity;
using Smakosz.Application.Features.Admin.Commands.CreateIngredient;
using Smakosz.Application.Features.Admin.Commands.DeleteCity;
using Smakosz.Application.Features.Admin.Commands.DeleteIngredient;
using Smakosz.Application.Features.Admin.Commands.ReviewIngredientSuggestion;
using Smakosz.Application.Features.Admin.Commands.AdminDisable2fa;
using Smakosz.Application.Features.Admin.Commands.UnbanUser;
using Smakosz.Application.Features.Admin.Commands.ChangeRestaurantStatus;
using Smakosz.Application.Features.Admin.Commands.UpdateRestaurantAdmin;
using Smakosz.Application.Features.Admin.Commands.VerifyRestaurant;
using Smakosz.Application.Features.Admin.Queries.GetRestaurantModerationHistory;
using Smakosz.Domain.Enums;
using Smakosz.Application.Features.Admin.Commands.CreateTag;
using Smakosz.Application.Features.Admin.Commands.DeleteTag;
using Smakosz.Application.Features.Admin.Commands.UpdateCity;
using Smakosz.Application.Features.Admin.Commands.UpdateIngredient;
using Smakosz.Application.Features.Admin.Commands.UpdateTag;
using Smakosz.Application.Features.Admin.Queries.GetAdminIngredients;
using Smakosz.Application.Features.Admin.Queries.GetAdminRestaurantDetail;
using Smakosz.Application.Features.Admin.Queries.GetAdminRestaurants;
using Smakosz.Application.Features.Admin.Queries.GetCities;
using Smakosz.Application.Features.Admin.Queries.GetIngredientSuggestions;
using Smakosz.Application.Features.Admin.Queries.GetTags;
using Smakosz.Application.Features.Admin.Queries.GetUserDetail;
using Smakosz.Application.Features.Admin.Queries.GetUsers;

namespace Smakosz.API.Controllers;

[Authorize(Roles = "Admin")]
[Route("api/admin")]
[DisableRateLimiting]
public class AdminController : ApiController
{
    private readonly IMediator _mediator;

    public AdminController(IMediator mediator)
    {
        _mediator = mediator;
    }

    [HttpGet("users")]
    public async Task<IActionResult> GetUsers(
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20,
        [FromQuery] string? search = null)
    {
        var result = await _mediator.Send(new GetUsersQuery(new PaginationParams(page, pageSize), search));
        return ToActionResult(result);
    }

    [HttpGet("users/{publicId:guid}")]
    public async Task<IActionResult> GetUserDetail(Guid publicId)
    {
        var result = await _mediator.Send(new GetUserDetailQuery(publicId));
        return ToActionResult(result);
    }

    [HttpPost("users/{publicId:guid}/ban")]
    public async Task<IActionResult> BanUser(Guid publicId)
    {
        var result = await _mediator.Send(new BanUserCommand(publicId));
        return ToNoContentResult(result);
    }

    [HttpPost("users/{publicId:guid}/unban")]
    public async Task<IActionResult> UnbanUser(Guid publicId)
    {
        var result = await _mediator.Send(new UnbanUserCommand(publicId));
        return ToNoContentResult(result);
    }

    [HttpPost("users/{publicId:guid}/disable-2fa")]
    public async Task<IActionResult> AdminDisable2fa(Guid publicId)
    {
        var result = await _mediator.Send(new AdminDisable2faCommand(publicId));
        return ToNoContentResult(result);
    }

    [HttpGet("restaurants")]
    public async Task<IActionResult> GetRestaurants(
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20,
        [FromQuery] string? search = null)
    {
        var result = await _mediator.Send(new GetAdminRestaurantsQuery(new PaginationParams(page, pageSize), search));
        return ToActionResult(result);
    }

    [HttpGet("restaurants/by-id/{id:int}")]
    public async Task<IActionResult> GetRestaurantDetail(int id)
    {
        var result = await _mediator.Send(new GetAdminRestaurantDetailQuery(id));
        return ToActionResult(result);
    }

    [HttpPut("restaurants/{publicId:guid}")]
    public async Task<IActionResult> UpdateRestaurant(Guid publicId, [FromBody] UpdateRestaurantAdminRequest request)
    {
        var result = await _mediator.Send(new UpdateRestaurantAdminCommand(
            publicId, request.Name, request.Description, request.CuisineType,
            request.PriceLevel, request.Address, request.PostalCode,
            request.Phone, request.Email, request.Website,
            request.CityId, request.ExpectedVersion));
        return ToNoContentResult(result);
    }

    [HttpGet("restaurants/by-id/{id:int}/moderation-history")]
    public async Task<IActionResult> GetRestaurantModerationHistory(
        int id,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20)
    {
        var result = await _mediator.Send(new GetRestaurantModerationHistoryQuery(id, new PaginationParams(page, pageSize)));
        return ToActionResult(result);
    }

    [HttpPost("restaurants/{publicId:guid}/status")]
    public async Task<IActionResult> ChangeRestaurantStatus(Guid publicId, [FromBody] ChangeRestaurantStatusRequest request)
    {
        if (!Enum.TryParse<RestaurantStatus>(request.Status, true, out var status))
            return BadRequest(new { error = "Nieprawidłowy status" });

        var result = await _mediator.Send(new ChangeRestaurantStatusCommand(publicId, status, request.Reason));
        return ToNoContentResult(result);
    }

    [HttpPost("restaurants/{publicId:guid}/verify")]
    public async Task<IActionResult> VerifyRestaurant(Guid publicId)
    {
        var result = await _mediator.Send(new VerifyRestaurantCommand(publicId));
        return ToNoContentResult(result);
    }

    [HttpGet("ingredients")]
    public async Task<IActionResult> GetIngredients(
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20,
        [FromQuery] string? search = null)
    {
        var result = await _mediator.Send(new GetAdminIngredientsQuery(new PaginationParams(page, pageSize), search));
        return ToActionResult(result);
    }

    [HttpPost("ingredients")]
    public async Task<IActionResult> CreateIngredient([FromBody] CreateIngredientRequest request)
    {
        var result = await _mediator.Send(new CreateIngredientCommand(
            request.Name, request.IsAllergen, request.IsVegetarian,
            request.IsVegan, request.IsGlutenFree, request.IsLactoseFree));
        return ToCreatedResult(result);
    }

    [HttpPut("ingredients/{ingredientId:int}")]
    public async Task<IActionResult> UpdateIngredient(int ingredientId, [FromBody] UpdateIngredientRequest request)
    {
        var result = await _mediator.Send(new UpdateIngredientCommand(
            ingredientId, request.Name, request.IsAllergen, request.IsVegetarian, request.IsVegan, request.IsGlutenFree, request.IsLactoseFree));
        return ToNoContentResult(result);
    }

    [HttpDelete("ingredients/{ingredientId:int}")]
    public async Task<IActionResult> DeleteIngredient(int ingredientId)
    {
        var result = await _mediator.Send(new DeleteIngredientCommand(ingredientId));
        return ToNoContentResult(result);
    }

    [HttpGet("cities")]
    public async Task<IActionResult> GetCities(
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20,
        [FromQuery] string? search = null)
    {
        var result = await _mediator.Send(new GetCitiesQuery(new PaginationParams(page, pageSize), search));
        return ToActionResult(result);
    }

    [HttpPost("cities")]
    public async Task<IActionResult> CreateCity([FromBody] CreateCityRequest request)
    {
        var result = await _mediator.Send(new CreateCityCommand(request.Name, request.Region));
        return ToCreatedResult(result);
    }

    [HttpPut("cities/{cityId:int}")]
    public async Task<IActionResult> UpdateCity(int cityId, [FromBody] UpdateCityRequest request)
    {
        var result = await _mediator.Send(new UpdateCityCommand(cityId, request.Name, request.Region));
        return ToNoContentResult(result);
    }

    [HttpDelete("cities/{cityId:int}")]
    public async Task<IActionResult> DeleteCity(int cityId)
    {
        var result = await _mediator.Send(new DeleteCityCommand(cityId));
        return ToNoContentResult(result);
    }

    [HttpGet("tags")]
    public async Task<IActionResult> GetTags(
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20,
        [FromQuery] string? search = null)
    {
        var result = await _mediator.Send(new GetTagsQuery(new PaginationParams(page, pageSize), search));
        return ToActionResult(result);
    }

    [HttpPost("tags")]
    public async Task<IActionResult> CreateTag([FromBody] CreateTagRequest request)
    {
        var result = await _mediator.Send(new CreateTagCommand(request.Name, request.Category, request.TargetEntity, request.DisplayColor));
        return ToCreatedResult(result);
    }

    [HttpPut("tags/{tagId:int}")]
    public async Task<IActionResult> UpdateTag(int tagId, [FromBody] UpdateTagRequest request)
    {
        var result = await _mediator.Send(new UpdateTagCommand(tagId, request.Name, request.Category, request.TargetEntity, request.DisplayColor));
        return ToNoContentResult(result);
    }

    [HttpDelete("tags/{tagId:int}")]
    public async Task<IActionResult> DeleteTag(int tagId)
    {
        var result = await _mediator.Send(new DeleteTagCommand(tagId));
        return ToNoContentResult(result);
    }

    [HttpGet("ingredient-suggestions")]
    public async Task<IActionResult> GetIngredientSuggestions(
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20,
        [FromQuery] string? status = null)
    {
        var result = await _mediator.Send(new GetIngredientSuggestionsQuery(new PaginationParams(page, pageSize), status));
        return ToActionResult(result);
    }

    [HttpPost("ingredient-suggestions/{suggestionId:int}/review")]
    public async Task<IActionResult> ReviewIngredientSuggestion(
        int suggestionId,
        [FromBody] ReviewIngredientSuggestionRequest request)
    {
        var result = await _mediator.Send(new ReviewIngredientSuggestionCommand(
            suggestionId, request.Approve, request.AdminNote,
            request.IsAllergen, request.IsVegetarian, request.IsVegan,
            request.IsGlutenFree, request.IsLactoseFree, request.IconUrl));
        return ToNoContentResult(result);
    }
}

public record CreateIngredientRequest(
    string Name,
    bool IsAllergen,
    bool IsVegetarian,
    bool IsVegan,
    bool IsGlutenFree,
    bool IsLactoseFree);
public record UpdateIngredientRequest(string? Name, bool? IsAllergen, bool? IsVegetarian, bool? IsVegan, bool? IsGlutenFree, bool? IsLactoseFree);
public record CreateTagRequest(string Name, string Category, string TargetEntity, string? DisplayColor);
public record UpdateTagRequest(string? Name, string? Category, string? TargetEntity, string? DisplayColor);
public record CreateCityRequest(string Name, string? Region);
public record UpdateCityRequest(string? Name, string? Region);
public record ChangeRestaurantStatusRequest(string Status, string? Reason);
public record UpdateRestaurantAdminRequest(
    string? Name,
    string? Description,
    string? CuisineType,
    int? PriceLevel,
    string? Address,
    string? PostalCode,
    string? Phone,
    string? Email,
    string? Website,
    int? CityId,
    int ExpectedVersion);
public record ReviewIngredientSuggestionRequest(
    bool Approve,
    string? AdminNote,
    bool? IsAllergen = null,
    bool? IsVegetarian = null,
    bool? IsVegan = null,
    bool? IsGlutenFree = null,
    bool? IsLactoseFree = null,
    string? IconUrl = null);
