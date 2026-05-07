using Microsoft.EntityFrameworkCore;
using Smakosz.Application.Common.Interfaces;
using Smakosz.IntegrationTests.Infrastructure;

namespace Smakosz.IntegrationTests.Features.PublicEndpoints;

public class HomeEndpointsTests : IntegrationTestBase
{
    protected override Task SeedAsync() => Task.CompletedTask;

    [Fact]
    public async Task GetHome_Returns200()
    {
        var response = await AnonymousClient.GetAsync("/api/home");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task GetHome_WithCachedJsonNulled_HitsParallelCacheMissPath()
    {
        await Factory.SeedDataAsync(async db =>
        {
            await db.HomePageCaches.ExecuteUpdateAsync(setters => setters
                .SetProperty(c => c.TrendingRestaurantsJson, (string?)null)
                .SetProperty(c => c.TrendingDishesJson, (string?)null)
                .SetProperty(c => c.TopRatedDishesJson, (string?)null)
                .SetProperty(c => c.RecentReviewsJson, (string?)null)
                .SetProperty(c => c.PopularCategoriesJson, (string?)null)
                .SetProperty(c => c.HeroImageJson, (string?)null));
        });

        var factory = Factory.GetService<ISmakoszDbContextFactory>();
        factory.Should().NotBeNull("AddInfrastructureCore must register ISmakoszDbContextFactory for cache-miss parallel queries");

        var response = await AnonymousClient.GetAsync("/api/home");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }
}
