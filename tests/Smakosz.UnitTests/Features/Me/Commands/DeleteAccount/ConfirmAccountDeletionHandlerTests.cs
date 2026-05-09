using FluentAssertions;
using NSubstitute;
using Smakosz.Application.Common.Interfaces;
using Smakosz.Application.Features.Me.Commands.DeleteAccount;
using Smakosz.Domain.Entities;
using Smakosz.Domain.Entities.System;
using Smakosz.Domain.Enums;
using Smakosz.UnitTests.Common.TestInfrastructure;
using Smakosz.UnitTests.Common.TestInfrastructure.EntityBuilders;

namespace Smakosz.UnitTests.Features.Me.Commands.DeleteAccount;

[Trait("Category", "Handlers")]
public class ConfirmAccountDeletionHandlerTests
{
    private readonly ISmakoszDbContext _db;
    private readonly MockDbSets _sets;
    private readonly ICurrentUserService _currentUser;
    private readonly ICodeHasher _codeHasher;
    private readonly IDateTimeProvider _clock;
    private readonly IEmailService _emailService;
    private readonly ConfirmAccountDeletionHandler _handler;

    public ConfirmAccountDeletionHandlerTests()
    {
        (_db, _sets) = DbContextMockFactory.Create();
        _currentUser = MockExtensions.CreateAuthenticatedUser(userId: 1);
        _codeHasher = Substitute.For<ICodeHasher>();
        _clock = Substitute.For<IDateTimeProvider>();
        _clock.UtcNow.Returns(new DateTime(2026, 3, 23, 12, 0, 0, DateTimeKind.Utc));
        _emailService = Substitute.For<IEmailService>();
        _handler = new ConfirmAccountDeletionHandler(_db, _currentUser, _codeHasher, _clock, _emailService);
    }

