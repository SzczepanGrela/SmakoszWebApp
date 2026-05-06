using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using NSubstitute;
using Smakosz.Application.Common.Interfaces;
using Smakosz.Domain.Entities;
using Smakosz.Domain.Enums;
using Smakosz.Infrastructure.Persistence;
using Smakosz.Orchestrator.Jobs;
using Xunit;

namespace Smakosz.UnitTests.Orchestrator.Jobs;

[Trait("Category", "Builders")]
public class NcfTrainingDatasetBuilderTests : IDisposable
{
    private readonly SmakoszDbContext _db;
    private readonly IDateTimeProvider _clock;
    private readonly NcfTrainingDatasetBuilder _builder;
    private static readonly DateTime _now = new(2026, 5, 8, 12, 0, 0, DateTimeKind.Utc);

    public NcfTrainingDatasetBuilderTests()
    {
        var options = new DbContextOptionsBuilder<SmakoszDbContext>()
            .UseInMemoryDatabase($"NcfDataset_{Guid.NewGuid():N}")
            .Options;
        _db = new SmakoszDbContext(options);
        _clock = Substitute.For<IDateTimeProvider>();
        _clock.UtcNow.Returns(_now);
        _builder = new NcfTrainingDatasetBuilder(_db, _clock);
    }

    public void Dispose() => _db.Dispose();

    private void SeedUser(int userId, bool isDeleted = false)
    {
        _db.Users.Add(new User
        {
            UserId = userId,
            Email = $"u{userId}@test",
            PasswordHash = "x",
            SecurityStamp = "x",
            Slug = $"u-{userId}",
            IsDeleted = isDeleted
        });
    }

    private void SeedReview(int reviewId, int userId, int dishId, int rating,
        bool isVisible = true, bool isDeleted = false,
        ContentModerationStatus moderationStatus = ContentModerationStatus.Approved,
        DateTime? createdAt = null)
    {
        _db.Reviews.Add(new Review
        {
            ReviewId = reviewId,
            UserId = userId,
            DishId = dishId,
            DishRating = rating,
            IsVisible = isVisible,
            IsDeleted = isDeleted,
            ModerationStatus = moderationStatus,
            CreatedAt = createdAt ?? _now
        });
    }

    [Fact]
    public async Task NoReviews_ReturnsEmpty()
    {
        await _db.SaveChangesAsync();
        var result = await _builder.FetchSamplesAsync(0, CancellationToken.None);
        result.Should().BeEmpty();
    }

    [Fact]
    public async Task VisibleApprovedReview_IsIncluded()
    {
        SeedUser(1);
        SeedReview(1, userId: 1, dishId: 10, rating: 5);
        await _db.SaveChangesAsync();

        var result = await _builder.FetchSamplesAsync(0, CancellationToken.None);

        result.Should().HaveCount(1);
        result[0].Should().BeEquivalentTo(new NcfTrainingSample(1, 10, 5));
    }

    [Fact]
    public async Task RejectedReview_IsFiltered()
    {
        SeedUser(1);
        SeedReview(1, 1, 10, 5, moderationStatus: ContentModerationStatus.Rejected);
        await _db.SaveChangesAsync();

        var result = await _builder.FetchSamplesAsync(0, CancellationToken.None);
        result.Should().BeEmpty();
    }

    [Fact]
    public async Task DeletedReview_IsFiltered()
    {
        SeedUser(1);
        SeedReview(1, 1, 10, 5, isDeleted: true);
        await _db.SaveChangesAsync();

        var result = await _builder.FetchSamplesAsync(0, CancellationToken.None);
        result.Should().BeEmpty();
    }

    [Fact]
    public async Task HiddenReview_IsFiltered()
    {
        SeedUser(1);
        SeedReview(1, 1, 10, 5, isVisible: false);
        await _db.SaveChangesAsync();

        var result = await _builder.FetchSamplesAsync(0, CancellationToken.None);
        result.Should().BeEmpty();
    }

    [Fact]
    public async Task DeletedUserReview_IsFiltered()
    {
        SeedUser(1, isDeleted: true);
        SeedReview(1, 1, 10, 5);
        await _db.SaveChangesAsync();

        var result = await _builder.FetchSamplesAsync(0, CancellationToken.None);
        result.Should().BeEmpty();
    }

    [Fact]
    public async Task PendingAndNeedsReviewAndApproved_AreIncluded_OnlyRejectedExcluded()
    {
        SeedUser(1);
        SeedReview(1, 1, 10, 5, moderationStatus: ContentModerationStatus.Pending);
        SeedReview(2, 1, 11, 4, moderationStatus: ContentModerationStatus.NeedsReview);
        SeedReview(3, 1, 12, 3, moderationStatus: ContentModerationStatus.Approved);
        SeedReview(4, 1, 13, 2, moderationStatus: ContentModerationStatus.Rejected);
        await _db.SaveChangesAsync();

        var result = await _builder.FetchSamplesAsync(0, CancellationToken.None);
        result.Should().HaveCount(3);
        result.Select(s => s.DishId).Should().BeEquivalentTo(new[] { 10, 11, 12 });
    }

    [Fact]
    public async Task ReviewWindowDaysZero_ReturnsAllRegardlessOfAge()
    {
        SeedUser(1);
        SeedReview(1, 1, 10, 5, createdAt: _now.AddDays(-365));
        SeedReview(2, 1, 11, 4, createdAt: _now);
        await _db.SaveChangesAsync();

        var result = await _builder.FetchSamplesAsync(reviewWindowDays: 0, CancellationToken.None);
        result.Should().HaveCount(2);
    }

    [Fact]
    public async Task ReviewWindowDays30_FiltersOlderReviews()
    {
        SeedUser(1);
        SeedReview(1, 1, 10, 5, createdAt: _now.AddDays(-60));
        SeedReview(2, 1, 11, 4, createdAt: _now.AddDays(-10));
        await _db.SaveChangesAsync();

        var result = await _builder.FetchSamplesAsync(reviewWindowDays: 30, CancellationToken.None);
        result.Should().HaveCount(1);
        result[0].DishId.Should().Be(11);
    }
}
