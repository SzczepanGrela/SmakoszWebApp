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
using Smakosz.Application.Features.Admin.Commands.AdminResetUserPassword;
using Smakosz.Application.Features.Admin.Commands.ChangeUserRole;
using Smakosz.Application.Features.Admin.Commands.CreatePrivilegedAccount;
using Smakosz.Application.Features.Admin.Commands.UnbanUser;
using Smakosz.Application.Features.Admin.Commands.ChangeRestaurantStatus;
using Smakosz.Application.Features.Admin.Commands.CreateBannedIdentifier;
using Smakosz.Application.Features.Admin.Commands.CreateForbiddenWord;
using Smakosz.Application.Features.Admin.Commands.DeleteBannedIdentifier;
using Smakosz.Application.Features.Admin.Commands.UpdateBannedIdentifier;
using Smakosz.Application.Features.Admin.Queries.GetBannedIdentifiers;
using Smakosz.Application.Features.Admin.Commands.DeleteForbiddenWord;
using Smakosz.Application.Features.Admin.Commands.TestForbiddenWord;
using Smakosz.Application.Features.Admin.Commands.UpdateForbiddenWord;
using Smakosz.Application.Features.Admin.Queries.GetForbiddenWords;
using Smakosz.Application.Features.Admin.Commands.UpdateRestaurantAdmin;
using Smakosz.Application.Features.Admin.Commands.VerifyRestaurant;
using Smakosz.Application.Features.Admin.Queries.GetRestaurantModerationHistory;
using Smakosz.Domain.Enums;
using Smakosz.Application.Features.Admin.Commands.CreateRejectionReason;
using Smakosz.Application.Features.Admin.Commands.CreateTag;
using Smakosz.Application.Features.Admin.Commands.DeleteRejectionReason;
using Smakosz.Application.Features.Admin.Commands.DeleteTag;
using Smakosz.Application.Features.Admin.Commands.UpdateCity;
using Smakosz.Application.Features.Admin.Commands.UpdateIngredient;
using Smakosz.Application.Features.Admin.Commands.UpdateRejectionReason;
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
        [FromQuery] string? search = null,
        [FromQuery] UserRole? role = null)
    {
        var result = await _mediator.Send(new GetUsersQuery(new PaginationParams(page, pageSize), search, role));
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

    [HttpPost("users/{publicId:guid}/reset-password")]
    public async Task<IActionResult> ResetUserPassword(Guid publicId)
    {
        var result = await _mediator.Send(new AdminResetUserPasswordCommand(publicId));
        return ToNoContentResult(result);
    }

    [HttpPost("users")]
    public async Task<IActionResult> CreatePrivilegedAccount([FromBody] CreatePrivilegedAccountRequest body)
    {
        var result = await _mediator.Send(new CreatePrivilegedAccountCommand(body.Email, body.Username, body.Role));
        return ToActionResult(result);
    }

    [HttpPut("users/{publicId:guid}/role")]
    public async Task<IActionResult> ChangeUserRole(Guid publicId, [FromBody] ChangeUserRoleRequest body)
    {
        var result = await _mediator.Send(new ChangeUserRoleCommand(publicId, body.Role, body.Reason));
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
            publicId, request.Name, request.Description, request.CuisineTypeId,
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

    [HttpGet("banned-identifiers")]
    public async Task<IActionResult> GetBannedIdentifiers(
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20,
        [FromQuery] string? type = null,
        [FromQuery] bool includeExpired = false)
    {
        var result = await _mediator.Send(new GetBannedIdentifiersQuery(new PaginationParams(page, pageSize), type, includeExpired));
        return ToActionResult(result);
    }

    [HttpPost("banned-identifiers")]
    public async Task<IActionResult> CreateBannedIdentifier([FromBody] CreateBannedIdentifierRequest request)
    {
        if (!Enum.TryParse<BannedIdentifierType>(request.Type, true, out var banType))
            return BadRequest(new { error = "Nieprawidłowy typ" });

        var result = await _mediator.Send(new CreateBannedIdentifierCommand(banType, request.Value, request.Reason, request.ExpiresAt));
        return ToActionResult(result);
    }

    [HttpPut("banned-identifiers/{id:int}")]
    public async Task<IActionResult> UpdateBannedIdentifier(int id, [FromBody] UpdateBannedIdentifierRequest request)
    {
        var result = await _mediator.Send(new UpdateBannedIdentifierCommand(id, request.Reason, request.ExpiresAt, request.ClearExpiration));
        return ToNoContentResult(result);
    }

    [HttpDelete("banned-identifiers/{id:int}")]
    public async Task<IActionResult> DeleteBannedIdentifier(int id)
    {
        var result = await _mediator.Send(new DeleteBannedIdentifierCommand(id));
        return ToNoContentResult(result);
    }

    [HttpGet("forbidden-words")]
    public async Task<IActionResult> GetForbiddenWords(
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20,
        [FromQuery] string? search = null)
    {
        var result = await _mediator.Send(new GetForbiddenWordsQuery(new PaginationParams(page, pageSize), search));
        return ToActionResult(result);
    }

    [HttpPost("forbidden-words")]
    public async Task<IActionResult> CreateForbiddenWord([FromBody] CreateForbiddenWordRequest request)
    {
        if (!Enum.TryParse<ForbiddenWordCategory>(request.Category, true, out var category))
            return BadRequest(new { error = "Nieprawidłowa kategoria" });

        var result = await _mediator.Send(new CreateForbiddenWordCommand(request.Word, category, request.IsRegex));
        return ToActionResult(result);
    }

    [HttpPut("forbidden-words/{id:int}")]
    public async Task<IActionResult> UpdateForbiddenWord(int id, [FromBody] UpdateForbiddenWordRequest request)
    {
        ForbiddenWordCategory? category = null;
        if (request.Category is not null && !Enum.TryParse(request.Category, true, out ForbiddenWordCategory parsed))
            return BadRequest(new { error = "Nieprawidłowa kategoria" });
        else if (request.Category is not null)
            category = Enum.Parse<ForbiddenWordCategory>(request.Category, true);

        var result = await _mediator.Send(new UpdateForbiddenWordCommand(id, request.Word, category, request.IsRegex));
        return ToNoContentResult(result);
    }

    [HttpDelete("forbidden-words/{id:int}")]
    public async Task<IActionResult> DeleteForbiddenWord(int id)
    {
        var result = await _mediator.Send(new DeleteForbiddenWordCommand(id));
        return ToNoContentResult(result);
    }

    [HttpPost("forbidden-words/test")]
    public async Task<IActionResult> TestForbiddenWord([FromBody] TestForbiddenWordRequest request)
    {
        var categories = request.Categories
            .Select(c => Enum.TryParse<ForbiddenWordCategory>(c, true, out var cat) ? cat : (ForbiddenWordCategory?)null)
            .Where(c => c.HasValue)
            .Select(c => c!.Value)
            .ToArray();

        var result = await _mediator.Send(new TestForbiddenWordCommand(request.Text, categories));
        return ToActionResult(result);
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

    [HttpPost("rejection-reasons")]
    public async Task<IActionResult> CreateRejectionReason([FromBody] CreateRejectionReasonRequest request)
    {
        var result = await _mediator.Send(new CreateRejectionReasonCommand(
            request.ReasonCode,
            request.Category,
            request.AdminLabel,
            request.UserMessageTemplate,
            request.IsActive));
        return ToCreatedResult(result);
    }

    [HttpPut("rejection-reasons/{reasonCode}")]
    public async Task<IActionResult> UpdateRejectionReason(string reasonCode, [FromBody] UpdateRejectionReasonRequest request)
    {
        var result = await _mediator.Send(new UpdateRejectionReasonCommand(
            reasonCode,
            request.Category,
            request.AdminLabel,
            request.UserMessageTemplate,
            request.IsActive));
        return ToNoContentResult(result);
    }

    [HttpDelete("rejection-reasons/{reasonCode}")]
    public async Task<IActionResult> DeleteRejectionReason(string reasonCode)
    {
        var result = await _mediator.Send(new DeleteRejectionReasonCommand(reasonCode));
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

public record CreatePrivilegedAccountRequest(string Email, string Username, UserRole Role);
public record ChangeUserRoleRequest(UserRole Role, string? Reason);

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
public record CreateBannedIdentifierRequest(string Type, string Value, string? Reason, DateTime? ExpiresAt);
public record UpdateBannedIdentifierRequest(string? Reason, DateTime? ExpiresAt, bool ClearExpiration = false);
public record CreateForbiddenWordRequest(string Word, string Category, bool IsRegex);
public record UpdateForbiddenWordRequest(string? Word, string? Category, bool? IsRegex);
public record TestForbiddenWordRequest(string Text, string[] Categories);
public record CreateRejectionReasonRequest(
    string ReasonCode,
    string Category,
    string AdminLabel,
    string UserMessageTemplate,
    bool IsActive);
public record UpdateRejectionReasonRequest(
    string Category,
    string AdminLabel,
    string UserMessageTemplate,
    bool IsActive);
public record UpdateRestaurantAdminRequest(
    string? Name,
    string? Description,
    int? CuisineTypeId,
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
