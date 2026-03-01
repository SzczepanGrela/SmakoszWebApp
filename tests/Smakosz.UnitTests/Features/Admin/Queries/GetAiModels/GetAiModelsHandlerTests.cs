using FluentAssertions;
using Smakosz.Application.Common.Interfaces;
using Smakosz.Application.Features.Admin.Queries.GetAiModels;
using Smakosz.Domain.Entities.System;
using Smakosz.UnitTests.Common.TestInfrastructure;

namespace Smakosz.UnitTests.Features.Admin.Queries.GetAiModels;

[Trait("Category", "Handlers")]
public class GetAiModelsHandlerTests
{
    private readonly ISmakoszDbContext _db;
    private readonly MockDbSets _sets;
    private readonly ICurrentUserService _currentUser;
    private readonly GetAiModelsHandler _handler;

    public GetAiModelsHandlerTests()
    {
        (_db, _sets) = DbContextMockFactory.Create();
        _currentUser = MockExtensions.CreateAdminUser();
        _handler = new GetAiModelsHandler(_db, _currentUser);
    }

    [Fact]
    public async Task Handle_ReturnsGroupedModels()
    {
        _sets.AiLogs.Add(new AiLog { LogId = 1, ModelType = "gpt-4", ModelVersion = "v1", CreatedAt = DateTime.UtcNow });
        _sets.AiLogs.Add(new AiLog { LogId = 2, ModelType = "gpt-4", ModelVersion = "v1", CreatedAt = DateTime.UtcNow });
        DbContextMockFactory.Refresh(_db, _sets);

        var result = await _handler.Handle(new GetAiModelsQuery(), CancellationToken.None);

        result.IsError.Should().BeFalse();
        result.Value.Should().HaveCount(1);
        result.Value[0].UsageCount.Should().Be(2);
    }

    [Fact]
    public async Task Handle_NonAdmin_ReturnsForbidden()
    {
        var nonAdmin = MockExtensions.CreateAuthenticatedUser(userId: 1, role: "User");
        var handler = new GetAiModelsHandler(_db, nonAdmin);

        var result = await handler.Handle(new GetAiModelsQuery(), CancellationToken.None);

        result.IsError.Should().BeTrue();
        result.FirstError.Code.Should().Be("ADMIN_FORBIDDEN");
    }
}
