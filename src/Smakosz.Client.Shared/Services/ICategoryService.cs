using Smakosz.Client.Models;

namespace Smakosz.Client.Services;

public interface ICategoryService
{
    Task<List<CategoryDto>?> GetCategoriesAsync();
}
