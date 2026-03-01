using FluentAssertions;
using Smakosz.Application.Common.Interfaces;
using Smakosz.Application.Features.Reports.Commands.CreateReport;
using Smakosz.Domain.Entities;
using Smakosz.Domain.Enums;
using Smakosz.UnitTests.Common.TestInfrastructure;
using Smakosz.UnitTests.Common.TestInfrastructure.EntityBuilders;

namespace Smakosz.UnitTests.Features.Reports.Commands.CreateReport;

[Trait("Category", "Handlers")]
public class CreateReportHandlerTests
{
    private readonly ISmakoszDbContext _db;
    private readonly MockDbSets _sets;
    private readonly ICurrentUserService _currentUser;
    private readonly CreateReportHandler _handler;

    public CreateReportHandlerTests()
    {
        (_db, _sets) = DbContextMockFactory.Create();
        _currentUser = MockExtensions.CreateAuthenticatedUser(userId: 1);
        _handler = new CreateReportHandler(_db, _currentUser);
    }

    [Fact]
    public async Task Handle_ValidReport_CreatesReportAndTicket()
    {
        var review = new ReviewBuilder().WithId(1).Build();
        _sets.Reviews.Add(review);
        _sets.ReportReasonDefinitions.Add(new ReportReasonDefinition
        {
            ReasonCode = "SPAM", LabelPl = "Spam", IsActive = true, SeverityScore = 2
        });
        DbContextMockFactory.Refresh(_db, _sets);

        var result = await _handler.Handle(
            new CreateReportCommand(review.PublicId, new List<string> { "SPAM" }, null),
            CancellationToken.None);

        result.IsError.Should().BeFalse();
        _sets.Reports.Should().HaveCount(1);
        _sets.SystemTickets.Should().HaveCount(1);
        _sets.ReportReasonAssignments.Should().HaveCount(1);
    }

    [Fact]
    public async Task Handle_NotAuthenticated_ReturnsError()
    {
        var anonymous = MockExtensions.CreateAnonymousUser();
        var handler = new CreateReportHandler(_db, anonymous);

        var result = await handler.Handle(
            new CreateReportCommand(Guid.NewGuid(), new List<string> { "SPAM" }, null),
            CancellationToken.None);

        result.IsError.Should().BeTrue();
        result.FirstError.Code.Should().Be("AUTH_INVALID_CREDENTIALS");
    }

    [Fact]
    public async Task Handle_EmptyReasonCodes_ReturnsError()
    {
        var result = await _handler.Handle(
            new CreateReportCommand(Guid.NewGuid(), new List<string>(), null),
            CancellationToken.None);

        result.IsError.Should().BeTrue();
        result.FirstError.Code.Should().Be("REPORT_INVALID_REASON_CODE");
    }

    [Fact]
    public async Task Handle_InvalidReasonCode_ReturnsError()
    {
        var review = new ReviewBuilder().WithId(1).Build();
        _sets.Reviews.Add(review);
        DbContextMockFactory.Refresh(_db, _sets);

        var result = await _handler.Handle(
            new CreateReportCommand(review.PublicId, new List<string> { "NONEXISTENT_CODE" }, null),
            CancellationToken.None);

        result.IsError.Should().BeTrue();
        result.FirstError.Code.Should().Be("REPORT_INVALID_REASON_CODE");
    }

    [Fact]
    public async Task Handle_AlreadyReported_ReturnsConflict()
    {
        var review = new ReviewBuilder().WithId(1).Build();
        _sets.Reviews.Add(review);
        _sets.Reports.Add(new Report
        {
            ReportId = 1, ReporterId = 1, EntityType = ReportEntityType.Review,
            EntityId = 1, Status = ReportStatus.Pending
        });
        DbContextMockFactory.Refresh(_db, _sets);

        var result = await _handler.Handle(
            new CreateReportCommand(review.PublicId, new List<string> { "SPAM" }, null),
            CancellationToken.None);

        result.IsError.Should().BeTrue();
        result.FirstError.Code.Should().Be("REPORT_ALREADY_EXISTS");
    }

    [Fact]
    public async Task Handle_HighSeverity_SetsHighPriority()
    {
        var review = new ReviewBuilder().WithId(1).Build();
        _sets.Reviews.Add(review);
        _sets.ReportReasonDefinitions.Add(new ReportReasonDefinition
        {
            ReasonCode = "DANGER", LabelPl = "Niebezpieczne", IsActive = true, SeverityScore = 5
        });
        DbContextMockFactory.Refresh(_db, _sets);

        var result = await _handler.Handle(
            new CreateReportCommand(review.PublicId, new List<string> { "DANGER" }, null),
            CancellationToken.None);

        result.IsError.Should().BeFalse();
        _sets.SystemTickets.Should().ContainSingle(t => t.Priority == 1);
    }
}
