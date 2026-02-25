using FluentAssertions;
using Smakosz.Application.Common.Interfaces;
using Smakosz.Application.Features.Admin.Commands.UnbanUser;
using Smakosz.UnitTests.Common.TestInfrastructure;
using Smakosz.UnitTests.Common.TestInfrastructure.EntityBuilders;

namespace Smakosz.UnitTests.Features.Admin.Commands.UnbanUser;

[Trait("Category", "Handlers")]
public class UnbanUserHandlerTests
{
    private readonly ISmakoszDbContext _db;
    private readonly MockDbSets _sets;
    private readonly ICurrentUserService _currentUser;
    private readonly UnbanUserHandler _handler;
    private static readonly Guid TestPublicId = Guid.NewGuid();

    public UnbanUserHandlerTests()
    {
        (_db, _sets) = DbContextMockFactory.Create();
        _currentUser = MockExtensions.CreateAdminUser(userId: 99, sessionId: 100);
        _handler = new UnbanUserHandler(_db, _currentUser);
    }

    [Fact]
    public async Task Handle_HappyPath_SetsBannedToFalse()
    {
        var user = new UserBuilder().WithId(5).WithPublicId(TestPublicId).AsBanned().Build();
        _sets.Users.Add(user);
        DbContextMockFactory.Refresh(_db, _sets);

        var result = await _handler.Handle(new UnbanUserCommand(TestPublicId), CancellationToken.None);

        result.IsError.Should().BeFalse();
        user.IsBanned.Should().BeFalse();
    }

    [Fact]
    public async Task Handle_NonAdmin_ReturnsForbidden()
    {
        var nonAdmin = MockExtensions.CreateAuthenticatedUser(userId: 1, role: "User");
        var handler = new UnbanUserHandler(_db, nonAdmin);

        var result = await handler.Handle(new UnbanUserCommand(TestPublicId), CancellationToken.None);

        result.IsError.Should().BeTrue();
        result.FirstError.Code.Should().Be("ADMIN_FORBIDDEN");
    }

    [Fact]
    public async Task Handle_UserNotFound_ReturnsNotFoundError()
    {
        var result = await _handler.Handle(new UnbanUserCommand(Guid.NewGuid()), CancellationToken.None);

        result.IsError.Should().BeTrue();
        result.FirstError.Code.Should().Be("USER_NOT_FOUND");
    }
}
