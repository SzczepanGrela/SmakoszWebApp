using Smakosz.Application.Common.Interfaces;

namespace Smakosz.IntegrationTests.Infrastructure.Stubs;

public class StubRecommendationProvider : IRecommendationProvider
{
    public bool IsAvailable => false;
    public bool IsUserInMapping(int userId) => false;

    public string? FallbackReason => "NCF unavailable in test environment.";

    public Task<List<(int DishId, float Score)>> GetPersonalizedAsync(
        int userId, int count, CancellationToken ct)
        => Task.FromResult(new List<(int, float)>());
}
