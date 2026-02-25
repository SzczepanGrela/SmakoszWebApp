using Smakosz.Client.Models;

namespace Smakosz.Client.Services;

public interface IDishService
{
    Task<DishDetailDto?> GetBySlugAsync(string slug);
    Task<DishCardDto?> GetRandomAsync();
    Task<PagedResult<ReviewCardDto>?> GetReviewsAsync(string slug, int page = 1, int pageSize = 10);
}
