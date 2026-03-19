namespace Smakosz.Domain.Entities;

public class ReportReasonDefinition
{
    public string ReasonCode { get; set; } = string.Empty;
    public string LabelPl { get; set; } = string.Empty;
    public string? Description { get; set; }
    public int SeverityScore { get; set; } = 1;
    public bool IsActive { get; set; } = true;
    public DateTime? CreatedAt { get; set; }
}
