namespace Smakosz.Domain.Entities;

public class ReportReasonAssignment
{
    public int ReportId { get; set; }
    public string ReasonCode { get; set; } = string.Empty;

    public Report Report { get; set; } = null!;
    public ReportReasonDefinition ReasonDefinition { get; set; } = null!;
}
