namespace Smakosz.Application.Features.Search.Dtos;

public class SuggestItemDto
{
    public string Type { get; init; } = default!;
    public string Name { get; init; } = default!;
    public string Slug { get; init; } = default!;
    public string? Subtitle { get; init; }
    public string? ImageUrl { get; init; }
    public string? ImageBlurhash { get; init; }
}
