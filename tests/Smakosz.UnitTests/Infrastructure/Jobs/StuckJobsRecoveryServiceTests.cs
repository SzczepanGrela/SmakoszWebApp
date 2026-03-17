using System.Text.Json;
using FluentAssertions;
using Microsoft.Extensions.Logging;
using NSubstitute;
using Smakosz.Application.Common.Interfaces;
using Smakosz.Domain.Entities;
using Smakosz.Domain.Entities.System;
using Smakosz.Domain.Enums;
using Smakosz.Orchestrator.Jobs;
using Smakosz.UnitTests.Common.TestInfrastructure;

namespace Smakosz.UnitTests.Infrastructure.Jobs;

[Trait("Category", "Handlers")]
public class StuckJobsRecoveryServiceTests
{
    private readonly ISmakoszDbContext _db;
    private readonly MockDbSets _sets;
    private readonly IDateTimeProvider _clock;
    private readonly StuckJobsRecoveryService _service;

    private static readonly DateTime Now = new(2026, 3, 15, 12, 0, 0, DateTimeKind.Utc);

    public StuckJobsRecoveryServiceTests()
    {
        (_db, _sets) = DbContextMockFactory.Create();
        _clock = Substitute.For<IDateTimeProvider>();
        _clock.UtcNow.Returns(Now);
        var logger = Substitute.For<ILogger<StuckJobsRecoveryService>>();
        _service = new StuckJobsRecoveryService(_db, _clock, logger);
    }

    [Fact]
    public async Task RecoverAsync_ZombiePendingOlderThan24h_AutoCancelled()
    {
        var zombieJob = new SystemJob
        {
            JobId = 1,
            Type = "ncf_training",
            Status = JobStatus.Pending,
            CreatedAt = Now.AddHours(-48)
        };
        _sets.SystemJobs.Add(zombieJob);
        DbContextMockFactory.Refresh(_db, _sets);

        await _service.RecoverAsync(CancellationToken.None);

        zombieJob.Status.Should().Be(JobStatus.Cancelled);
        zombieJob.ErrorMessage.Should().Contain("Auto-cancelled");
        zombieJob.FinishedAt.Should().Be(Now);
    }

    [Fact]
    public async Task RecoverAsync_RecentPendingJob_NotCancelled()
    {
        var recentJob = new SystemJob
        {
            JobId = 2,
            Type = "ncf_training",
            Status = JobStatus.Pending,
            CreatedAt = Now.AddHours(-2)
        };
        _sets.SystemJobs.Add(recentJob);
        DbContextMockFactory.Refresh(_db, _sets);

        await _service.RecoverAsync(CancellationToken.None);

        recentJob.Status.Should().Be(JobStatus.Pending);
        recentJob.FinishedAt.Should().BeNull();
    }

    [Fact]
    public async Task RecoverAsync_PendingExactlyAt24h_NotCancelled()
    {
        var boundaryJob = new SystemJob
        {
            JobId = 3,
            Type = "ncf_training",
            Status = JobStatus.Pending,
            CreatedAt = Now.AddHours(-24)
        };
        _sets.SystemJobs.Add(boundaryJob);
        DbContextMockFactory.Refresh(_db, _sets);

        await _service.RecoverAsync(CancellationToken.None);

        boundaryJob.Status.Should().Be(JobStatus.Pending);
        boundaryJob.FinishedAt.Should().BeNull();
    }

    [Fact]
    public async Task RecoverAsync_MixedStuckAndZombie_BothHandled()
    {
        var stuckProcessing = new SystemJob
        {
            JobId = 10,
            Type = "text_moderation",
            Status = JobStatus.Processing,
            StartedAt = Now.AddHours(-6),
            Attempts = 3,
            MaxAttempts = 3
        };
        var zombiePending = new SystemJob
        {
            JobId = 11,
            Type = "ncf_training",
            Status = JobStatus.Pending,
            CreatedAt = Now.AddHours(-30)
        };
        _sets.SystemJobs.Add(stuckProcessing);
        _sets.SystemJobs.Add(zombiePending);
        DbContextMockFactory.Refresh(_db, _sets);

        await _service.RecoverAsync(CancellationToken.None);

        stuckProcessing.Status.Should().Be(JobStatus.Failed);
        stuckProcessing.FinishedAt.Should().Be(Now);

        zombiePending.Status.Should().Be(JobStatus.Cancelled);
        zombiePending.ErrorMessage.Should().Contain("Auto-cancelled");
        zombiePending.FinishedAt.Should().Be(Now);
    }

    [Fact]
    public async Task RecoverAsync_StuckBatchJob_ResetsMenuSectionModerationStatus()
    {
        var menuSection = new MenuSection
        {
            SectionId = 42,
            RestaurantId = 1,
            SectionName = "Desery",
            ModerationStatus = ContentModerationStatus.Processing,
            CreatedAt = Now.AddHours(-5)
        };
        _sets.MenuSections.Add(menuSection);

        var payload = JsonSerializer.Serialize(new
        {
            items = new[]
            {
                new { entity_type = "menu_section", entity_id = 42, text = "Desery", language = "pl" }
            }
        });

        var stuckJob = new SystemJob
        {
            JobId = 20,
            Type = "text_moderation_batch",
            Status = JobStatus.Processing,
            StartedAt = Now.AddHours(-6),
            Attempts = 3,
            MaxAttempts = 3,
            Payload = payload
        };
        _sets.SystemJobs.Add(stuckJob);
        DbContextMockFactory.Refresh(_db, _sets);

        await _service.RecoverAsync(CancellationToken.None);

        stuckJob.Status.Should().Be(JobStatus.Failed);
        menuSection.ModerationStatus.Should().Be(ContentModerationStatus.Pending);
    }
}
