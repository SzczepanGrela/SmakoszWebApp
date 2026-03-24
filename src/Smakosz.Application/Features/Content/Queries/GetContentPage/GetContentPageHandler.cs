using ErrorOr;
using MediatR;
using Smakosz.Application.Common.Errors;

namespace Smakosz.Application.Features.Content.Queries.GetContentPage;

public class GetContentPageHandler : IRequestHandler<GetContentPageQuery, ErrorOr<ContentPageDto>>
{
    // Static content - in production this could be backed by a DB table or CMS
    private static readonly Dictionary<string, ContentPageDto> Pages = new(StringComparer.OrdinalIgnoreCase)
    {
        ["about"] = new ContentPageDto
        {
            Title = "O nas",
            Content = "Smakosz to platforma recenzji kulinarnych dla Podkarpacia. " +
                      "Pomagamy mieszkańcom odkrywać najlepsze dania w regionie.",
            LastUpdated = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc)
        },
        ["terms"] = new ContentPageDto
        {
            Title = "Regulamin",
            Content = "Korzystając z platformy Smakosz, akceptujesz niniejszy regulamin...",
            LastUpdated = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc)
        },
        ["contact"] = new ContentPageDto
        {
            Title = "Kontakt",
            Content = "Masz pytania? Napisz do nas na kontakt@smakosz.pl",
            LastUpdated = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc)
        }
    };

    public Task<ErrorOr<ContentPageDto>> Handle(GetContentPageQuery request, CancellationToken cancellationToken)
    {
        if (Pages.TryGetValue(request.Slug, out var page))
            return Task.FromResult<ErrorOr<ContentPageDto>>(page);

        return Task.FromResult<ErrorOr<ContentPageDto>>(DomainErrors.Content.NotFound);
    }
}
