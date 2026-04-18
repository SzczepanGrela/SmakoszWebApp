using FluentAssertions;
using Smakosz.Application.Common.Interfaces;
using Smakosz.Application.Common.Models;
using Smakosz.Application.Features.Admin.Queries.GetAiLogs;
using Smakosz.Domain.Entities.System;
using Smakosz.UnitTests.Common.TestInfrastructure;

namespace Smakosz.UnitTests.Features.Admin.Queries.GetAiLogs;

[Trait("Category", "Handlers")]
public class GetAiLogsHandlerTests
{
    private readonly ISmakoszDbContext _db;
    private readonly MockDbSets _sets;
    private readonly ICurrentUserService _currentUser;
    private readonly GetAiLogsHandler _handler;

    public GetAiLogsHandlerTests()
    {
        (_db, _sets) = DbContextMockFactory.Create();
        _currentUser = MockExtensions.CreateAdminUser();
        _handler = new GetAiLogsHandler(_db, _currentUser);
    }

    [Fact]
    public async Task Handle_ReturnsPagedAiLogs()
    {
        _sets.AiLogs.Add(new AiLog { LogId = 1, ModelType = "text_moderation", ModelName = "herbert", EntityType = "Review", EntityId = 10, Verdict = "Rejected", CreatedAt = DateTime.UtcNow });
        _sets.AiLogs.Add(new AiLog { LogId = 2, ModelType = "image_moderation", ModelName = "clip", EntityType = "Photo", EntityId = 20, Verdict = "Approved", Fallback = true, CreatedAt = DateTime.UtcNow.AddHours(-1) });
        _sets.AiLogs.Add(new AiLog { LogId = 3, ModelType = "ncf_training", ModelName = "ncf", CreatedAt = DateTime.UtcNow.AddHours(-2) });
        DbContextMockFactory.Refresh(_db, _sets);

        var result = await _handler.Handle(
            new GetAiLogsQuery(new PaginationParams(1, 20)), CancellationToken.None);

        result.IsError.Should().BeFalse();
        result.Value.Data.Should().HaveCount(3);
        result.Value.Data[0].LogId.Should().Be(1);
    }

    [Fact]
    public async Task Handle_WithModelTypeFilter_ReturnsOnlyMatching()
    {
        _sets.AiLogs.Add(new AiLog { LogId = 1, ModelType = "text_moderation", ModelName = "herbert", CreatedAt = DateTime.UtcNow });
        _sets.AiLogs.Add(new AiLog { LogId = 2, ModelType = "image_moderation", ModelName = "clip", CreatedAt = DateTime.UtcNow.AddHours(-1) });
        DbContextMockFactory.Refresh(_db, _sets);

        var result = await _handler.Handle(
            new GetAiLogsQuery(new PaginationParams(1, 20), ModelType: "text_moderation"), CancellationToken.None);

        result.IsError.Should().BeFalse();
        result.Value.Data.Should().HaveCount(1);
        result.Value.Data[0].ModelType.Should().Be("text_moderation");
    }

    [Fact]
    public async Task Handle_WithFallbackTrue_ReturnsOnlyFallback()
    {
        _sets.AiLogs.Add(new AiLog { LogId = 1, ModelType = "text_moderation", Fallback = false, CreatedAt = DateTime.UtcNow });
        _sets.AiLogs.Add(new AiLog { LogId = 2, ModelType = "text_moderation", Fallback = true, CreatedAt = DateTime.UtcNow.AddHours(-1) });
        _sets.AiLogs.Add(new AiLog { LogId = 3, ModelType = "image_moderation", Fallback = true, CreatedAt = DateTime.UtcNow.AddHours(-2) });
        DbContextMockFactory.Refresh(_db, _sets);

        var result = await _handler.Handle(
            new GetAiLogsQuery(new PaginationParams(1, 20), Fallback: true), CancellationToken.None);

        result.IsError.Should().BeFalse();
        result.Value.Data.Should().HaveCount(2);
        result.Value.Data.Should().OnlyContain(l => l.Fallback);
    }

    [Fact]
    public async Task Handle_WithFallbackFalse_ReturnsOnlyNonFallback()
    {
        _sets.AiLogs.Add(new AiLog { LogId = 1, ModelType = "text_moderation", Fallback = false, CreatedAt = DateTime.UtcNow });
        _sets.AiLogs.Add(new AiLog { LogId = 2, ModelType = "text_moderation", Fallback = true, CreatedAt = DateTime.UtcNow.AddHours(-1) });
        DbContextMockFactory.Refresh(_db, _sets);

        var result = await _handler.Handle(
            new GetAiLogsQuery(new PaginationParams(1, 20), Fallback: false), CancellationToken.None);

        result.IsError.Should().BeFalse();
        result.Value.Data.Should().HaveCount(1);
        result.Value.Data[0].Fallback.Should().BeFalse();
    }

    [Fact]
    public async Task Handle_NonAdmin_ReturnsForbidden()
    {
        var nonAdmin = MockExtensions.CreateAuthenticatedUser(userId: 1, role: "User");
        var handler = new GetAiLogsHandler(_db, nonAdmin);

        var result = await handler.Handle(
            new GetAiLogsQuery(new PaginationParams(1, 20)), CancellationToken.None);

        result.IsError.Should().BeTrue();
        result.FirstError.Code.Should().Be("ADMIN_FORBIDDEN");
    }
}
