using FluentAssertions;
using Smakosz.Application.Common.Interfaces;
using Smakosz.Application.Features.Reports.Queries.GetReportReasons;
using Smakosz.Domain.Entities;
using Smakosz.UnitTests.Common.TestInfrastructure;

namespace Smakosz.UnitTests.Features.Reports.Queries.GetReportReasons;

[Trait("Category", "Handlers")]
public class GetReportReasonsHandlerTests
{
    private readonly ISmakoszDbContext _db;
    private readonly MockDbSets _sets;
    private readonly GetReportReasonsHandler _handler;

    public GetReportReasonsHandlerTests()
    {
        (_db, _sets) = DbContextMockFactory.Create();
        _handler = new GetReportReasonsHandler(_db);
    }

    [Fact]
    public async Task Handle_ReturnsActiveReasons_OrderedBySeverity()
    {
        _sets.ReportReasonDefinitions.Add(new ReportReasonDefinition
        {
            ReasonCode = "SPAM", LabelPl = "Spam", IsActive = true, SeverityScore = 2
        });
        _sets.ReportReasonDefinitions.Add(new ReportReasonDefinition
        {
            ReasonCode = "DANGER", LabelPl = "Niebezpieczne", IsActive = true, SeverityScore = 5
        });
        _sets.ReportReasonDefinitions.Add(new ReportReasonDefinition
        {
            ReasonCode = "OLD", LabelPl = "Stary", IsActive = false, SeverityScore = 1
        });
        DbContextMockFactory.Refresh(_db, _sets);

        var result = await _handler.Handle(new GetReportReasonsQuery(), CancellationToken.None);

        result.IsError.Should().BeFalse();
        result.Value.Should().HaveCount(2); // only active
        result.Value[0].ReasonCode.Should().Be("SPAM"); // lower severity first
        result.Value[1].ReasonCode.Should().Be("DANGER");
    }

    [Fact]
    public async Task Handle_NoActiveReasons_ReturnsEmptyList()
    {
        var result = await _handler.Handle(new GetReportReasonsQuery(), CancellationToken.None);

        result.IsError.Should().BeFalse();
        result.Value.Should().BeEmpty();
    }

    [Fact]
    public async Task Handle_MapsFieldsCorrectly()
    {
        _sets.ReportReasonDefinitions.Add(new ReportReasonDefinition
        {
            ReasonCode = "TEST", LabelPl = "Testowy", Description = "Opis testowy",
            IsActive = true, SeverityScore = 1
        });
        DbContextMockFactory.Refresh(_db, _sets);

        var result = await _handler.Handle(new GetReportReasonsQuery(), CancellationToken.None);

        result.IsError.Should().BeFalse();
        result.Value[0].ReasonCode.Should().Be("TEST");
        result.Value[0].LabelPl.Should().Be("Testowy");
        result.Value[0].Description.Should().Be("Opis testowy");
    }
}
