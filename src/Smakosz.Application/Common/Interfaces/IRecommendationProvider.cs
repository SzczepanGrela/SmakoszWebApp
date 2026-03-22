namespace Smakosz.Application.Common.Interfaces;

public interface IRecommendationProvider
{
    bool IsAvailable { get; }
    string? FallbackReason { get; }
    bool IsUserInMapping(int userId);
    Task<List<(int DishId, float Score)>> GetPersonalizedAsync(int userId, int count, CancellationToken ct);
}
