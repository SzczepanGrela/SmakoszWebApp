using ErrorOr;
using MediatR;

namespace Smakosz.Application.Features.Content.Queries.GetContentPage;

public record GetContentPageQuery(string Slug) : IRequest<ErrorOr<ContentPageDto>>;

public class ContentPageDto
{
    public string Title { get; init; } = default!;
    public string Content { get; init; } = default!;
    public DateTime LastUpdated { get; init; }
}
