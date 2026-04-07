using FluentAssertions;
using NSubstitute;
using Smakosz.Application.Common.Interfaces;
using Smakosz.Application.Features.Auth.Commands.Resend2fa;
using Smakosz.UnitTests.Common.TestInfrastructure;
using Smakosz.UnitTests.Common.TestInfrastructure.EntityBuilders;

namespace Smakosz.UnitTests.Features.Auth.Commands.Resend2fa;

[Trait("Category", "Handlers")]
public class Resend2faHandlerTests
{
    private readonly ISmakoszDbContext _db;
    private readonly MockDbSets _sets;
    private readonly IEmailService _emailService;
    private readonly IVerificationCodeService _verificationCodeService;
    private readonly Resend2faHandler _handler;

    public Resend2faHandlerTests()
    {
        (_db, _sets) = DbContextMockFactory.Create();
        _emailService = Substitute.For<IEmailService>();
        _verificationCodeService = Substitute.For<IVerificationCodeService>();
        _verificationCodeService.CreateCodeAsync(Arg.Any<int>(), Arg.Any<Domain.Enums.VerificationCodeType>(), Arg.Any<CancellationToken>())
            .Returns("123456");
        _handler = new Resend2faHandler(_db, _emailService, _verificationCodeService);
    }

    [Fact]
    public async Task Handle_ValidUser_Sends2faCode()
    {
        var user = new UserBuilder().WithId(1).WithEmail("test@example.com").With2faEnabled().Build();
        _sets.Users.Add(user);
        DbContextMockFactory.Refresh(_db, _sets);
        var command = new Resend2faCommand("test@example.com");

        var result = await _handler.Handle(command, CancellationToken.None);

        result.IsError.Should().BeFalse();
        await _emailService.Received(1).Send2faCodeAsync(
            "test@example.com", Arg.Any<string>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_UserNotFound_ReturnsSilentSuccess()
    {
        var command = new Resend2faCommand("nonexistent@example.com");

        var result = await _handler.Handle(command, CancellationToken.None);

        result.IsError.Should().BeFalse();
        await _emailService.DidNotReceive().Send2faCodeAsync(
            Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_2faNotEnabled_ReturnsSilentSuccess()
    {
        var user = new UserBuilder().WithId(1).WithEmail("test@example.com").Build();
        _sets.Users.Add(user);
        DbContextMockFactory.Refresh(_db, _sets);
        var command = new Resend2faCommand("test@example.com");

        var result = await _handler.Handle(command, CancellationToken.None);

        result.IsError.Should().BeFalse();
        await _emailService.DidNotReceive().Send2faCodeAsync(
            Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>());
    }
}
