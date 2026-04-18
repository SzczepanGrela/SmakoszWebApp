using FluentAssertions;
using Smakosz.Application.Common.Interfaces;
using Smakosz.Application.Common.Models;
using Smakosz.Application.Features.Admin.Queries.GetModerationLogs;
using Smakosz.Domain.Entities;
using Smakosz.Domain.Entities.System;
using Smakosz.Domain.Enums;
using Smakosz.UnitTests.Common.TestInfrastructure;

namespace Smakosz.UnitTests.Features.Admin.Queries.GetModerationLogs;

[Trait("Category", "Handlers")]
public class GetModerationLogsHandlerTests
{
    private readonly ISmakoszDbContext _db;
    private readonly MockDbSets _sets;
    private readonly ICurrentUserService _currentUser;
    private readonly GetModerationLogsHandler _handler;

    public GetModerationLogsHandlerTests()
    {
        (_db, _sets) = DbContextMockFactory.Create();
        _currentUser = MockExtensions.CreateAdminUser();
        _handler = new GetModerationLogsHandler(_db, _currentUser);
    }

    [Fact]
    public async Task Handle_ReturnsPagedModerationLogs()
    {
        _sets.ModerationLogs.Add(new ModerationLog { LogId = 1, EntityType = ModerationEntityType.Review, EntityId = 10, Actor = ModerationActor.Ai, Verdict = ModerationVerdict.Rejected, CreatedAt = DateTime.UtcNow });
        _sets.ModerationLogs.Add(new ModerationLog { LogId = 2, EntityType = ModerationEntityType.Photo, EntityId = 20, Actor = ModerationActor.Admin, Verdict = ModerationVerdict.Approved, CreatedAt = DateTime.UtcNow.AddHours(-1) });
        DbContextMockFactory.Refresh(_db, _sets);

        var result = await _handler.Handle(
            new GetModerationLogsQuery(new PaginationParams(1, 20)), CancellationToken.None);

        result.IsError.Should().BeFalse();
        result.Value.Data.Should().HaveCount(2);
        result.Value.Data[0].LogId.Should().Be(1);
    }

    [Fact]
    public async Task Handle_WithActorFilter_ReturnsOnlyMatching()
    {
        _sets.ModerationLogs.Add(new ModerationLog { LogId = 1, EntityType = ModerationEntityType.Review, EntityId = 10, Actor = ModerationActor.Ai, Verdict = ModerationVerdict.Rejected, CreatedAt = DateTime.UtcNow });
        _sets.ModerationLogs.Add(new ModerationLog { LogId = 2, EntityType = ModerationEntityType.Review, EntityId = 11, Actor = ModerationActor.Admin, Verdict = ModerationVerdict.Approved, CreatedAt = DateTime.UtcNow.AddHours(-1) });
        DbContextMockFactory.Refresh(_db, _sets);

        var result = await _handler.Handle(
            new GetModerationLogsQuery(new PaginationParams(1, 20), Actor: "Ai"), CancellationToken.None);

        result.IsError.Should().BeFalse();
        result.Value.Data.Should().HaveCount(1);
        result.Value.Data[0].Actor.Should().Be("Ai");
    }

    [Fact]
    public async Task Handle_WithEntityTypeFilter_ReturnsOnlyMatching()
    {
        _sets.ModerationLogs.Add(new ModerationLog { LogId = 1, EntityType = ModerationEntityType.Review, EntityId = 10, Actor = ModerationActor.Admin, Verdict = ModerationVerdict.Approved, CreatedAt = DateTime.UtcNow });
        _sets.ModerationLogs.Add(new ModerationLog { LogId = 2, EntityType = ModerationEntityType.Photo, EntityId = 20, Actor = ModerationActor.Admin, Verdict = ModerationVerdict.Approved, CreatedAt = DateTime.UtcNow.AddHours(-1) });
        DbContextMockFactory.Refresh(_db, _sets);

        var result = await _handler.Handle(
            new GetModerationLogsQuery(new PaginationParams(1, 20), EntityType: "Photo"), CancellationToken.None);

        result.IsError.Should().BeFalse();
        result.Value.Data.Should().HaveCount(1);
        result.Value.Data[0].EntityType.Should().Be("Photo");
    }

    [Fact]
    public async Task Handle_ProjectsProcessedByUsername_WhenUserSet()
    {
        var moderator = new User { UserId = 5, Username = "moderator1", Email = "m@t.com" };
        _sets.Users.Add(moderator);
        _sets.ModerationLogs.Add(new ModerationLog
        {
            LogId = 1,
            EntityType = ModerationEntityType.Review,
            EntityId = 10,
            Actor = ModerationActor.Admin,
            Verdict = ModerationVerdict.Rejected,
            ProcessedBy = 5,
            ProcessedByUser = moderator,
            ReasonCodes = new List<string> { "spam", "toxic" },
            AiScores = "{\"toxicity\":0.9}",
            CreatedAt = DateTime.UtcNow
        });
        DbContextMockFactory.Refresh(_db, _sets);

        var result = await _handler.Handle(
            new GetModerationLogsQuery(new PaginationParams(1, 20)), CancellationToken.None);

        result.IsError.Should().BeFalse();
        var log = result.Value.Data.Single();
        log.ProcessedBy.Should().Be(5);
        log.ProcessedByUsername.Should().Be("moderator1");
        log.ReasonCodes.Should().BeEquivalentTo("spam", "toxic");
        log.AiScores.Should().Be("{\"toxicity\":0.9}");
    }

    [Fact]
    public async Task Handle_NonAdmin_ReturnsForbidden()
    {
        var nonAdmin = MockExtensions.CreateAuthenticatedUser(userId: 1, role: "User");
        var handler = new GetModerationLogsHandler(_db, nonAdmin);

        var result = await handler.Handle(
            new GetModerationLogsQuery(new PaginationParams(1, 20)), CancellationToken.None);

        result.IsError.Should().BeTrue();
        result.FirstError.Code.Should().Be("ADMIN_FORBIDDEN");
    }
}
