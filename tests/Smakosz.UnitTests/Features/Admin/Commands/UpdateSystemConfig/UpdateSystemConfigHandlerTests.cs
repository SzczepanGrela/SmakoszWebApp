using FluentAssertions;
using NSubstitute;
using Smakosz.Application.Common.Interfaces;
using Smakosz.Application.Features.Admin.Commands.UpdateSystemConfig;
using Smakosz.Domain.Entities.System;
using Smakosz.UnitTests.Common.TestInfrastructure;

namespace Smakosz.UnitTests.Features.Admin.Commands.UpdateSystemConfig;

[Trait("Category", "Handlers")]
public class UpdateSystemConfigHandlerTests
{
    private readonly ISmakoszDbContext _db;
    private readonly MockDbSets _sets;
    private readonly ICurrentUserService _currentUser;
    private readonly IDateTimeProvider _dateTime;
    private readonly UpdateSystemConfigHandler _handler;

    public UpdateSystemConfigHandlerTests()
    {
        (_db, _sets) = DbContextMockFactory.Create();
        _currentUser = MockExtensions.CreateAdminUser();
        _dateTime = Substitute.For<IDateTimeProvider>();
        _dateTime.UtcNow.Returns(new DateTime(2024, 1, 1, 0, 0, 0, DateTimeKind.Utc));
        _handler = new UpdateSystemConfigHandler(_db, _currentUser, _dateTime);
    }

    [Fact]
    public async Task Handle_NewConfig_CreatesAndAudits()
    {
        var result = await _handler.Handle(
            new UpdateSystemConfigCommand("app.name", "Smakosz"), CancellationToken.None);

        result.IsError.Should().BeFalse();
        _sets.SystemConfigs.Should().HaveCount(1);
        _sets.SystemConfigs[0].Key.Should().Be("app.name");
        _sets.SystemConfigs[0].Value.Should().Be("Smakosz");
        _sets.AuditLogs.Should().HaveCount(1);
    }

    [Fact]
    public async Task Handle_ExistingConfig_UpdatesValue()
    {
        _sets.SystemConfigs.Add(new SystemConfig { Key = "app.name", Value = "OldName" });
        DbContextMockFactory.Refresh(_db, _sets);

        var result = await _handler.Handle(
            new UpdateSystemConfigCommand("app.name", "NewName"), CancellationToken.None);

        result.IsError.Should().BeFalse();
        _sets.SystemConfigs[0].Value.Should().Be("NewName");
    }

    [Fact]
    public async Task Handle_NonAdmin_ReturnsForbidden()
    {
        var nonAdmin = MockExtensions.CreateAuthenticatedUser(userId: 1, role: "User");
        var handler = new UpdateSystemConfigHandler(_db, nonAdmin, _dateTime);

        var result = await handler.Handle(
            new UpdateSystemConfigCommand("key", "val"), CancellationToken.None);

        result.IsError.Should().BeTrue();
        result.FirstError.Code.Should().Be("ADMIN_FORBIDDEN");
    }
}
