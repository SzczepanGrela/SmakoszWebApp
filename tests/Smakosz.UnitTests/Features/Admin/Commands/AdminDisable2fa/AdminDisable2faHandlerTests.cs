using FluentAssertions;
using Smakosz.Application.Common.Interfaces;
using Smakosz.Application.Features.Admin.Commands.AdminDisable2fa;
using Smakosz.Domain.Enums;
using Smakosz.UnitTests.Common.TestInfrastructure;
using Smakosz.UnitTests.Common.TestInfrastructure.EntityBuilders;

namespace Smakosz.UnitTests.Features.Admin.Commands.AdminDisable2fa;

[Trait("Category", "Handlers")]
public class AdminDisable2faHandlerTests
{
    private readonly ISmakoszDbContext _db;
    private readonly MockDbSets _sets;
    private readonly AdminDisable2faHandler _handler;
    private static readonly Guid TestPublicId = Guid.NewGuid();

    public AdminDisable2faHandlerTests()
    {
        (_db, _sets) = DbContextMockFactory.Create();
        var admin = MockExtensions.CreateAdminUser(userId: 99);
        _handler = new AdminDisable2faHandler(_db, admin);
    }

    [Fact]
    public async Task Handle_UserWith2fa_DisablesIt()
    {
        var user = new UserBuilder().WithId(5).WithPublicId(TestPublicId).With2faEnabled().Build();
        _sets.Users.Add(user);
        DbContextMockFactory.Refresh(_db, _sets);

        var result = await _handler.Handle(new AdminDisable2faCommand(TestPublicId), CancellationToken.None);

        result.IsError.Should().BeFalse();
        user.Is2faEnabled.Should().BeFalse();
    }

    [Fact]
    public async Task Handle_UserWith2fa_LogsSecurityEvent()
    {
        var user = new UserBuilder().WithId(5).WithPublicId(TestPublicId).With2faEnabled().Build();
        _sets.Users.Add(user);
        DbContextMockFactory.Refresh(_db, _sets);

        await _handler.Handle(new AdminDisable2faCommand(TestPublicId), CancellationToken.None);

        _sets.SecurityLogs.Should().ContainSingle(l =>
            l.EventType == SecurityEventType.TwoFactorDisabledByAdmin
            && l.Details!.Contains("99"));
    }

    [Fact]
    public async Task Handle_UserNotFound_ReturnsError()
    {
        var result = await _handler.Handle(new AdminDisable2faCommand(Guid.NewGuid()), CancellationToken.None);

        result.IsError.Should().BeTrue();
        result.FirstError.Code.Should().Be("USER_NOT_FOUND");
    }

    [Fact]
    public async Task Handle_2faNotEnabled_ReturnsError()
    {
        var user = new UserBuilder().WithId(5).WithPublicId(TestPublicId).Build();
        _sets.Users.Add(user);
        DbContextMockFactory.Refresh(_db, _sets);

        var result = await _handler.Handle(new AdminDisable2faCommand(TestPublicId), CancellationToken.None);

        result.IsError.Should().BeTrue();
        result.FirstError.Code.Should().Be("AUTH_2FA_NOT_ENABLED");
    }

    [Fact]
    public async Task Handle_NonAdmin_ReturnsForbidden()
    {
        var nonAdmin = MockExtensions.CreateAuthenticatedUser(userId: 1, role: "User");
        var handler = new AdminDisable2faHandler(_db, nonAdmin);

        var result = await handler.Handle(new AdminDisable2faCommand(TestPublicId), CancellationToken.None);

        result.IsError.Should().BeTrue();
        result.FirstError.Code.Should().Be("ADMIN_FORBIDDEN");
    }
}
