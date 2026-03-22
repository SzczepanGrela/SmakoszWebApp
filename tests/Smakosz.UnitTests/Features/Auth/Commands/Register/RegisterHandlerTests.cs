using FluentAssertions;
using NSubstitute;
using Smakosz.Application.Common.Interfaces;
using Smakosz.Application.Features.Auth.Commands.Register;
using Smakosz.UnitTests.Common.TestInfrastructure;
using Smakosz.UnitTests.Common.TestInfrastructure.EntityBuilders;

namespace Smakosz.UnitTests.Features.Auth.Commands.Register;

[Trait("Category", "Handlers")]
public class RegisterHandlerTests
{
    private readonly ISmakoszDbContext _db;
    private readonly MockDbSets _sets;
    private readonly IPasswordHasher _passwordHasher;
    private readonly IVerificationCodeService _verificationCodeService;
    private readonly ICurrentUserService _currentUser;
    private readonly IEmailService _emailService;
    private readonly IForbiddenWordService _forbiddenWords;
    private readonly ITurnstileService _turnstile;
    private readonly RegisterHandler _handler;

    public RegisterHandlerTests()
    {
        (_db, _sets) = DbContextMockFactory.Create();
        _passwordHasher = Substitute.For<IPasswordHasher>();
        _verificationCodeService = Substitute.For<IVerificationCodeService>();
        _currentUser = Substitute.For<ICurrentUserService>();
        _emailService = Substitute.For<IEmailService>();
        _forbiddenWords = Substitute.For<IForbiddenWordService>();
        _turnstile = Substitute.For<ITurnstileService>();

        _passwordHasher.Hash(Arg.Any<string>()).Returns("hashed_password");
        _verificationCodeService.CreateCodeAsync(Arg.Any<int>(), Arg.Any<Domain.Enums.VerificationCodeType>(), Arg.Any<CancellationToken>())
            .Returns("123456");
        _turnstile.VerifyAsync(Arg.Any<string>(), Arg.Any<CancellationToken>()).Returns(true);
        _turnstile.VerifyAsync(string.Empty, Arg.Any<CancellationToken>()).Returns(false);

        _handler = new RegisterHandler(_db, _passwordHasher, _verificationCodeService, _currentUser, _emailService, _forbiddenWords, _turnstile);
    }

    [Fact]
    public async Task Handle_MissingTurnstileToken_ReturnsCaptchaFailed()
    {
        var command = new RegisterCommand("newuser", "new@example.com", "Password123");

        var result = await _handler.Handle(command, CancellationToken.None);

        result.IsError.Should().BeTrue();
        result.FirstError.Code.Should().Be("CAPTCHA_FAILED");
    }

    [Fact]
    public async Task Handle_InvalidTurnstileToken_ReturnsCaptchaFailed()
    {
        _turnstile.VerifyAsync(Arg.Any<string>(), Arg.Any<CancellationToken>()).Returns(false);
        var command = new RegisterCommand("newuser", "new@example.com", "Password123", "invalid-token");

        var result = await _handler.Handle(command, CancellationToken.None);

        result.IsError.Should().BeTrue();
        result.FirstError.Code.Should().Be("CAPTCHA_FAILED");
    }

    [Fact]
    public async Task Handle_ValidCommand_ReturnsSuccess()
    {
        var command = new RegisterCommand("newuser", "new@example.com", "Password123", "valid-token");

        var result = await _handler.Handle(command, CancellationToken.None);

        result.IsError.Should().BeFalse();
    }

    [Fact]
    public async Task Handle_EmailAlreadyExists_ReturnsError()
    {
        _sets.Users.Add(new UserBuilder().WithEmail("existing@example.com").Build());
        DbContextMockFactory.Refresh(_db, _sets);
        var command = new RegisterCommand("newuser", "existing@example.com", "Password123", "valid-token");

        var result = await _handler.Handle(command, CancellationToken.None);

        result.IsError.Should().BeTrue();
        result.FirstError.Code.Should().Be("AUTH_EMAIL_EXISTS");
    }

    [Fact]
    public async Task Handle_UsernameAlreadyExists_ReturnsError()
    {
        _sets.Users.Add(new UserBuilder().WithUsername("existinguser").Build());
        DbContextMockFactory.Refresh(_db, _sets);
        var command = new RegisterCommand("existinguser", "new@example.com", "Password123", "valid-token");

        var result = await _handler.Handle(command, CancellationToken.None);

        result.IsError.Should().BeTrue();
        result.FirstError.Code.Should().Be("AUTH_USERNAME_EXISTS");
    }

    [Fact]
    public async Task Handle_ValidCommand_HashesPasswordWithPasswordHasher()
    {
        var command = new RegisterCommand("newuser", "new@example.com", "Password123", "valid-token");

        await _handler.Handle(command, CancellationToken.None);

        _passwordHasher.Received(1).Hash("Password123");
    }

    [Fact]
    public async Task Handle_ValidCommand_CreatesVerificationCode()
    {
        var command = new RegisterCommand("newuser", "new@example.com", "Password123", "valid-token");

        await _handler.Handle(command, CancellationToken.None);

        await _verificationCodeService.Received(1).CreateCodeAsync(
            Arg.Any<int>(), Domain.Enums.VerificationCodeType.Register, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_ValidCommand_SendsVerificationEmail()
    {
        var command = new RegisterCommand("newuser", "new@example.com", "Password123", "valid-token");

        await _handler.Handle(command, CancellationToken.None);

        await _emailService.Received(1).SendVerificationCodeAsync(
            "new@example.com", Arg.Any<string>(), Arg.Any<CancellationToken>());
    }
}
