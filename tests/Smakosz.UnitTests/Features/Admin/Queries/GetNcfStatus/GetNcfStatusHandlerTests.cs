using FluentAssertions;
using NSubstitute;
using Smakosz.Application.Common.Interfaces;
using Smakosz.Application.Features.Admin.Queries.GetNcfStatus;
using Smakosz.Domain.Entities.System;
using Smakosz.Domain.Enums;
using Smakosz.UnitTests.Common.TestInfrastructure;

namespace Smakosz.UnitTests.Features.Admin.Queries.GetNcfStatus;

[Trait("Category", "Handlers")]
public class GetNcfStatusHandlerTests
{
    private readonly ISmakoszDbContext _db;
    private readonly MockDbSets _sets;
    private readonly IRecommendationProvider _provider;
    private readonly ICurrentUserService _admin;
    private readonly GetNcfStatusHandler _handler;

    public GetNcfStatusHandlerTests()
    {
        (_db, _sets) = DbContextMockFactory.Create();
        _provider = Substitute.For<IRecommendationProvider>();
        _admin = MockExtensions.CreateAdminUser();
        _handler = new GetNcfStatusHandler(_db, _provider, _admin);
    }

    [Fact]
    public async Task Handle_NonAdmin_ReturnsForbidden()
    {
        var nonAdmin = MockExtensions.CreateAuthenticatedUser(userId: 1, role: "User");
        var handler = new GetNcfStatusHandler(_db, _provider, nonAdmin);

        var result = await handler.Handle(new GetNcfStatusQuery(), CancellationToken.None);

        result.IsError.Should().BeTrue();
        result.FirstError.Code.Should().Be("ADMIN_FORBIDDEN");
    }

    [Fact]
    public async Task Handle_NcfUnavailable_ReturnsFallbackReasonAndZeroCounts()
    {
        _provider.IsAvailable.Returns(false);
        _provider.FallbackReason.Returns("Model NCF nie został jeszcze pobrany.");
        _provider.GetLoadedVersion().Returns(string.Empty);
        _provider.GetMappedUserIds().Returns([]);

        var result = await _handler.Handle(new GetNcfStatusQuery(), CancellationToken.None);

        result.IsError.Should().BeFalse();
        result.Value.NcfAvailable.Should().BeFalse();
        result.Value.FallbackReason.Should().Be("Model NCF nie został jeszcze pobrany.");
        result.Value.LoadedVersion.Should().BeEmpty();
        result.Value.MappedUsersCount.Should().Be(0);
        result.Value.CachePopulatedCount.Should().Be(0);
        result.Value.CachePopulatedPercent.Should().Be(0);
        result.Value.LastTraining.Should().BeNull();
        result.Value.LastCacheRegen.Should().BeNull();
    }

    [Fact]
    public async Task Handle_FullState_AggregatesFromAllSources()
    {
        const string version = "v20260513_004159";
        var t0 = new DateTime(2026, 5, 13, 12, 0, 0, DateTimeKind.Utc);

        _provider.IsAvailable.Returns(true);
        _provider.GetLoadedVersion().Returns(version);
        _provider.GetMappedUserIds().Returns(Enumerable.Range(1, 100).ToList());

        for (var i = 1; i <= 80; i++)
        {
            _sets.UserRecommendationCaches.Add(new UserRecommendationCache
            {
                UserId = i,
                ModelVersion = version,
                TopDishIdsJson = "[]",
                GeneratedAt = i == 1 ? t0 : t0.AddSeconds(60)
            });
        }

        _sets.SystemJobs.Add(new SystemJob
        {
            JobId = 10,
            Type = "ncf_training",
            Status = JobStatus.Completed,
            CreatedAt = t0.AddDays(-2),
            StartedAt = t0.AddDays(-2).AddSeconds(5),
            FinishedAt = t0.AddDays(-2).AddSeconds(605),
            WorkerNode = "gpu-homelab"
        });
        _sets.SystemJobs.Add(new SystemJob
        {
            JobId = 20,
            Type = "ncf_training",
            Status = JobStatus.Completed,
            CreatedAt = t0.AddDays(-1),
            StartedAt = t0.AddDays(-1).AddSeconds(5),
            FinishedAt = t0.AddDays(-1).AddSeconds(305),
            WorkerNode = "gpu-homelab"
        });

        DbContextMockFactory.Refresh(_db, _sets);

        var result = await _handler.Handle(new GetNcfStatusQuery(), CancellationToken.None);

        result.IsError.Should().BeFalse();
        result.Value.NcfAvailable.Should().BeTrue();
        result.Value.LoadedVersion.Should().Be(version);
        result.Value.MappedUsersCount.Should().Be(100);
        result.Value.CachePopulatedCount.Should().Be(80);
        result.Value.CachePopulatedPercent.Should().Be(80.0);
        result.Value.LastTraining!.JobId.Should().Be(20);
        result.Value.LastCacheRegen!.ApproxDurationSeconds.Should().Be(60);
        result.Value.RecentTrainings.Should().HaveCount(2);
    }

    [Fact]
    public async Task Handle_NoTrainingsYet_LastTrainingNull()
    {
        _provider.IsAvailable.Returns(true);
        _provider.GetLoadedVersion().Returns("v1");
        _provider.GetMappedUserIds().Returns([1, 2, 3]);

        DbContextMockFactory.Refresh(_db, _sets);

        var result = await _handler.Handle(new GetNcfStatusQuery(), CancellationToken.None);

        result.IsError.Should().BeFalse();
        result.Value.LastTraining.Should().BeNull();
        result.Value.RecentTrainings.Should().BeEmpty();
    }

    [Fact]
    public async Task Handle_CountsCacheOnlyForLoadedVersion()
    {
        _provider.IsAvailable.Returns(true);
        _provider.GetLoadedVersion().Returns("v2");
        _provider.GetMappedUserIds().Returns(Enumerable.Range(1, 30).ToList());

        for (var i = 1; i <= 50; i++)
            _sets.UserRecommendationCaches.Add(new UserRecommendationCache
            {
                UserId = i,
                ModelVersion = "v1",
                TopDishIdsJson = "[]",
                GeneratedAt = DateTime.UtcNow
            });
        for (var i = 51; i <= 70; i++)
            _sets.UserRecommendationCaches.Add(new UserRecommendationCache
            {
                UserId = i,
                ModelVersion = "v2",
                TopDishIdsJson = "[]",
                GeneratedAt = DateTime.UtcNow
            });
        DbContextMockFactory.Refresh(_db, _sets);

        var result = await _handler.Handle(new GetNcfStatusQuery(), CancellationToken.None);

        result.IsError.Should().BeFalse();
        result.Value.CachePopulatedCount.Should().Be(20);
    }
}
