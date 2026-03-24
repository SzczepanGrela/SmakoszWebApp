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

    public Task<PagedResult<DishCardDto>?> GetDishesAsync(int page = 1)
        => _api.GetAsync<PagedResult<DishCardDto>>($"/api/business/dishes?page={page}");

    public Task<DishDetailDto?> GetDishAsync(Guid id)
        => _api.GetAsync<DishDetailDto>($"/api/business/dishes/{id}");

    public async Task<bool> CreateDishAsync(DishDetailDto dish)
    {
        var response = await _api.PostApiResponseAsync<object>("/api/business/dishes", dish);
        return response.Success;
    }

    public async Task<bool> UpdateDishAsync(Guid id, DishDetailDto dish)
    {
        var response = await _api.PutApiResponseAsync<object>($"/api/business/dishes/{id}", dish);
        return response.Success;
    }

    public async Task<bool> DeleteDishAsync(Guid id)
        => await _api.DeleteAsync($"/api/business/dishes/{id}");

    public Task<PagedResult<ReviewCardDto>?> GetReviewsAsync(int page = 1)
        => _api.GetAsync<PagedResult<ReviewCardDto>>($"/api/business/reviews?page={page}");

    public async Task<List<BusinessStatsDto>> GetStatsAsync(string period = "week")
        => await _api.GetAsync<List<BusinessStatsDto>>($"/api/business/stats?period={period}") ?? [];

    public async Task<List<EditRequestSummaryDto>> GetEditRequestsAsync()
        => await _api.GetAsync<List<EditRequestSummaryDto>>("/api/business/edit-requests") ?? [];

    public Task<RegistrationStatusDto?> GetRegistrationStatusAsync()
        => _api.GetAsync<RegistrationStatusDto>("/api/business/registration-status");

    public async Task<bool> RegisterBusinessAsync(BusinessRestaurantDto dto)
    {
        var response = await _api.PostApiResponseAsync<object>("/api/business/register", dto);
        return response.Success;
    }
}