    [Fact]
    public async Task Handle_ValidCode_SoftDeletesAndAnonymizesUser()
    {
        var user = new UserBuilder()
            .WithId(1)
            .WithEmail("test@example.com")
            .WithUsername("testuser")
            .WithPasswordHash("hash")
            .Build();
        user.FirstName = "Jan";
        user.LastName = "Kowalski";
        user.FullName = "Jan Kowalski";

        var code = new VerificationCodeBuilder()
            .WithUserId(1)
            .WithCode("hashed_code")
            .WithType(VerificationCodeType.AccountDeletion)
            .WithExpiresAt(DateTime.UtcNow.AddMinutes(15))
            .Build();

        _sets.Users.Add(user);
        _sets.VerificationCodes.Add(code);
        _sets.SystemConfigs.Add(new SystemConfig { Key = "auth.verify_code_max_attempts", Value = "3" });
        DbContextMockFactory.Refresh(_db, _sets);
        _codeHasher.Verify("123456", "hashed_code").Returns(true);

        var result = await _handler.Handle(new ConfirmAccountDeletionCommand("123456"), CancellationToken.None);

        result.IsError.Should().BeFalse();
        user.IsDeleted.Should().BeTrue();
        user.IsActive.Should().BeFalse();
        user.Username.Should().StartWith("usuniety_");
        user.Email.Should().HaveLength(64);
        user.FirstName.Should().BeNull();
        user.LastName.Should().BeNull();
        user.FullName.Should().BeNull();
        user.PasswordHash.Should().BeEmpty();
        _sets.SecurityLogs.Should().Contain(sl => sl.EventType == SecurityEventType.AccountDeleted);
        _sets.AuditLogs.Should().Contain(al => al.TableName == "Users" && al.Operation == AuditOperation.Delete);
        await _emailService.Received(1).SendAccountDeletionConfirmationAsync("test@example.com", Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_InvalidCode_ReturnsError()
    {
        var user = new UserBuilder().WithId(1).WithEmail("test@example.com").Build();
        var code = new VerificationCodeBuilder()
            .WithUserId(1)
            .WithCode("hashed_code")
            .WithType(VerificationCodeType.AccountDeletion)
            .WithExpiresAt(DateTime.UtcNow.AddMinutes(15))
            .Build();

        _sets.Users.Add(user);
        _sets.VerificationCodes.Add(code);
        DbContextMockFactory.Refresh(_db, _sets);
        _codeHasher.Verify("wrong_code", "hashed_code").Returns(false);

        var result = await _handler.Handle(new ConfirmAccountDeletionCommand("wrong_code"), CancellationToken.None);

        result.IsError.Should().BeTrue();
        result.FirstError.Code.Should().Be("AUTH_INVALID_VERIFICATION_CODE");
        user.IsDeleted.Should().BeFalse();
    }

    [Fact]
    public async Task Handle_ExpiredCode_ReturnsError()
    {
        var user = new UserBuilder().WithId(1).WithEmail("test@example.com").Build();
        var code = new VerificationCodeBuilder()
            .WithUserId(1)
            .WithCode("hashed_code")
            .WithType(VerificationCodeType.AccountDeletion)
            .AsExpired()
            .Build();

        _sets.Users.Add(user);
        _sets.VerificationCodes.Add(code);
        DbContextMockFactory.Refresh(_db, _sets);

        var result = await _handler.Handle(new ConfirmAccountDeletionCommand("123456"), CancellationToken.None);

        result.IsError.Should().BeTrue();
        result.FirstError.Code.Should().Be("AUTH_INVALID_VERIFICATION_CODE");
    }

    [Fact]
    public async Task Handle_UserWithAvatar_QueuesR2Deletion()
    {
        var user = new UserBuilder()
            .WithId(1)
            .WithEmail("test@example.com")
            .WithAvatarUrl("https://pub-example.r2.dev/avatars/user-1.webp")
            .Build();

        var code = new VerificationCodeBuilder()
            .WithUserId(1)
            .WithCode("hashed_code")
            .WithType(VerificationCodeType.AccountDeletion)
            .WithExpiresAt(DateTime.UtcNow.AddMinutes(15))
            .Build();

        _sets.Users.Add(user);
        _sets.VerificationCodes.Add(code);
        DbContextMockFactory.Refresh(_db, _sets);
        _codeHasher.Verify("123456", "hashed_code").Returns(true);

        var result = await _handler.Handle(new ConfirmAccountDeletionCommand("123456"), CancellationToken.None);

        result.IsError.Should().BeFalse();
        _sets.FilesToDelete.Should().HaveCount(1);
        _sets.FilesToDelete[0].R2Key.Should().Be("avatars/user-1.webp");
        _sets.FilesToDelete[0].Reason.Should().Be("gdpr_account_deletion");
    }

    [Fact]
    public async Task Handle_UserNotFound_ReturnsNotFound()
    {
        DbContextMockFactory.Refresh(_db, _sets);

        var result = await _handler.Handle(new ConfirmAccountDeletionCommand("123456"), CancellationToken.None);

        result.IsError.Should().BeTrue();
        result.FirstError.Code.Should().Be("USER_NOT_FOUND");
    }

    [Fact]
    public async Task Handle_AdminRole_ReturnsForbiddenWithoutDeleting()
    {
        var user = new UserBuilder().WithId(1).WithEmail("admin@example.com").WithRole(UserRole.Admin).Build();
        var code = new VerificationCodeBuilder()
            .WithUserId(1)
            .WithCode("hashed_code")
            .WithType(VerificationCodeType.AccountDeletion)
            .WithExpiresAt(DateTime.UtcNow.AddMinutes(15))
            .Build();

        _sets.Users.Add(user);
        _sets.VerificationCodes.Add(code);
        DbContextMockFactory.Refresh(_db, _sets);
        _codeHasher.Verify("123456", "hashed_code").Returns(true);

        var result = await _handler.Handle(new ConfirmAccountDeletionCommand("123456"), CancellationToken.None);

        result.IsError.Should().BeTrue();
        result.FirstError.Code.Should().Be("ACCOUNT_ADMIN_CANNOT_DELETE_OWN");
        user.IsDeleted.Should().BeFalse();
        await _emailService.DidNotReceive().SendAccountDeletionConfirmationAsync(Arg.Any<string>(), Arg.Any<CancellationToken>());
    }
}
