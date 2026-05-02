using FluentAssertions;
using NSubstitute;
using Smakosz.Application.Common.Interfaces;
using Smakosz.Application.Features.Admin.Commands.CreatePrivilegedAccount;
using Smakosz.Domain.Enums;
using Smakosz.UnitTests.Common.TestInfrastructure;
using Smakosz.UnitTests.Common.TestInfrastructure.EntityBuilders;

namespace Smakosz.UnitTests.Features.Admin.Commands.CreatePrivilegedAccount;

[Trait("Category", "Handlers")]
public class CreatePrivilegedAccountHandlerTests
{
    private readonly ISmakoszDbContext _db;
    private readonly MockDbSets _sets;
    private readonly ICurrentUserService _currentUser;
    private readonly IEmailService _emailService;
    private readonly IVerificationCodeService _verificationCodeService;
    private readonly CreatePrivilegedAccountHandler _handler;

    public CreatePrivilegedAccountHandlerTests()
    {
        (_db, _sets) = DbContextMockFactory.Create();
        _currentUser = MockExtensions.CreateAdminUser();
        _emailService = Substitute.For<IEmailService>();
        _verificationCodeService = Substitute.For<IVerificationCodeService>();
        _verificationCodeService
            .CreateCodeAsync(Arg.Any<int>(), Arg.Any<VerificationCodeType>(), Arg.Any<TimeSpan>(), Arg.Any<CancellationToken>())
            .Returns("inv-abc-123");
        _handler = new CreatePrivilegedAccountHandler(_db, _currentUser, _emailService, _verificationCodeService);
    }

    [Fact]
    public async Task Handle_AdminCreatesAdmin_PersistsUserSendsEmailAndWritesLogs()
    {
        var cmd = new CreatePrivilegedAccountCommand("new-admin@smakosz.test", "new-admin", UserRole.Admin);

        var result = await _handler.Handle(cmd, CancellationToken.None);

        result.IsError.Should().BeFalse();
        result.Value.Should().NotBe(Guid.Empty);

        _sets.Users.Should().ContainSingle(u =>
            u.Email == "new-admin@smakosz.test" &&
            u.Username == "new-admin" &&
            u.Role == UserRole.Admin &&
            u.EmailVerified &&
            u.PasswordHash == string.Empty);

        await _verificationCodeService.Received(1).CreateCodeAsync(
            Arg.Any<int>(), VerificationCodeType.Invitation, TimeSpan.FromHours(24), Arg.Any<CancellationToken>());
        await _emailService.Received(1).SendInvitationAsync(
            "new-admin@smakosz.test", "inv-abc-123", "new-admin", UserRole.Admin, Arg.Any<CancellationToken>());

        _sets.EmailLogs.Should().ContainSingle(l => l.Type == "Invitation" && l.Recipient == "new-admin@smakosz.test");
        _sets.SecurityLogs.Should().ContainSingle(l => l.EventType == SecurityEventType.AccountInvited);
        _sets.AuditLogs.Should().ContainSingle(l => l.TableName == "users" && l.Operation == AuditOperation.Insert);
    }

    [Fact]
    public async Task Handle_AdminCreatesModerator_PersistsWithModeratorRole()
    {
        var cmd = new CreatePrivilegedAccountCommand("mod@smakosz.test", "new-mod", UserRole.Moderator);

        var result = await _handler.Handle(cmd, CancellationToken.None);

        result.IsError.Should().BeFalse();
        _sets.Users.Should().ContainSingle(u => u.Role == UserRole.Moderator && u.Username == "new-mod");
    }

    [Fact]
    public async Task Handle_EmailAlreadyExists_ReturnsConflict()
    {
        _sets.Users.Add(new UserBuilder().WithId(10).WithEmail("taken@smakosz.test").Build());
        DbContextMockFactory.Refresh(_db, _sets);

        var result = await _handler.Handle(
            new CreatePrivilegedAccountCommand("taken@smakosz.test", "fresh-username", UserRole.Moderator),
            CancellationToken.None);

        result.IsError.Should().BeTrue();
        result.FirstError.Code.Should().Be("ADMIN_EMAIL_ALREADY_EXISTS");
    }

    [Fact]
    public async Task Handle_UsernameAlreadyExists_ReturnsConflict()
    {
        _sets.Users.Add(new UserBuilder().WithId(11).WithEmail("other@smakosz.test").WithUsername("taken-name").Build());
        DbContextMockFactory.Refresh(_db, _sets);

        var result = await _handler.Handle(
            new CreatePrivilegedAccountCommand("fresh@smakosz.test", "taken-name", UserRole.Moderator),
            CancellationToken.None);

        result.IsError.Should().BeTrue();
        result.FirstError.Code.Should().Be("ADMIN_USERNAME_ALREADY_EXISTS");
    }

    [Fact]
    public async Task Handle_NonAdmin_ReturnsForbidden()
    {
        var nonAdmin = MockExtensions.CreateAuthenticatedUser(userId: 5, role: "Moderator");
        var handler = new CreatePrivilegedAccountHandler(_db, nonAdmin, _emailService, _verificationCodeService);

        var result = await handler.Handle(
            new CreatePrivilegedAccountCommand("x@y.com", "user", UserRole.Admin),
            CancellationToken.None);

        result.IsError.Should().BeTrue();
        result.FirstError.Code.Should().Be("ADMIN_FORBIDDEN");
        await _emailService.DidNotReceive().SendInvitationAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>(), Arg.Any<UserRole>(), Arg.Any<CancellationToken>());
    }
}
