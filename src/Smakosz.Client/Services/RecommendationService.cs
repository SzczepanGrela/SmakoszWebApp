using Smakosz.Client.Models;

namespace Smakosz.Client.Services;

public class RecommendationService : IRecommendationService
{
    private readonly SmakoszApiClient _api;

    public RecommendationService(SmakoszApiClient api) => _api = api;

    public Task<RecommendationsDto?> GetRecommendationsAsync()
        => _api.GetAsync<RecommendationsDto>("/api/recommendations");
}
