using Smakosz.Client.Models;

namespace Smakosz.Client.Services;

public interface IIngredientService
{
    Task<List<IngredientDto>> GetAllAsync();
}
