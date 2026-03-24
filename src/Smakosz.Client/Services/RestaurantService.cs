using Smakosz.Client.Models;

namespace Smakosz.Client.Services;

public class RestaurantService : IRestaurantService
{
    private readonly SmakoszApiClient _api;

    public RestaurantService(SmakoszApiClient api) => _api = api;

    public Task<PagedResult<RestaurantCardDto>?> GetAllAsync(int page = 1, int pageSize = 20)
        => _api.GetAsync<PagedResult<RestaurantCardDto>>($"/api/restaurants?page={page}&pageSize={pageSize}");

    public Task<RestaurantDetailDto?> GetBySlugAsync(string slug)
        => _api.GetAsync<RestaurantDetailDto>($"/api/restaurants/{slug}");

    public Task<PagedResult<DishCardDto>?> GetDishesAsync(string slug, int page = 1, int pageSize = 20)
        => _api.GetAsync<PagedResult<DishCardDto>>($"/api/restaurants/{slug}/dishes?page={page}&pageSize={pageSize}");
}
