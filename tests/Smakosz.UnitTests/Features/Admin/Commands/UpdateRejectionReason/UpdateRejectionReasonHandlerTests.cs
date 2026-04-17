using FluentAssertions;
using NSubstitute;
using Smakosz.Application.Common.Interfaces;
using Smakosz.Application.Features.Admin.Commands.UpdateRejectionReason;
using Smakosz.Domain.Entities;
using Smakosz.Domain.Enums;
using Smakosz.UnitTests.Common.TestInfrastructure;

namespace Smakosz.UnitTests.Features.Admin.Commands.UpdateRejectionReason;

[Trait("Category", "Handlers")]
public class UpdateRejectionReasonHandlerTests
{
    private readonly ISmakoszDbContext _db;
    private readonly MockDbSets _sets;
    private readonly ICurrentUserService _currentUser;
    private readonly IDateTimeProvider _dateTime;
    private readonly UpdateRejectionReasonHandler _handler;

    public UpdateRejectionReasonHandlerTests()
    {
        (_db, _sets) = DbContextMockFactory.Create();
        _currentUser = MockExtensions.CreateAdminUser(userId: 99);
        _dateTime = Substitute.For<IDateTimeProvider>();
        _dateTime.UtcNow.Returns(new DateTime(2026, 4, 17, 12, 0, 0, DateTimeKind.Utc));
        _handler = new UpdateRejectionReasonHandler(_db, _currentUser, _dateTime);
    }

    private RejectionReason Seed(string code, string label)
    {
        var entity = new RejectionReason
        {
            ReasonCode = code,
            Category = RejectionReasonCategory.Text,
            AdminLabel = label,
            UserMessageTemplate = "Stara treść komunikatu",
            IsActive = true
        };
        _sets.RejectionReasons.Add(entity);
        DbContextMockFactory.Refresh(_db, _sets);
        return entity;
    }

    [Fact]
    public async Task Handle_HappyPath_UpdatesFieldsAndWritesAudit()
    {
        Seed("to_update", "Stara etykieta");

        var result = await _handler.Handle(
            new UpdateRejectionReasonCommand(
                "to_update",
                "Photo",
                "Nowa etykieta",
                "Nowy komunikat dla użytkownika",
                false),
            CancellationToken.None);

        result.IsError.Should().BeFalse();
        var updated = _sets.RejectionReasons.Single();
        updated.Category.Should().Be(RejectionReasonCategory.Photo);
        updated.AdminLabel.Should().Be("Nowa etykieta");
        updated.UserMessageTemplate.Should().Be("Nowy komunikat dla użytkownika");
        updated.IsActive.Should().BeFalse();
        _sets.AuditLogs.Should().ContainSingle(a => a.Operation == AuditOperation.Update);
    }

    [Fact]
    public async Task Handle_NotFound_ReturnsNotFound()
    {
        var result = await _handler.Handle(
            new UpdateRejectionReasonCommand("missing", "Text", "Etykieta", "Komunikat dla użytkownika", true),
            CancellationToken.None);

        result.IsError.Should().BeTrue();
        result.FirstError.Code.Should().Be("REJECTION_REASON_NOT_FOUND");
    }

    [Fact]
    public async Task Handle_DuplicateLabel_OnDifferentCode_ReturnsConflict()
    {
        Seed("code_a", "Etykieta A");
        Seed("code_b", "Etykieta B");

        var result = await _handler.Handle(
            new UpdateRejectionReasonCommand("code_a", "Text", "Etykieta B", "Komunikat dla użytkownika", true),
            CancellationToken.None);

        result.IsError.Should().BeTrue();
        result.FirstError.Code.Should().Be("REJECTION_REASON_LABEL_EXISTS");
    }

    [Fact]
    public async Task Handle_SameLabelOnSelf_IsAllowed()
    {
        Seed("code_a", "Etykieta A");

        var result = await _handler.Handle(
            new UpdateRejectionReasonCommand("code_a", "Text", "etykieta a", "Nowy komunikat dla użytkownika", true),
            CancellationToken.None);

        result.IsError.Should().BeFalse();
        _sets.RejectionReasons.Single().AdminLabel.Should().Be("etykieta a");
    }

    [Fact]
    public async Task Handle_InvalidCategory_ReturnsValidationError()
    {
        Seed("code_a", "Etykieta A");

        var result = await _handler.Handle(
            new UpdateRejectionReasonCommand("code_a", "Audio", "Etykieta A", "Komunikat dla użytkownika", true),
            CancellationToken.None);

        result.IsError.Should().BeTrue();
        result.FirstError.Code.Should().Be("REJECTION_REASON_INVALID_CATEGORY");
    }
}
