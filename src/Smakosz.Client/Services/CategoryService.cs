using Smakosz.Client.Models;

namespace Smakosz.Client.Services;

public class CategoryService : ICategoryService
{
    private readonly SmakoszApiClient _api;

    public CategoryService(SmakoszApiClient api) => _api = api;

    public Task<List<CategoryDto>?> GetCategoriesAsync()
        => _api.GetAsync<List<CategoryDto>>("/api/categories");
}
