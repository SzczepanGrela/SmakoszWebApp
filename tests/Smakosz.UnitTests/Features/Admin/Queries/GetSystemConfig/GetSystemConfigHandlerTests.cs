using FluentAssertions;
using Smakosz.Application.Common.Interfaces;
using Smakosz.Application.Features.Admin.Queries.GetSystemConfig;
using Smakosz.Domain.Entities.System;
using Smakosz.UnitTests.Common.TestInfrastructure;

namespace Smakosz.UnitTests.Features.Admin.Queries.GetSystemConfig;

[Trait("Category", "Handlers")]
public class GetSystemConfigHandlerTests
{
    private readonly ISmakoszDbContext _db;
    private readonly MockDbSets _sets;
    private readonly ICurrentUserService _currentUser;
    private readonly GetSystemConfigHandler _handler;

    public GetSystemConfigHandlerTests()
    {
        (_db, _sets) = DbContextMockFactory.Create();
        _currentUser = MockExtensions.CreateAdminUser();
        _handler = new GetSystemConfigHandler(_db, _currentUser);
    }

    [Fact]
    public async Task Handle_ReturnsConfigs()
    {
        _sets.SystemConfigs.Add(new SystemConfig { Key = "app.name", Value = "Smakosz" });
        DbContextMockFactory.Refresh(_db, _sets);

        var result = await _handler.Handle(new GetSystemConfigQuery(), CancellationToken.None);

        result.IsError.Should().BeFalse();
        result.Value.Should().HaveCount(1);
        result.Value[0].Key.Should().Be("app.name");
    }

    [Fact]
    public async Task Handle_SecretValues_AreMasked()
    {
        _sets.SystemConfigs.Add(new SystemConfig { Key = "api.key", Value = "secret123", IsSecret = true });
        DbContextMockFactory.Refresh(_db, _sets);

        var result = await _handler.Handle(new GetSystemConfigQuery(), CancellationToken.None);

        result.IsError.Should().BeFalse();
        result.Value[0].Value.Should().Be("***");
    }

    [Fact]
    public async Task Handle_NonAdmin_ReturnsForbidden()
    {
        var nonAdmin = MockExtensions.CreateAuthenticatedUser(userId: 1, role: "User");
        var handler = new GetSystemConfigHandler(_db, nonAdmin);

        var result = await handler.Handle(new GetSystemConfigQuery(), CancellationToken.None);

        result.IsError.Should().BeTrue();
        result.FirstError.Code.Should().Be("ADMIN_FORBIDDEN");
    }
}
