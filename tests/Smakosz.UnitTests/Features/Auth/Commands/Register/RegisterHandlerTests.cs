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
    private readonly ICodeHasher _codeHasher;
    private readonly ICurrentUserService _currentUser;
    private readonly IEmailService _emailService;
    private readonly RegisterHandler _handler;

    public RegisterHandlerTests()
    {
        (_db, _sets) = DbContextMockFactory.Create();
        _passwordHasher = Substitute.For<IPasswordHasher>();
        _codeHasher = Substitute.For<ICodeHasher>();
        _currentUser = Substitute.For<ICurrentUserService>();
        _emailService = Substitute.For<IEmailService>();

        _passwordHasher.Hash(Arg.Any<string>()).Returns("hashed_password");
        _codeHasher.Hash(Arg.Any<string>()).Returns("hashed_code");

        _handler = new RegisterHandler(_db, _passwordHasher, _codeHasher, _currentUser, _emailService);
    }

    [Fact]
    public async Task Handle_ValidCommand_ReturnsSuccess()
    {
        var command = new RegisterCommand("newuser", "new@example.com", "Password123");

        var result = await _handler.Handle(command, CancellationToken.None);

        result.IsError.Should().BeFalse();
    }

    [Fact]
    public async Task Handle_EmailAlreadyExists_ReturnsError()
    {
        _sets.Users.Add(new UserBuilder().WithEmail("existing@example.com").Build());
        DbContextMockFactory.Refresh(_db, _sets);
        var command = new RegisterCommand("newuser", "existing@example.com", "Password123");

        var result = await _handler.Handle(command, CancellationToken.None);

        result.IsError.Should().BeTrue();
        result.FirstError.Code.Should().Be("AUTH_EMAIL_EXISTS");
    }

    [Fact]
    public async Task Handle_UsernameAlreadyExists_ReturnsError()
    {
        _sets.Users.Add(new UserBuilder().WithUsername("existinguser").Build());
        DbContextMockFactory.Refresh(_db, _sets);
        var command = new RegisterCommand("existinguser", "new@example.com", "Password123");

        var result = await _handler.Handle(command, CancellationToken.None);

        result.IsError.Should().BeTrue();
        result.FirstError.Code.Should().Be("AUTH_USERNAME_EXISTS");
    }

    [Fact]
    public async Task Handle_ValidCommand_HashesPasswordWithPasswordHasher()
    {
        var command = new RegisterCommand("newuser", "new@example.com", "Password123");

        await _handler.Handle(command, CancellationToken.None);

        _passwordHasher.Received(1).Hash("Password123");
    }

    [Fact]
    public async Task Handle_ValidCommand_HashesCodeWithCodeHasher()
    {
        var command = new RegisterCommand("newuser", "new@example.com", "Password123");

        await _handler.Handle(command, CancellationToken.None);

        // Assert - verification code is hashed with ICodeHasher, not IPasswordHasher
        _codeHasher.Received(1).Hash(Arg.Any<string>());
    }

    [Fact]
    public async Task Handle_ValidCommand_SendsVerificationEmail()
    {
        var command = new RegisterCommand("newuser", "new@example.com", "Password123");

        await _handler.Handle(command, CancellationToken.None);

        await _emailService.Received(1).SendVerificationCodeAsync(
            "new@example.com", Arg.Any<string>(), Arg.Any<CancellationToken>());
    }
}
