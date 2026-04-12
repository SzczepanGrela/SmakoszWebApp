using FluentAssertions;
using Smakosz.Application.Common.Interfaces;
using Smakosz.Application.Common.Models;
using Smakosz.Application.Features.Admin.Queries.GetBannedIdentifiers;
using Smakosz.Domain.Entities.System;
using Smakosz.Domain.Enums;
using Smakosz.UnitTests.Common.TestInfrastructure;

namespace Smakosz.UnitTests.Features.Admin.Queries.GetBannedIdentifiers;

[Trait("Category", "Handlers")]
public class GetBannedIdentifiersHandlerTests
{
    private readonly ISmakoszDbContext _db;
    private readonly MockDbSets _sets;
    private readonly ICurrentUserService _currentUser;
    private readonly GetBannedIdentifiersHandler _handler;

    public GetBannedIdentifiersHandlerTests()
    {
        (_db, _sets) = DbContextMockFactory.Create();
        _currentUser = MockExtensions.CreateAdminUser();
        _handler = new GetBannedIdentifiersHandler(_db, _currentUser);
    }

    [Fact]
    public async Task Handle_ReturnsActiveBansByDefault()
    {
        _sets.BannedIdentifiers.Add(new BannedIdentifier { BanId = 1, Type = BannedIdentifierType.Ip, Value = "1.2.3.4", BannedAt = DateTime.UtcNow });
        _sets.BannedIdentifiers.Add(new BannedIdentifier { BanId = 2, Type = BannedIdentifierType.Email, Value = "x@y.com", BannedAt = DateTime.UtcNow, ExpiresAt = DateTime.UtcNow.AddDays(-1) });
        DbContextMockFactory.Refresh(_db, _sets);

        var result = await _handler.Handle(new GetBannedIdentifiersQuery(new PaginationParams(1, 20)), CancellationToken.None);

        result.IsError.Should().BeFalse();
        result.Value.Data.Should().HaveCount(1);
        result.Value.Data[0].Value.Should().Be("1.2.3.4");
    }

    [Fact]
    public async Task Handle_IncludeExpired_ReturnsAll()
    {
        _sets.BannedIdentifiers.Add(new BannedIdentifier { BanId = 1, Type = BannedIdentifierType.Ip, Value = "1.2.3.4", BannedAt = DateTime.UtcNow });
        _sets.BannedIdentifiers.Add(new BannedIdentifier { BanId = 2, Type = BannedIdentifierType.Email, Value = "x@y.com", BannedAt = DateTime.UtcNow, ExpiresAt = DateTime.UtcNow.AddDays(-1) });
        DbContextMockFactory.Refresh(_db, _sets);

        var result = await _handler.Handle(new GetBannedIdentifiersQuery(new PaginationParams(1, 20), IncludeExpired: true), CancellationToken.None);

        result.IsError.Should().BeFalse();
        result.Value.Data.Should().HaveCount(2);
    }

    [Fact]
    public async Task Handle_FilterByType_FiltersResults()
    {
        _sets.BannedIdentifiers.Add(new BannedIdentifier { BanId = 1, Type = BannedIdentifierType.Ip, Value = "1.2.3.4", BannedAt = DateTime.UtcNow });
        _sets.BannedIdentifiers.Add(new BannedIdentifier { BanId = 2, Type = BannedIdentifierType.Email, Value = "x@y.com", BannedAt = DateTime.UtcNow });
        DbContextMockFactory.Refresh(_db, _sets);

        var result = await _handler.Handle(new GetBannedIdentifiersQuery(new PaginationParams(1, 20), Type: "Ip"), CancellationToken.None);

        result.IsError.Should().BeFalse();
        result.Value.Data.Should().HaveCount(1);
        result.Value.Data[0].Type.Should().Be("Ip");
    }

    [Fact]
    public async Task Handle_NonAdmin_ReturnsForbidden()
    {
        var nonAdmin = MockExtensions.CreateAuthenticatedUser(userId: 1, role: "User");
        var handler = new GetBannedIdentifiersHandler(_db, nonAdmin);

        var result = await handler.Handle(new GetBannedIdentifiersQuery(new PaginationParams(1, 20)), CancellationToken.None);

        result.IsError.Should().BeTrue();
        result.FirstError.Code.Should().Be("ADMIN_FORBIDDEN");
    }
}
