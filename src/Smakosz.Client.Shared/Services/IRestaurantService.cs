using Smakosz.Client.Models;

namespace Smakosz.Client.Services;

public interface IRestaurantService
{
    Task<PagedResult<RestaurantCardDto>?> GetAllAsync(int page = 1, int pageSize = 20);
    Task<RestaurantDetailDto?> GetBySlugAsync(string slug);
    Task<PagedResult<DishCardDto>?> GetDishesAsync(string slug, int page = 1, int pageSize = 20);
    Task<bool> SubmitCorrectionAsync(string slug, CreateDataCorrectionDto dto);
    Task<bool> SuggestIngredientAsync(string slug, CreateIngredientSuggestionDto dto);
    Task<int?> ClaimRestaurantAsync(Guid publicId, string justification);
}

public record CreateDataCorrectionDto(string IssueType, string? Description, string? ProposedValue);
public record CreateIngredientSuggestionDto(string SuggestedName, bool IsAllergen, bool IsVegetarian, bool IsVegan, bool IsGlutenFree, bool IsLactoseFree);
