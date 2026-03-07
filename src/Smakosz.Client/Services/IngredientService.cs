using Smakosz.Client.Models;

namespace Smakosz.Client.Services;

public class IngredientService : IIngredientService
{
    private readonly SmakoszApiClient _api;

    public IngredientService(SmakoszApiClient api) => _api = api;

    public async Task<List<IngredientDto>> GetAllAsync()
        => await _api.GetAsync<List<IngredientDto>>("/api/ingredients") ?? [];
}
