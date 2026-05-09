using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using Smakosz.Application.Common.Interfaces;
using Smakosz.Domain.Entities.System;
using Smakosz.Infrastructure.Services;
using Smakosz.UnitTests.Common.TestInfrastructure;
using Smakosz.UnitTests.Common.TestInfrastructure.EntityBuilders;

namespace Smakosz.UnitTests.Infrastructure.Services;

[Trait("Category", "Services")]
public class UserRecommendationCacheRegenerationServiceTests
{
    private const string Version = "v20260513_120000";

    private readonly ISmakoszDbContext _db;
    private readonly MockDbSets _sets;
    private readonly IRecommendationProvider _provider;
    private readonly UserRecommendationCacheRegenerationService _sut;

    public UserRecommendationCacheRegenerationServiceTests()
    {
        (_db, _sets) = DbContextMockFactory.Create();
        _provider = Substitute.For<IRecommendationProvider>();
        _sut = new UserRecommendationCacheRegenerationService(_db, _provider, NullLogger<UserRecommendationCacheRegenerationService>.Instance);
    }

    [Fact]
    public async Task Regenerate_ProviderUnavailable_NoOp()
    {
        _provider.IsAvailable.Returns(false);

        await _sut.RegenerateAsync(Version, CancellationToken.None);

        _sets.UserRecommendationCaches.Should().BeEmpty();
    }

    [Fact]
    public async Task Regenerate_VersionMismatch_Skips()
    {
        _provider.IsAvailable.Returns(true);
        _provider.GetLoadedVersion().Returns("v20260513_999999");

        await _sut.RegenerateAsync(Version, CancellationToken.None);

        _sets.UserRecommendationCaches.Should().BeEmpty();
    }

    [Fact]
    public async Task Regenerate_AlreadyPopulated_SkipsWithoutInference()
    {
        _provider.IsAvailable.Returns(true);
        _provider.GetLoadedVersion().Returns(Version);

        _sets.UserRecommendationCaches.Add(new UserRecommendationCache
        {
            UserId = 1,
            ModelVersion = Version,
            TopDishIdsJson = "[]",
            GeneratedAt = DateTime.UtcNow
        });
        DbContextMockFactory.Refresh(_db, _sets);

        await _sut.RegenerateAsync(Version, CancellationToken.None);

        _ = _provider.DidNotReceive().GetMappedUserIds();
    }

    [Fact]
    public async Task Regenerate_FreshRun_InsertsRowsForAllMappedUsers()
    {
        _provider.IsAvailable.Returns(true);
        _provider.GetLoadedVersion().Returns(Version);
        _provider.GetMappedUserIds().Returns(new[] { 1, 2, 3 });
        _provider.GetPersonalizedAsync(Arg.Any<int>(), Arg.Any<int>(), Arg.Any<CancellationToken>())
            .Returns(new List<(int DishId, float Score)>
            {
                (10, 0.9f),
                (20, 0.8f)
            });

        DbContextMockFactory.Refresh(_db, _sets);

        await _sut.RegenerateAsync(Version, CancellationToken.None);

        _sets.UserRecommendationCaches.Should().HaveCount(3);
        _sets.UserRecommendationCaches.Should().AllSatisfy(c =>
        {
            c.ModelVersion.Should().Be(Version);
            c.TopDishIdsJson.Should().Contain("\"dishId\":10");
        });
    }

    [Fact]
    public async Task Regenerate_FiltersReviewedDishesPerUser()
    {
        _provider.IsAvailable.Returns(true);
        _provider.GetLoadedVersion().Returns(Version);
        _provider.GetMappedUserIds().Returns(new[] { 1 });
        _provider.GetPersonalizedAsync(1, Arg.Any<int>(), Arg.Any<CancellationToken>())
            .Returns(new List<(int DishId, float Score)>
            {
                (10, 0.9f),
                (20, 0.8f),
                (30, 0.7f)
            });

        _sets.Reviews.Add(new ReviewBuilder().WithId(1).WithUserId(1).WithDishId(20).Build());
        DbContextMockFactory.Refresh(_db, _sets);

        await _sut.RegenerateAsync(Version, CancellationToken.None);

        _sets.UserRecommendationCaches.Should().HaveCount(1);
        var row = _sets.UserRecommendationCaches.First();
        row.TopDishIdsJson.Should().NotContain("\"dishId\":20");
        row.TopDishIdsJson.Should().Contain("\"dishId\":10");
        row.TopDishIdsJson.Should().Contain("\"dishId\":30");
    }
}
