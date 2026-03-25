using ErrorOr;
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
    private readonly IJwtTokenService _jwtTokenService;
    private readonly ICurrentUserService _currentUser;
    private readonly RegisterHandler _handler;

    public RegisterHandlerTests()
    {
        (_db, _sets) = DbContextMockFactory.Create();
        _passwordHasher = Substitute.For<IPasswordHasher>();
        _jwtTokenService = Substitute.For<IJwtTokenService>();
        _currentUser = Substitute.For<ICurrentUserService>();

        _passwordHasher.Hash(Arg.Any<string>()).Returns("hashed_password");
        _jwtTokenService.GenerateAccessToken(Arg.Any<Smakosz.Domain.Entities.User>()).Returns("access_token");
        _jwtTokenService.GenerateRefreshToken().Returns("refresh_token");

        _handler = new RegisterHandler(_db, _passwordHasher, _jwtTokenService, _currentUser);
    }

    [Fact]
    public async Task Handle_ValidCommand_ReturnsAuthResult()
    {
        var command = new RegisterCommand("newuser", "new@example.com", "Password123");

        var result = await _handler.Handle(command, CancellationToken.None);

        result.IsError.Should().BeFalse();
        result.Value.AccessToken.Should().Be("access_token");
        result.Value.RefreshToken.Should().Be("refresh_token");
        result.Value.User.Username.Should().Be("newuser");
        result.Value.User.Email.Should().Be("new@example.com");
        result.Value.User.Role.Should().Be("User");
        result.Value.User.EmailVerified.Should().BeFalse();
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
    public async Task Handle_EmailStoredLowercase_NormalizesEmail()
    {
        var command = new RegisterCommand("newuser", "Test@Example.COM", "Password123");

        var result = await _handler.Handle(command, CancellationToken.None);

        result.IsError.Should().BeFalse();
        result.Value.User.Email.Should().Be("test@example.com");
    }

    [Fact]
    public async Task Handle_ValidCommand_HashesPassword()
    {
        var command = new RegisterCommand("newuser", "new@example.com", "Password123");

        await _handler.Handle(command, CancellationToken.None);

        _passwordHasher.Received(1).Hash("Password123");
    }

    [Fact]
    public async Task Handle_ValidCommand_SavesChangestwice()
    {
        var command = new RegisterCommand("newuser", "new@example.com", "Password123");

        await _handler.Handle(command, CancellationToken.None);

        // Assert - first SaveChanges for User, second for UserSession
        await _db.Received(2).SaveChangesAsync(Arg.Any<CancellationToken>());
    }
}
