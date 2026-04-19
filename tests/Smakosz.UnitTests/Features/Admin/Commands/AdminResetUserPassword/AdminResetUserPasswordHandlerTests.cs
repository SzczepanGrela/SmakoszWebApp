using FluentAssertions;
using NSubstitute;
using Smakosz.Application.Common.Interfaces;
using Smakosz.Application.Features.Admin.Commands.AdminResetUserPassword;
using Smakosz.Domain.Enums;
using Smakosz.UnitTests.Common.TestInfrastructure;
using Smakosz.UnitTests.Common.TestInfrastructure.EntityBuilders;

namespace Smakosz.UnitTests.Features.Admin.Commands.AdminResetUserPassword;

[Trait("Category", "Handlers")]
public class AdminResetUserPasswordHandlerTests
{
    private readonly ISmakoszDbContext _db;
    private readonly MockDbSets _sets;
    private readonly ICurrentUserService _currentUser;
    private readonly IEmailService _emailService;
    private readonly IVerificationCodeService _verificationCodeService;
    private readonly AdminResetUserPasswordHandler _handler;

    public AdminResetUserPasswordHandlerTests()
    {
        (_db, _sets) = DbContextMockFactory.Create();
        _currentUser = MockExtensions.CreateAdminUser();
        _emailService = Substitute.For<IEmailService>();
        _verificationCodeService = Substitute.For<IVerificationCodeService>();
        _verificationCodeService
            .CreateCodeAsync(Arg.Any<int>(), Arg.Any<VerificationCodeType>(), Arg.Any<CancellationToken>())
            .Returns("abc123");
        _handler = new AdminResetUserPasswordHandler(_db, _currentUser, _emailService, _verificationCodeService);
    }

    [Fact]
    public async Task Handle_GeneratesCodeSendsEmailAndWritesLogs_WhenAdmin()
    {
        var publicId = Guid.NewGuid();
        var user = new UserBuilder().WithId(5).WithPublicId(publicId).WithEmail("user@test.com").Build();
        _sets.Users.Add(user);
        DbContextMockFactory.Refresh(_db, _sets);

        var result = await _handler.Handle(new AdminResetUserPasswordCommand(publicId), CancellationToken.None);

        result.IsError.Should().BeFalse();
        await _verificationCodeService.Received(1).CreateCodeAsync(5, VerificationCodeType.ResetPassword, Arg.Any<CancellationToken>());
        await _emailService.Received(1).SendPasswordResetAsync("user@test.com", "abc123", Arg.Any<CancellationToken>());
        _sets.EmailLogs.Should().ContainSingle(l => l.Type == "PasswordReset" && l.Recipient == "user@test.com");
        _sets.SecurityLogs.Should().ContainSingle(l => l.EventType == SecurityEventType.PasswordResetByAdmin && l.UserId == 5);
    }

    [Fact]
    public async Task Handle_UserNotFound_ReturnsError()
    {
        var result = await _handler.Handle(new AdminResetUserPasswordCommand(Guid.NewGuid()), CancellationToken.None);

        result.IsError.Should().BeTrue();
        result.FirstError.Code.Should().Be("USER_NOT_FOUND");
        await _emailService.DidNotReceive().SendPasswordResetAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_DeletedUser_ReturnsNotFound()
    {
        var publicId = Guid.NewGuid();
        var user = new UserBuilder().WithId(7).WithPublicId(publicId).WithEmail("deleted@test.com").Build();
        user.IsDeleted = true;
        _sets.Users.Add(user);
        DbContextMockFactory.Refresh(_db, _sets);

        var result = await _handler.Handle(new AdminResetUserPasswordCommand(publicId), CancellationToken.None);

        result.IsError.Should().BeTrue();
        result.FirstError.Code.Should().Be("USER_NOT_FOUND");
    }

    [Fact]
    public async Task Handle_NonAdmin_ReturnsForbidden()
    {
        var nonAdmin = MockExtensions.CreateAuthenticatedUser(userId: 1, role: "User");
        var handler = new AdminResetUserPasswordHandler(_db, nonAdmin, _emailService, _verificationCodeService);

        var result = await handler.Handle(new AdminResetUserPasswordCommand(Guid.NewGuid()), CancellationToken.None);

        result.IsError.Should().BeTrue();
        result.FirstError.Code.Should().Be("ADMIN_FORBIDDEN");
    }
}
