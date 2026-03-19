namespace Smakosz.Domain.Entities;

public class SearchHistory
{
    public int SearchId { get; set; }
    public int? UserId { get; set; }
    public string SearchQuery { get; set; } = string.Empty;
    public DateTime? CreatedAt { get; set; }

    public User? User { get; set; }
}
