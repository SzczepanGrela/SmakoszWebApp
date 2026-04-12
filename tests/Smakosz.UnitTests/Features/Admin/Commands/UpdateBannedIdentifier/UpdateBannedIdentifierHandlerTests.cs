using FluentAssertions;
using Smakosz.Application.Common.Interfaces;
using Smakosz.Application.Features.Admin.Commands.UpdateBannedIdentifier;
using Smakosz.Domain.Entities.System;
using Smakosz.Domain.Enums;
using Smakosz.UnitTests.Common.TestInfrastructure;

namespace Smakosz.UnitTests.Features.Admin.Commands.UpdateBannedIdentifier;

[Trait("Category", "Handlers")]
public class UpdateBannedIdentifierHandlerTests
{
    private readonly ISmakoszDbContext _db;
    private readonly MockDbSets _sets;
    private readonly ICurrentUserService _currentUser;
    private readonly UpdateBannedIdentifierHandler _handler;

    public UpdateBannedIdentifierHandlerTests()
    {
        (_db, _sets) = DbContextMockFactory.Create();
        _currentUser = MockExtensions.CreateAdminUser();
        _handler = new UpdateBannedIdentifierHandler(_db, _currentUser);
    }

    [Fact]
    public async Task Handle_UpdatesReason()
    {
        _sets.BannedIdentifiers.Add(new BannedIdentifier { BanId = 1, Type = BannedIdentifierType.Ip, Value = "1.1.1.1", Reason = "Old" });
        DbContextMockFactory.Refresh(_db, _sets);

        var result = await _handler.Handle(new UpdateBannedIdentifierCommand(1, "New reason", null, false), CancellationToken.None);

        result.IsError.Should().BeFalse();
        _sets.BannedIdentifiers[0].Reason.Should().Be("New reason");
    }

    [Fact]
    public async Task Handle_UpdatesExpiresAt()
    {
        _sets.BannedIdentifiers.Add(new BannedIdentifier { BanId = 1, Type = BannedIdentifierType.Ip, Value = "1.1.1.1" });
        DbContextMockFactory.Refresh(_db, _sets);
        var newExpiry = DateTime.UtcNow.AddDays(30);

        var result = await _handler.Handle(new UpdateBannedIdentifierCommand(1, null, newExpiry, false), CancellationToken.None);

        result.IsError.Should().BeFalse();
        _sets.BannedIdentifiers[0].ExpiresAt.Should().Be(newExpiry);
    }

    [Fact]
    public async Task Handle_ClearExpiration_SetsNull()
    {
        _sets.BannedIdentifiers.Add(new BannedIdentifier { BanId = 1, Type = BannedIdentifierType.Ip, Value = "1.1.1.1", ExpiresAt = DateTime.UtcNow.AddDays(1) });
        DbContextMockFactory.Refresh(_db, _sets);

        var result = await _handler.Handle(new UpdateBannedIdentifierCommand(1, null, null, true), CancellationToken.None);

        result.IsError.Should().BeFalse();
        _sets.BannedIdentifiers[0].ExpiresAt.Should().BeNull();
    }

    [Fact]
    public async Task Handle_NotFound_ReturnsError()
    {
        DbContextMockFactory.Refresh(_db, _sets);

        var result = await _handler.Handle(new UpdateBannedIdentifierCommand(999, "x", null, false), CancellationToken.None);

        result.IsError.Should().BeTrue();
        result.FirstError.Code.Should().Be("BANNED_IDENTIFIER_NOT_FOUND");
    }

    [Fact]
    public async Task Handle_CreatesAuditLog()
    {
        _sets.BannedIdentifiers.Add(new BannedIdentifier { BanId = 1, Type = BannedIdentifierType.Ip, Value = "1.1.1.1" });
        DbContextMockFactory.Refresh(_db, _sets);

        await _handler.Handle(new UpdateBannedIdentifierCommand(1, "Updated", null, false), CancellationToken.None);

        _sets.AuditLogs.Should().ContainSingle();
        _sets.AuditLogs[0].Operation.Should().Be(AuditOperation.Update);
    }
}
