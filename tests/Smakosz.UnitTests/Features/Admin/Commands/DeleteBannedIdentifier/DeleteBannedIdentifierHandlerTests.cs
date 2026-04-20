using FluentAssertions;
using Smakosz.Application.Common.Interfaces;
using Smakosz.Application.Features.Admin.Commands.DeleteBannedIdentifier;
using Smakosz.Domain.Entities.System;
using Smakosz.Domain.Enums;
using Smakosz.UnitTests.Common.TestInfrastructure;

namespace Smakosz.UnitTests.Features.Admin.Commands.DeleteBannedIdentifier;

[Trait("Category", "Handlers")]
public class DeleteBannedIdentifierHandlerTests
{
    private readonly ISmakoszDbContext _db;
    private readonly MockDbSets _sets;
    private readonly ICurrentUserService _currentUser;
    private readonly DeleteBannedIdentifierHandler _handler;

    public DeleteBannedIdentifierHandlerTests()
    {
        (_db, _sets) = DbContextMockFactory.Create();
        _currentUser = MockExtensions.CreateAdminUser();
        _handler = new DeleteBannedIdentifierHandler(_db, _currentUser);
    }

    [Fact]
    public async Task Handle_DeletesBan()
    {
        _sets.BannedIdentifiers.Add(new BannedIdentifier { BanId = 1, Type = BannedIdentifierType.Ip, Value = "1.1.1.1" });
        DbContextMockFactory.Refresh(_db, _sets);

        var result = await _handler.Handle(new DeleteBannedIdentifierCommand(1), CancellationToken.None);

        result.IsError.Should().BeFalse();
    }

    [Fact]
    public async Task Handle_NotFound_ReturnsError()
    {
        DbContextMockFactory.Refresh(_db, _sets);

        var result = await _handler.Handle(new DeleteBannedIdentifierCommand(999), CancellationToken.None);

        result.IsError.Should().BeTrue();
        result.FirstError.Code.Should().Be("BANNED_IDENTIFIER_NOT_FOUND");
    }

    [Fact]
    public async Task Handle_NonAdmin_ReturnsForbidden()
    {
        var nonAdmin = MockExtensions.CreateAuthenticatedUser(userId: 1, role: "User");
        var handler = new DeleteBannedIdentifierHandler(_db, nonAdmin);

        var result = await handler.Handle(new DeleteBannedIdentifierCommand(1), CancellationToken.None);

        result.IsError.Should().BeTrue();
        result.FirstError.Code.Should().Be("ADMIN_FORBIDDEN");
    }

    [Fact]
    public async Task Handle_CreatesAuditLog()
    {
        _sets.BannedIdentifiers.Add(new BannedIdentifier { BanId = 1, Type = BannedIdentifierType.Ip, Value = "1.1.1.1" });
        DbContextMockFactory.Refresh(_db, _sets);

        await _handler.Handle(new DeleteBannedIdentifierCommand(1), CancellationToken.None);

        _sets.AuditLogs.Should().ContainSingle();
        _sets.AuditLogs[0].Operation.Should().Be(AuditOperation.Delete);
    }
}
