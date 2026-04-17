using FluentAssertions;
using NSubstitute;
using Smakosz.Application.Common.Interfaces;
using Smakosz.Application.Features.Admin.Commands.CreateRejectionReason;
using Smakosz.Domain.Entities;
using Smakosz.Domain.Enums;
using Smakosz.UnitTests.Common.TestInfrastructure;

namespace Smakosz.UnitTests.Features.Admin.Commands.CreateRejectionReason;

[Trait("Category", "Handlers")]
public class CreateRejectionReasonHandlerTests
{
    private readonly ISmakoszDbContext _db;
    private readonly MockDbSets _sets;
    private readonly ICurrentUserService _currentUser;
    private readonly IDateTimeProvider _dateTime;
    private readonly CreateRejectionReasonHandler _handler;

    public CreateRejectionReasonHandlerTests()
    {
        (_db, _sets) = DbContextMockFactory.Create();
        _currentUser = MockExtensions.CreateAdminUser(userId: 99);
        _dateTime = Substitute.For<IDateTimeProvider>();
        _dateTime.UtcNow.Returns(new DateTime(2026, 4, 17, 12, 0, 0, DateTimeKind.Utc));
        _handler = new CreateRejectionReasonHandler(_db, _currentUser, _dateTime);
    }

    [Fact]
    public async Task Handle_HappyPath_CreatesReasonAndWritesAudit()
    {
        var result = await _handler.Handle(
            new CreateRejectionReasonCommand(
                "custom_reason",
                "Text",
                "Testowa etykieta",
                "Recenzja została odrzucona z testowego powodu",
                true),
            CancellationToken.None);

        result.IsError.Should().BeFalse();
        result.Value.Should().Be("custom_reason");
        _sets.RejectionReasons.Should().HaveCount(1);
        _sets.RejectionReasons[0].Category.Should().Be(RejectionReasonCategory.Text);
        _sets.RejectionReasons[0].IsActive.Should().BeTrue();
        _sets.AuditLogs.Should().ContainSingle(a =>
            a.TableName == "RejectionReasons" && a.Operation == AuditOperation.Insert);
    }

    [Fact]
    public async Task Handle_NotAdmin_ReturnsForbidden()
    {
        var userService = MockExtensions.CreateAuthenticatedUser(role: "User");
        var handler = new CreateRejectionReasonHandler(_db, userService, _dateTime);

        var result = await handler.Handle(
            new CreateRejectionReasonCommand("any_code", "Text", "Label", "Valid message body", true),
            CancellationToken.None);

        result.IsError.Should().BeTrue();
        result.FirstError.Code.Should().Be("ADMIN_FORBIDDEN");
    }

    [Fact]
    public async Task Handle_InvalidCategory_ReturnsValidationError()
    {
        var result = await _handler.Handle(
            new CreateRejectionReasonCommand("any_code", "Video", "Label", "Valid message body", true),
            CancellationToken.None);

        result.IsError.Should().BeTrue();
        result.FirstError.Code.Should().Be("REJECTION_REASON_INVALID_CATEGORY");
    }

    [Fact]
    public async Task Handle_DuplicateCode_ReturnsConflict()
    {
        _sets.RejectionReasons.Add(new RejectionReason
        {
            ReasonCode = "existing",
            Category = RejectionReasonCategory.Text,
            AdminLabel = "Istniejąca",
            UserMessageTemplate = "Treść komunikatu"
        });
        DbContextMockFactory.Refresh(_db, _sets);

        var result = await _handler.Handle(
            new CreateRejectionReasonCommand("existing", "Text", "Nowa etykieta", "Nowy komunikat", true),
            CancellationToken.None);

        result.IsError.Should().BeTrue();
        result.FirstError.Code.Should().Be("REJECTION_REASON_CODE_EXISTS");
    }

    [Fact]
    public async Task Handle_DuplicateLabel_ReturnsConflict()
    {
        _sets.RejectionReasons.Add(new RejectionReason
        {
            ReasonCode = "existing",
            Category = RejectionReasonCategory.Text,
            AdminLabel = "Istniejąca",
            UserMessageTemplate = "Treść komunikatu"
        });
        DbContextMockFactory.Refresh(_db, _sets);

        var result = await _handler.Handle(
            new CreateRejectionReasonCommand("new_code", "Text", "istniejąca", "Nowy komunikat", true),
            CancellationToken.None);

        result.IsError.Should().BeTrue();
        result.FirstError.Code.Should().Be("REJECTION_REASON_LABEL_EXISTS");
    }
}
