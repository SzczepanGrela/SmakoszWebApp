namespace Smakosz.Domain.Entities.System;

public class UserRecommendationCache
{
    public int UserId { get; set; }
    public string TopDishIdsJson { get; set; } = "[]";
    public string ModelVersion { get; set; } = string.Empty;
    public DateTime GeneratedAt { get; set; }
}
