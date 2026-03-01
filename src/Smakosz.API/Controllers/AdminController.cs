using Microsoft.AspNetCore.Authorization;
using Smakosz.Application.Common.Models;
using Smakosz.Application.Features.Admin.Commands.BanUser;
using Smakosz.Application.Features.Admin.Commands.CreateCity;
using Smakosz.Application.Features.Admin.Commands.CreateIngredient;
using Smakosz.Application.Features.Admin.Commands.DeleteCity;
using Smakosz.Application.Features.Admin.Commands.DeleteIngredient;
using Smakosz.Application.Features.Admin.Commands.ReviewIngredientSuggestion;
using Smakosz.Application.Features.Admin.Commands.UnbanUser;
using Smakosz.Application.Features.Admin.Commands.UpdateCity;
using Smakosz.Application.Features.Admin.Commands.UpdateIngredient;
using Smakosz.Application.Features.Admin.Commands.UpdateReportStatus;
using Smakosz.Application.Features.Admin.Queries.GetAdminDashboard;
using Smakosz.Application.Features.Admin.Queries.GetAdminIngredients;
using Smakosz.Application.Features.Admin.Queries.GetAdminRestaurants;
using Smakosz.Application.Features.Admin.Queries.GetCities;
using Smakosz.Application.Features.Admin.Queries.GetIngredientSuggestions;
using Smakosz.Application.Features.Admin.Queries.GetReports;
using Smakosz.Application.Features.Admin.Queries.GetUserDetail;
using Smakosz.Application.Features.Admin.Queries.GetUsers;

namespace Smakosz.API.Controllers;

[Authorize(Roles = "Admin")]
[Route("api/admin")]
public class AdminController : ApiController
{
    private readonly IMediator _mediator;

    public AdminController(IMediator mediator)
    {
        _mediator = mediator;
    }

    [HttpGet("dashboard")]
    public async Task<IActionResult> GetDashboard()
    {
        var result = await _mediator.Send(new GetAdminDashboardQuery());
        return ToActionResult(result);
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

    [HttpGet("restaurants")]
    public async Task<IActionResult> GetRestaurants(
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20,
        [FromQuery] string? search = null)
    {
        var result = await _mediator.Send(new GetAdminRestaurantsQuery(new PaginationParams(page, pageSize), search));
        return ToActionResult(result);
    }

    [HttpGet("reports")]
    public async Task<IActionResult> GetReports(
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20,
        [FromQuery] string? status = null)
    {
        var result = await _mediator.Send(new GetReportsQuery(new PaginationParams(page, pageSize), status));
        return ToActionResult(result);
    }

    [HttpPut("reports/{reportId:int}/status")]
    public async Task<IActionResult> UpdateReportStatus(int reportId, [FromBody] UpdateReportStatusRequest request)
    {
        var result = await _mediator.Send(new UpdateReportStatusCommand(reportId, request.Status));
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
            suggestionId, request.Approve, request.AdminNote));
        return ToNoContentResult(result);
    }
}

public record UpdateReportStatusRequest(string Status);
public record CreateIngredientRequest(
    string Name,
    bool IsAllergen,
    bool IsVegetarian,
    bool IsVegan,
    bool IsGlutenFree,
    bool IsLactoseFree);
public record UpdateIngredientRequest(string? Name, bool? IsAllergen, bool? IsVegetarian, bool? IsVegan, bool? IsGlutenFree, bool? IsLactoseFree);
public record CreateCityRequest(string Name, string? Region);
public record UpdateCityRequest(string? Name, string? Region);
public record ReviewIngredientSuggestionRequest(bool Approve, string? AdminNote);
