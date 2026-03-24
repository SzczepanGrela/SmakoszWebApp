using FluentAssertions;
using NSubstitute;
using Smakosz.Application.Common.Interfaces;
using Smakosz.Application.Features.Auth.Commands.ResendVerification;
using Smakosz.UnitTests.Common.TestInfrastructure;
using Smakosz.UnitTests.Common.TestInfrastructure.EntityBuilders;

namespace Smakosz.UnitTests.Features.Auth.Commands.ResendVerification;

[Trait("Category", "Handlers")]
public class ResendVerificationHandlerTests
{
    private readonly ISmakoszDbContext _db;
    private readonly MockDbSets _sets;
    private readonly IEmailService _emailService;
    private readonly IPasswordHasher _passwordHasher;
    private readonly ResendVerificationHandler _handler;

    public ResendVerificationHandlerTests()
    {
        (_db, _sets) = DbContextMockFactory.Create();
        _emailService = Substitute.For<IEmailService>();
        _passwordHasher = Substitute.For<IPasswordHasher>();
        _passwordHasher.Hash(Arg.Any<string>()).Returns("hashed_code");
        _handler = new ResendVerificationHandler(_db, _emailService, _passwordHasher);
    }

    [Fact]
    public async Task Handle_ValidEmail_SendsVerificationCode()
    {
        var user = new UserBuilder().WithId(1).WithEmail("test@example.com").AsEmailNotVerified().Build();
        _sets.Users.Add(user);
        DbContextMockFactory.Refresh(_db, _sets);
        var command = new ResendVerificationCommand("test@example.com");

        var result = await _handler.Handle(command, CancellationToken.None);

        result.IsError.Should().BeFalse();
        await _emailService.Received(1).SendVerificationCodeAsync(
            "test@example.com", Arg.Any<string>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_UserNotFound_ReturnsSilentSuccess()
    {
        // Arrange - security: don't reveal if email exists
        var command = new ResendVerificationCommand("nonexistent@example.com");

        var result = await _handler.Handle(command, CancellationToken.None);

        result.IsError.Should().BeFalse();
        await _emailService.DidNotReceive().SendVerificationCodeAsync(
            Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_AlreadyVerified_ReturnsSilentSuccess()
    {
        var user = new UserBuilder().WithId(1).WithEmail("test@example.com").Build();
        _sets.Users.Add(user);
        DbContextMockFactory.Refresh(_db, _sets);
        var command = new ResendVerificationCommand("test@example.com");

        var result = await _handler.Handle(command, CancellationToken.None);

        result.IsError.Should().BeFalse();
        await _emailService.DidNotReceive().SendVerificationCodeAsync(
            Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>());
    }
}
