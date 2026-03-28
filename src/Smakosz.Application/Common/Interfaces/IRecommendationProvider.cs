namespace Smakosz.Application.Common.Interfaces;

public interface IRecommendationProvider
{
    bool IsAvailable { get; }
    string? FallbackReason { get; }
    Task<List<(int DishId, float Score)>> GetPersonalizedAsync(int userId, int count, CancellationToken ct);
}
