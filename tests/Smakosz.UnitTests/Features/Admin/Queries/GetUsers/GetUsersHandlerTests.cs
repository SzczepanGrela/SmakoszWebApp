using FluentAssertions;
using Smakosz.Application.Common.Interfaces;
using Smakosz.Application.Common.Models;
using Smakosz.Application.Features.Admin.Queries.GetUsers;
using Smakosz.Domain.Enums;
using Smakosz.UnitTests.Common.TestInfrastructure;
using Smakosz.UnitTests.Common.TestInfrastructure.EntityBuilders;

namespace Smakosz.UnitTests.Features.Admin.Queries.GetUsers;

[Trait("Category", "Handlers")]
public class GetUsersHandlerTests
{
    private readonly ISmakoszDbContext _db;
    private readonly MockDbSets _sets;
    private readonly ICurrentUserService _currentUser;
    private readonly GetUsersHandler _handler;

    public GetUsersHandlerTests()
    {
        (_db, _sets) = DbContextMockFactory.Create();
        _currentUser = MockExtensions.CreateAdminUser(userId: 99, sessionId: 100);
        _handler = new GetUsersHandler(_db, _currentUser);
    }

    [Fact]
    public async Task Handle_HappyPath_ReturnsPaginatedUsers()
    {
        _sets.Users.Add(new UserBuilder().WithId(1).WithUsername("alice").WithRole(UserRole.User).Build());
        _sets.Users.Add(new UserBuilder().WithId(2).WithUsername("bob").WithRole(UserRole.User).Build());
        DbContextMockFactory.Refresh(_db, _sets);

        var result = await _handler.Handle(
            new GetUsersQuery(new PaginationParams(1, 20)),
            CancellationToken.None);

        result.IsError.Should().BeFalse();
        result.Value.Data.Should().HaveCount(2);
        result.Value.Pagination.TotalCount.Should().Be(2);
    }

    [Fact]
    public async Task Handle_SearchFilter_ReturnsMatchingUsers()
    {
        _sets.Users.Add(new UserBuilder().WithId(1).WithUsername("alice").Build());
        _sets.Users.Add(new UserBuilder().WithId(2).WithUsername("bob").Build());
        DbContextMockFactory.Refresh(_db, _sets);

        var result = await _handler.Handle(
            new GetUsersQuery(new PaginationParams(1, 20), Search: "alice"),
            CancellationToken.None);

        result.IsError.Should().BeFalse();
        result.Value.Data.Should().HaveCount(1);
        result.Value.Data[0].Username.Should().Be("alice");
    }

    [Fact]
    public async Task Handle_NonAdmin_ReturnsForbidden()
    {
        var nonAdmin = MockExtensions.CreateAuthenticatedUser(userId: 1, role: "User");
        var handler = new GetUsersHandler(_db, nonAdmin);

        var result = await handler.Handle(
            new GetUsersQuery(new PaginationParams(1, 20)),
            CancellationToken.None);

        result.IsError.Should().BeTrue();
        result.FirstError.Code.Should().Be("ADMIN_FORBIDDEN");
    }
}
