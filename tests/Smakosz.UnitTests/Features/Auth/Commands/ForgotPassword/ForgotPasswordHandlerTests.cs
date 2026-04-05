using FluentAssertions;
using NSubstitute;
using Smakosz.Application.Common.Interfaces;
using Smakosz.Application.Features.Auth.Commands.ForgotPassword;
using Smakosz.UnitTests.Common.TestInfrastructure;
using Smakosz.UnitTests.Common.TestInfrastructure.EntityBuilders;

namespace Smakosz.UnitTests.Features.Auth.Commands.ForgotPassword;

[Trait("Category", "Handlers")]
public class ForgotPasswordHandlerTests
{
    private readonly ISmakoszDbContext _db;
    private readonly MockDbSets _sets;
    private readonly IEmailService _emailService;
    private readonly ICodeHasher _codeHasher;
    private readonly ITurnstileService _turnstile;
    private readonly ForgotPasswordHandler _handler;

    public ForgotPasswordHandlerTests()
    {
        (_db, _sets) = DbContextMockFactory.Create();
        _emailService = Substitute.For<IEmailService>();
        _codeHasher = Substitute.For<ICodeHasher>();
        _turnstile = Substitute.For<ITurnstileService>();
        _codeHasher.Hash(Arg.Any<string>()).Returns("hashed_code");
        _turnstile.VerifyAsync(Arg.Any<string>(), Arg.Any<CancellationToken>()).Returns(true);
        _handler = new ForgotPasswordHandler(_db, _emailService, _codeHasher, _turnstile);
    }

    [Fact]
    public async Task Handle_MissingTurnstileToken_ReturnsCaptchaFailed()
    {
        var command = new ForgotPasswordCommand("test@example.com");

        var result = await _handler.Handle(command, CancellationToken.None);

        result.IsError.Should().BeTrue();
        result.FirstError.Code.Should().Be("CAPTCHA_FAILED");
    }

    [Fact]
    public async Task Handle_InvalidTurnstileToken_ReturnsCaptchaFailed()
    {
        _turnstile.VerifyAsync(Arg.Any<string>(), Arg.Any<CancellationToken>()).Returns(false);
        var command = new ForgotPasswordCommand("test@example.com", "invalid-token");

        var result = await _handler.Handle(command, CancellationToken.None);

        result.IsError.Should().BeTrue();
        result.FirstError.Code.Should().Be("CAPTCHA_FAILED");
    }

    [Fact]
    public async Task Handle_ValidEmail_SendsResetCode()
    {
        var user = new UserBuilder().WithId(1).WithEmail("test@example.com").Build();
        _sets.Users.Add(user);
        DbContextMockFactory.Refresh(_db, _sets);
        var command = new ForgotPasswordCommand("test@example.com", "valid-token");

        var result = await _handler.Handle(command, CancellationToken.None);

        result.IsError.Should().BeFalse();
        await _emailService.Received(1).SendPasswordResetAsync(
            "test@example.com", Arg.Any<string>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_UserNotFound_ReturnsSilentSuccess()
    {
        var command = new ForgotPasswordCommand("nonexistent@example.com", "valid-token");

        var result = await _handler.Handle(command, CancellationToken.None);

        result.IsError.Should().BeFalse();
        await _emailService.DidNotReceive().SendPasswordResetAsync(
            Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>());
    }
}
