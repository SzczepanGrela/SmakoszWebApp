using Smakosz.Client.Models;

namespace Smakosz.Client.Services;

public interface IRestaurantService
{
    Task<PagedResult<RestaurantCardDto>?> GetAllAsync(int page = 1, int pageSize = 20);
    Task<RestaurantDetailDto?> GetBySlugAsync(string slug);
    Task<PagedResult<DishCardDto>?> GetDishesAsync(string slug, int page = 1, int pageSize = 20);
}
