namespace Smakosz.Application.Features.Reviews.Dtos;

public class UserSummaryDto
{
    public Guid PublicId { get; init; }
    public string Slug { get; init; } = default!;
    public string Username { get; init; } = default!;
    public string? AvatarUrl { get; init; }
    public string? AvatarBlurhash { get; init; }
    public int ReviewCount { get; init; }
}
