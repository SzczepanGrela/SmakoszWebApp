using FluentAssertions;
using NSubstitute;
using Smakosz.Application.Common.Interfaces;
using Smakosz.Application.Features.Me.Commands.DeleteAccount;
using Smakosz.Domain.Enums;
using Smakosz.UnitTests.Common.TestInfrastructure;
using Smakosz.UnitTests.Common.TestInfrastructure.EntityBuilders;

namespace Smakosz.UnitTests.Features.Me.Commands.DeleteAccount;

[Trait("Category", "Handlers")]
public class RequestAccountDeletionHandlerTests
{
    private readonly ISmakoszDbContext _db;
    private readonly MockDbSets _sets;
    private readonly ICurrentUserService _currentUser;
    private readonly IPasswordHasher _passwordHasher;
    private readonly IVerificationCodeService _verificationCodeService;
    private readonly IEmailService _emailService;
    private readonly RequestAccountDeletionHandler _handler;

    public RequestAccountDeletionHandlerTests()
    {
        (_db, _sets) = DbContextMockFactory.Create();
        _currentUser = MockExtensions.CreateAuthenticatedUser(userId: 1);
        _passwordHasher = Substitute.For<IPasswordHasher>();
        _verificationCodeService = Substitute.For<IVerificationCodeService>();
        _emailService = Substitute.For<IEmailService>();
        _handler = new RequestAccountDeletionHandler(_db, _currentUser, _passwordHasher, _verificationCodeService, _emailService);
    }

    [Fact]
    public async Task Handle_ValidPassword_CreatesCodeAndSendsEmail()
    {
        var user = new UserBuilder().WithId(1).WithEmail("test@example.com").WithPasswordHash("hash").Build();
        _sets.Users.Add(user);
        DbContextMockFactory.Refresh(_db, _sets);
        _passwordHasher.Verify("Password123!", "hash").Returns(true);
        _verificationCodeService.CreateCodeAsync(1, VerificationCodeType.AccountDeletion, Arg.Any<CancellationToken>())
            .Returns("123456");

        var result = await _handler.Handle(new RequestAccountDeletionCommand("Password123!"), CancellationToken.None);

        result.IsError.Should().BeFalse();
        await _verificationCodeService.Received(1).CreateCodeAsync(1, VerificationCodeType.AccountDeletion, Arg.Any<CancellationToken>());
        await _emailService.Received(1).SendAccountDeletionCodeAsync("test@example.com", "123456", Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_WrongPassword_ReturnsInvalidCredentials()
    {
        var user = new UserBuilder().WithId(1).WithPasswordHash("hash").Build();
        _sets.Users.Add(user);
        DbContextMockFactory.Refresh(_db, _sets);
        _passwordHasher.Verify("WrongPassword", "hash").Returns(false);

        var result = await _handler.Handle(new RequestAccountDeletionCommand("WrongPassword"), CancellationToken.None);

        result.IsError.Should().BeTrue();
        result.FirstError.Code.Should().Be("AUTH_INVALID_CREDENTIALS");
        await _emailService.DidNotReceive().SendAccountDeletionCodeAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_UserNotFound_ReturnsNotFound()
    {
        DbContextMockFactory.Refresh(_db, _sets);

        var result = await _handler.Handle(new RequestAccountDeletionCommand("Password123!"), CancellationToken.None);

        result.IsError.Should().BeTrue();
        result.FirstError.Code.Should().Be("USER_NOT_FOUND");
    }

    [Fact]
    public async Task Handle_RestaurantOwner_ReturnsForbidden()
    {
        var user = new UserBuilder().WithId(1).WithPasswordHash("hash").Build();
        _sets.Users.Add(user);
        _sets.Restaurants.Add(new Smakosz.Domain.Entities.Restaurant { RestaurantId = 1, OwnerId = 1, RestaurantName = "Test", Slug = "test", CreatedAt = DateTime.UtcNow });
        DbContextMockFactory.Refresh(_db, _sets);
        _passwordHasher.Verify("Password123!", "hash").Returns(true);

        var result = await _handler.Handle(new RequestAccountDeletionCommand("Password123!"), CancellationToken.None);

        result.IsError.Should().BeTrue();
        result.FirstError.Code.Should().Be("ACCOUNT_IS_RESTAURANT_OWNER");
    }

    [Fact]
    public async Task Handle_AdminRole_ReturnsForbidden()
    {
        var user = new UserBuilder().WithId(1).WithPasswordHash("hash").WithRole(UserRole.Admin).Build();
        _sets.Users.Add(user);
        DbContextMockFactory.Refresh(_db, _sets);
        _passwordHasher.Verify("Password123!", "hash").Returns(true);

        var result = await _handler.Handle(new RequestAccountDeletionCommand("Password123!"), CancellationToken.None);

        result.IsError.Should().BeTrue();
        result.FirstError.Code.Should().Be("ACCOUNT_ADMIN_CANNOT_DELETE_OWN");
        await _emailService.DidNotReceive().SendAccountDeletionCodeAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>());
    }
}
