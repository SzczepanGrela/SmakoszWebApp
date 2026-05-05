namespace Smakosz.Domain.Entities;

public class ReviewLike
{
    public int UserId { get; set; }
    public int ReviewId { get; set; }
    public DateTime CreatedAt { get; set; }

    public User User { get; set; } = null!;
    public Review Review { get; set; } = null!;
}
