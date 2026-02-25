using FluentAssertions;
using NSubstitute;
using Smakosz.Application.Common.Interfaces;
using Smakosz.Application.Features.Admin.Commands.UpdateReportStatus;
using Smakosz.Domain.Entities;
using Smakosz.Domain.Enums;
using Smakosz.UnitTests.Common.TestInfrastructure;

namespace Smakosz.UnitTests.Features.Admin.Commands.UpdateReportStatus;

[Trait("Category", "Handlers")]
public class UpdateReportStatusHandlerTests
{
    private readonly ISmakoszDbContext _db;
    private readonly MockDbSets _sets;
    private readonly ICurrentUserService _currentUser;
    private readonly IDateTimeProvider _dateTime;
    private readonly UpdateReportStatusHandler _handler;

    public UpdateReportStatusHandlerTests()
    {
        (_db, _sets) = DbContextMockFactory.Create();
        _currentUser = MockExtensions.CreateAdminUser(userId: 99, sessionId: 100);
        _dateTime = Substitute.For<IDateTimeProvider>();
        _dateTime.UtcNow.Returns(new DateTime(2024, 1, 1, 0, 0, 0, DateTimeKind.Utc));
        _handler = new UpdateReportStatusHandler(_db, _currentUser, _dateTime);
    }

    [Fact]
    public async Task Handle_HappyPath_SetsResolvedFieldsAndReturnsSuccess()
    {
        var report = new Report
        {
            ReportId = 10,
            ReporterId = 5,
            EntityType = ReportEntityType.Review,
            EntityId = 1,
            Status = ReportStatus.Pending
        };
        _sets.Reports.Add(report);
        DbContextMockFactory.Refresh(_db, _sets);

        var result = await _handler.Handle(
            new UpdateReportStatusCommand(10, "Resolved"),
            CancellationToken.None);

        result.IsError.Should().BeFalse();
        report.Status.Should().Be(ReportStatus.Resolved);
        report.ResolvedAt.Should().Be(new DateTime(2024, 1, 1, 0, 0, 0, DateTimeKind.Utc));
        report.ResolvedByAdminId.Should().Be(99);
    }

    [Fact]
    public async Task Handle_NonAdmin_ReturnsForbidden()
    {
        var nonAdmin = MockExtensions.CreateAuthenticatedUser(userId: 1, role: "User");
        var handler = new UpdateReportStatusHandler(_db, nonAdmin, _dateTime);

        var result = await handler.Handle(
            new UpdateReportStatusCommand(10, "Resolved"),
            CancellationToken.None);

        result.IsError.Should().BeTrue();
        result.FirstError.Code.Should().Be("ADMIN_FORBIDDEN");
    }

    [Fact]
    public async Task Handle_ReportNotFound_ReturnsNotFoundError()
    {
        var result = await _handler.Handle(
            new UpdateReportStatusCommand(999, "Resolved"),
            CancellationToken.None);

        result.IsError.Should().BeTrue();
        result.FirstError.Code.Should().Be("REPORT_NOT_FOUND");
    }

    [Fact]
    public async Task Handle_InvalidStatus_ReturnsInvalidStatusError()
    {
        var report = new Report
        {
            ReportId = 11,
            ReporterId = 5,
            EntityType = ReportEntityType.Review,
            EntityId = 1,
            Status = ReportStatus.Pending
        };
        _sets.Reports.Add(report);
        DbContextMockFactory.Refresh(_db, _sets);

        var result = await _handler.Handle(
            new UpdateReportStatusCommand(11, "NotAValidStatus"),
            CancellationToken.None);

        result.IsError.Should().BeTrue();
        result.FirstError.Code.Should().Be("REPORT_INVALID_STATUS");
    }
}
