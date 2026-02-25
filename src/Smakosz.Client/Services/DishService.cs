using Smakosz.Client.Models;

namespace Smakosz.Client.Services;

public class DishService : IDishService
{
    private readonly SmakoszApiClient _api;

    public DishService(SmakoszApiClient api) => _api = api;

    public Task<DishDetailDto?> GetBySlugAsync(string slug)
        => _api.GetAsync<DishDetailDto>($"/api/dishes/{slug}");

    public Task<DishCardDto?> GetRandomAsync()
        => _api.GetAsync<DishCardDto>("/api/dishes/random");

    public Task<PagedResult<ReviewCardDto>?> GetReviewsAsync(string slug, int page = 1, int pageSize = 10)
        => _api.GetAsync<PagedResult<ReviewCardDto>>($"/api/dishes/{slug}/reviews?page={page}&pageSize={pageSize}");
}
