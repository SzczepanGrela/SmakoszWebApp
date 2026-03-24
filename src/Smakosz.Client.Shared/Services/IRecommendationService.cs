using Smakosz.Client.Models;

namespace Smakosz.Client.Services;

public interface IRecommendationService
{
    Task<RecommendationsDto?> GetRecommendationsAsync();
}
