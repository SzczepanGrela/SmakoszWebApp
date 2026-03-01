namespace Smakosz.Client.Models;

public class CreateReviewDto
{
    public Guid DishPublicId { get; set; }
    public int DishRating { get; set; }
    public int ServiceRating { get; set; }
    public int CleanlinessRating { get; set; }
    public int AmbianceRating { get; set; }
    public string? Content { get; set; }
    public string VisitDate { get; set; } = default!;
}

public class UpdateReviewDto
{
    public int DishRating { get; set; }
    public int ServiceRating { get; set; }
    public int CleanlinessRating { get; set; }
    public int AmbianceRating { get; set; }
    public string? Content { get; set; }
    public string VisitDate { get; set; } = default!;
}

public class ReportReasonDto
{
    public string ReasonCode { get; set; } = default!;
    public string LabelPl { get; set; } = default!;
    public string? Description { get; set; }
}
