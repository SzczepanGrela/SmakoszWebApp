using Smakosz.Client.Models;

namespace Smakosz.Client.Services;

public class BusinessService : IBusinessService
{
    private readonly SmakoszApiClient _api;

    public BusinessService(SmakoszApiClient api) => _api = api;

    public Task<BusinessDashboardDto?> GetDashboardAsync()
        => _api.GetAsync<BusinessDashboardDto>("/api/business/dashboard");

    public Task<BusinessRestaurantDto?> GetRestaurantInfoAsync()
        => _api.GetAsync<BusinessRestaurantDto>("/api/business/restaurant");

    public async Task<bool> UpdateRestaurantInfoAsync(BusinessRestaurantDto dto)
    {
        var response = await _api.PutApiResponseAsync<object>("/api/business/restaurant", dto);
        return response.Success;
    }

    public async Task<List<OpeningHoursDto>> GetOpeningHoursAsync()
        => await _api.GetAsync<List<OpeningHoursDto>>("/api/business/opening-hours") ?? [];

    public async Task<bool> UpdateOpeningHoursAsync(List<OpeningHoursDto> hours)
    {
        var response = await _api.PutApiResponseAsync<object>("/api/business/opening-hours", new { Hours = hours });
        return response.Success;
    }

    public async Task<List<MenuSectionDto>> GetMenuSectionsAsync()
        => await _api.GetAsync<List<MenuSectionDto>>("/api/business/menu-sections") ?? [];

    public async Task<bool> UpdateMenuSectionsAsync(List<MenuSectionDto> sections)
    {
        var response = await _api.PutApiResponseAsync<object>("/api/business/menu-sections/reorder",
            new { SectionIds = sections.OrderBy(s => s.DisplayOrder).Select(s => s.SectionName).ToList() });
        return response.Success;
    }

    public Task<PagedResult<BusinessDishDto>?> GetDishesAsync(int page = 1)
        => _api.GetAsync<PagedResult<BusinessDishDto>>($"/api/business/dishes?page={page}");

    public Task<DishDetailDto?> GetDishAsync(Guid id)
        => _api.GetAsync<DishDetailDto>($"/api/business/dishes/{id}");

    public async Task<List<BusinessMenuSectionDto>> GetBusinessMenuSectionsAsync()
        => await _api.GetAsync<List<BusinessMenuSectionDto>>("/api/business/menu-sections") ?? [];

    public async Task<bool> CreateDishAsync(DishDetailDto dish, List<int>? ingredientIds = null, List<int>? sectionIds = null)
    {
        var response = await _api.PostApiResponseAsync<object>("/api/business/dishes", new
        {
            DishName = dish.DishName,
            Price = dish.Price,
            Description = dish.Description,
            Calories = dish.Calories,
            IsAvailable = dish.IsAvailable,
            IsSpicy = dish.IsSpicy,
            DishCategoryTagName = dish.CategoryTagName,
            IngredientIds = ingredientIds,
            SectionIds = sectionIds
        });
        return response.Success;
    }

    public async Task<bool> UpdateDishAsync(Guid id, DishDetailDto dish, List<int>? ingredientIds = null, List<int>? sectionIds = null)
    {
        var response = await _api.PutApiResponseAsync<object>($"/api/business/dishes/{id}", new
        {
            DishName = dish.DishName,
            Price = dish.Price,
            Description = dish.Description,
            Calories = dish.Calories,
            IsAvailable = dish.IsAvailable,
            IsSpicy = dish.IsSpicy,
            DishCategoryTagName = dish.CategoryTagName,
            IngredientIds = ingredientIds,
            SectionIds = sectionIds
        });
        return response.Success;
    }

    public async Task<bool> DeleteDishAsync(Guid id)
        => await _api.DeleteAsync($"/api/business/dishes/{id}");

    public Task<PagedResult<ReviewCardDto>?> GetReviewsAsync(int page = 1)
        => _api.GetAsync<PagedResult<ReviewCardDto>>($"/api/business/reviews?page={page}");

    public Task<BusinessStatsDto?> GetStatsAsync()
        => _api.GetAsync<BusinessStatsDto>("/api/business/stats");

    public Task<BusinessChartDataDto?> GetChartDataAsync(int days = 30)
        => _api.GetAsync<BusinessChartDataDto>($"/api/business/stats/charts?days={days}");

    public async Task<List<EditRequestSummaryDto>> GetEditRequestsAsync()
        => await _api.GetAsync<List<EditRequestSummaryDto>>("/api/business/edit-requests") ?? [];

    public Task<RegistrationStatusDto?> GetRegistrationStatusAsync()
        => _api.GetAsync<RegistrationStatusDto>("/api/business/registration-status");

    public async Task<bool> RegisterBusinessAsync(BusinessRestaurantDto dto)
    {
        var response = await _api.PostApiResponseAsync<object>("/api/business/register", dto);
        return response.Success;
    }

    public async Task<bool> CreateMenuSectionAsync(string name)
    {
        var response = await _api.PostApiResponseAsync<object>("/api/business/menu-sections", new { Name = name });
        return response.Success;
    }

    public async Task<bool> UpdateMenuSectionAsync(int sectionId, string name)
    {
        var response = await _api.PutApiResponseAsync<object>($"/api/business/menu-sections/{sectionId}", new { Name = name });
        return response.Success;
    }

    public Task<bool> DeleteMenuSectionAsync(int sectionId)
        => _api.DeleteAsync($"/api/business/menu-sections/{sectionId}");

    public async Task<bool> UpdateDishAvailabilityAsync(Guid publicId, bool isAvailable)
    {
        var response = await _api.PutApiResponseAsync<object>($"/api/business/dishes/{publicId}/availability", new { IsAvailable = isAvailable });
        return response.Success;
    }

    public async Task<bool> CreateEditRequestAsync(CreateEditRequestDto dto)
    {
        var response = await _api.PostApiResponseAsync<object>("/api/business/edit-requests", dto);
        return response.Success;
    }

    public async Task<bool> SetDishIngredientsAsync(Guid publicId, List<int> ingredientIds)
    {
        var response = await _api.PutApiResponseAsync<object>($"/api/business/dishes/{publicId}/ingredients", new { IngredientIds = ingredientIds });
        return response.Success;
    }

    public async Task<bool> CreateIngredientSuggestionAsync(string restaurantSlug, string suggestedName)
    {
        var response = await _api.PostApiResponseAsync<object>($"/api/restaurants/{restaurantSlug}/ingredient-suggestions",
            new { SuggestedName = suggestedName });
        return response.Success;
    }

    public async Task<bool> ReorderMenuSectionsAsync(List<int> sectionIds)
    {
        var response = await _api.PutApiResponseAsync<object>("/api/business/menu-sections/reorder",
            new { SectionIds = sectionIds });
        return response.Success;
    }
}
