using FluentAssertions;
using NSubstitute;
using Smakosz.Application.Common.Interfaces;
using Smakosz.Application.Features.Me.Commands.TwoFactor;
using Smakosz.Domain.Enums;
using Smakosz.UnitTests.Common.TestInfrastructure;
using Smakosz.UnitTests.Common.TestInfrastructure.EntityBuilders;

namespace Smakosz.UnitTests.Features.Me.Commands.TwoFactor;

[Trait("Category", "Handlers")]
public class Enable2faHandlerTests
{
    private readonly ISmakoszDbContext _db;
    private readonly MockDbSets _sets;
    private readonly ICurrentUserService _currentUser;
    private readonly IVerificationCodeService _verificationCodeService;
    private readonly IEmailService _emailService;
    private readonly Enable2faHandler _handler;

    public Enable2faHandlerTests()
    {
        (_db, _sets) = DbContextMockFactory.Create();
        _currentUser = MockExtensions.CreateAuthenticatedUser(userId: 1);
        _verificationCodeService = Substitute.For<IVerificationCodeService>();
        _emailService = Substitute.For<IEmailService>();
        _handler = new Enable2faHandler(_db, _currentUser, _verificationCodeService, _emailService);
    }

    [Fact]
    public async Task Handle_ValidUser_SendsCodeAndSucceeds()
    {
        var user = new UserBuilder().WithId(1).Build();
        _sets.Users.Add(user);
        DbContextMockFactory.Refresh(_db, _sets);
        _verificationCodeService
            .CreateCodeAsync(1, VerificationCodeType.TwoFactorAuth, Arg.Any<CancellationToken>())
            .Returns("123456");

        var result = await _handler.Handle(new Enable2faCommand(), CancellationToken.None);

        result.IsError.Should().BeFalse();
        await _emailService.Received(1)
            .Send2faCodeAsync(user.Email, "123456", Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_AlreadyEnabled_ReturnsError()
    {
        var user = new UserBuilder().WithId(1).With2faEnabled().Build();
        _sets.Users.Add(user);
        DbContextMockFactory.Refresh(_db, _sets);

        var result = await _handler.Handle(new Enable2faCommand(), CancellationToken.None);

        result.IsError.Should().BeTrue();
        result.FirstError.Code.Should().Be("AUTH_2FA_ALREADY_ENABLED");
    }

    [Fact]
    public async Task Handle_UserNotFound_ReturnsError()
    {
        var result = await _handler.Handle(new Enable2faCommand(), CancellationToken.None);

        result.IsError.Should().BeTrue();
        result.FirstError.Code.Should().Be("USER_NOT_FOUND");
    }

    [Fact]
    public async Task Handle_Unauthenticated_ReturnsError()
    {
        var anon = MockExtensions.CreateAnonymousUser();
        var handler = new Enable2faHandler(_db, anon, _verificationCodeService, _emailService);

        var result = await handler.Handle(new Enable2faCommand(), CancellationToken.None);

        result.IsError.Should().BeTrue();
        result.FirstError.Code.Should().Be("AUTH_INVALID_CREDENTIALS");
    }
}
