using FluentAssertions;
using NSubstitute;
using Smakosz.Application.Common.Interfaces;
using Smakosz.Application.Features.Admin.Commands.DeleteRejectionReason;
using Smakosz.Domain.Entities;
using Smakosz.Domain.Enums;
using Smakosz.UnitTests.Common.TestInfrastructure;

namespace Smakosz.UnitTests.Features.Admin.Commands.DeleteRejectionReason;

[Trait("Category", "Handlers")]
public class DeleteRejectionReasonHandlerTests
{
    private readonly ISmakoszDbContext _db;
    private readonly MockDbSets _sets;
    private readonly ICurrentUserService _currentUser;
    private readonly IDateTimeProvider _dateTime;
    private readonly DeleteRejectionReasonHandler _handler;

    public DeleteRejectionReasonHandlerTests()
    {
        (_db, _sets) = DbContextMockFactory.Create();
        _currentUser = MockExtensions.CreateAdminUser(userId: 99);
        _dateTime = Substitute.For<IDateTimeProvider>();
        _dateTime.UtcNow.Returns(new DateTime(2026, 4, 17, 12, 0, 0, DateTimeKind.Utc));
        _handler = new DeleteRejectionReasonHandler(_db, _currentUser, _dateTime);
    }

    [Fact]
    public async Task Handle_HappyPath_RemovesEntityAndWritesAudit()
    {
        _sets.RejectionReasons.Add(new RejectionReason
        {
            ReasonCode = "to_delete",
            Category = RejectionReasonCategory.Text,
            AdminLabel = "Do usunięcia",
            UserMessageTemplate = "Treść komunikatu",
            IsActive = true
        });
        DbContextMockFactory.Refresh(_db, _sets);

        var result = await _handler.Handle(
            new DeleteRejectionReasonCommand("to_delete"),
            CancellationToken.None);

        result.IsError.Should().BeFalse();
        _sets.RejectionReasons.Should().BeEmpty();
        _sets.AuditLogs.Should().ContainSingle(a => a.Operation == AuditOperation.Delete);
    }

    [Fact]
    public async Task Handle_NotFound_ReturnsNotFound()
    {
        var result = await _handler.Handle(
            new DeleteRejectionReasonCommand("missing"),
            CancellationToken.None);

        result.IsError.Should().BeTrue();
        result.FirstError.Code.Should().Be("REJECTION_REASON_NOT_FOUND");
    }

    [Fact]
    public async Task Handle_NotAdmin_ReturnsForbidden()
    {
        var userService = MockExtensions.CreateAuthenticatedUser(role: "Moderator");
        var handler = new DeleteRejectionReasonHandler(_db, userService, _dateTime);

        var result = await handler.Handle(
            new DeleteRejectionReasonCommand("any"),
            CancellationToken.None);

        result.IsError.Should().BeTrue();
        result.FirstError.Code.Should().Be("ADMIN_FORBIDDEN");
    }
}
