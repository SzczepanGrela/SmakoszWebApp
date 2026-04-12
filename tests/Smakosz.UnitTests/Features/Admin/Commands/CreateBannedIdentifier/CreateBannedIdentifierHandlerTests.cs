using FluentAssertions;
using Smakosz.Application.Common.Interfaces;
using Smakosz.Application.Features.Admin.Commands.CreateBannedIdentifier;
using Smakosz.Domain.Entities.System;
using Smakosz.Domain.Enums;
using Smakosz.UnitTests.Common.TestInfrastructure;

namespace Smakosz.UnitTests.Features.Admin.Commands.CreateBannedIdentifier;

[Trait("Category", "Handlers")]
public class CreateBannedIdentifierHandlerTests
{
    private readonly ISmakoszDbContext _db;
    private readonly MockDbSets _sets;
    private readonly ICurrentUserService _currentUser;
    private readonly CreateBannedIdentifierHandler _handler;

    public CreateBannedIdentifierHandlerTests()
    {
        (_db, _sets) = DbContextMockFactory.Create();
        _currentUser = MockExtensions.CreateAdminUser();
        _handler = new CreateBannedIdentifierHandler(_db, _currentUser);
    }

    [Fact]
    public async Task Handle_CreatesBan()
    {
        DbContextMockFactory.Refresh(_db, _sets);

        var result = await _handler.Handle(
            new CreateBannedIdentifierCommand(BannedIdentifierType.Ip, "10.0.0.1", "Spam", null), CancellationToken.None);

        result.IsError.Should().BeFalse();
        _sets.BannedIdentifiers.Should().ContainSingle(b => b.Value == "10.0.0.1");
    }

    [Fact]
    public async Task Handle_Duplicate_ReturnsAlreadyExists()
    {
        _sets.BannedIdentifiers.Add(new BannedIdentifier { BanId = 1, Type = BannedIdentifierType.Ip, Value = "10.0.0.1" });
        DbContextMockFactory.Refresh(_db, _sets);

        var result = await _handler.Handle(
            new CreateBannedIdentifierCommand(BannedIdentifierType.Ip, "10.0.0.1", "Spam", null), CancellationToken.None);

        result.IsError.Should().BeTrue();
        result.FirstError.Code.Should().Be("BANNED_IDENTIFIER_ALREADY_EXISTS");
    }

    [Fact]
    public async Task Handle_NonAdmin_ReturnsForbidden()
    {
        var nonAdmin = MockExtensions.CreateAuthenticatedUser(userId: 1, role: "User");
        var handler = new CreateBannedIdentifierHandler(_db, nonAdmin);

        var result = await handler.Handle(
            new CreateBannedIdentifierCommand(BannedIdentifierType.Ip, "10.0.0.1", null, null), CancellationToken.None);

        result.IsError.Should().BeTrue();
        result.FirstError.Code.Should().Be("ADMIN_FORBIDDEN");
    }

    [Fact]
    public async Task Handle_CreatesAuditLog()
    {
        DbContextMockFactory.Refresh(_db, _sets);

        await _handler.Handle(
            new CreateBannedIdentifierCommand(BannedIdentifierType.Email, "spam@test.com", "Spam", null), CancellationToken.None);

        _sets.AuditLogs.Should().ContainSingle();
        _sets.AuditLogs[0].TableName.Should().Be("banned_identifiers");
        _sets.AuditLogs[0].Operation.Should().Be(AuditOperation.Insert);
    }

    [Fact]
    public async Task Handle_WithExpiresAt_SetsExpiration()
    {
        DbContextMockFactory.Refresh(_db, _sets);
        var expires = DateTime.UtcNow.AddDays(7);

        await _handler.Handle(
            new CreateBannedIdentifierCommand(BannedIdentifierType.Ip, "10.0.0.2", null, expires), CancellationToken.None);

        _sets.BannedIdentifiers[0].ExpiresAt.Should().Be(expires);
    }
}
