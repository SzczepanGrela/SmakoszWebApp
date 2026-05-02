using ErrorOr;
using MediatR;
using Smakosz.Application.Common.Errors;

namespace Smakosz.Application.Features.Content.Queries.GetContentPage;

public class GetContentPageHandler : IRequestHandler<GetContentPageQuery, ErrorOr<ContentPageDto>>
{
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
            Content = """
                <h2>§1 Postanowienia ogólne</h2>
                <p>Lorem ipsum dolor sit amet, consectetur adipiscing elit. Sed do eiusmod tempor incididunt ut labore et dolore magna aliqua. Ut enim ad minim veniam, quis nostrud exercitation ullamco laboris nisi ut aliquip ex ea commodo consequat.</p>
                <h2>§2 Definicje</h2>
                <p>Duis aute irure dolor in reprehenderit in voluptate velit esse cillum dolore eu fugiat nulla pariatur. Excepteur sint occaecat cupidatat non proident, sunt in culpa qui officia deserunt mollit anim id est laborum.</p>
                <h2>§3 Rejestracja i konto użytkownika</h2>
                <p>Sed ut perspiciatis unde omnis iste natus error sit voluptatem accusantium doloremque laudantium, totam rem aperiam, eaque ipsa quae ab illo inventore veritatis et quasi architecto beatae vitae dicta sunt explicabo.</p>
                <h2>§4 Zasady korzystania z serwisu</h2>
                <p>Nemo enim ipsam voluptatem quia voluptas sit aspernatur aut odit aut fugit, sed quia consequuntur magni dolores eos qui ratione voluptatem sequi nesciunt. Neque porro quisquam est, qui dolorem ipsum quia dolor sit amet, consectetur, adipisci velit.</p>
                <h2>§5 Treści zamieszczane przez użytkowników</h2>
                <p>Ut enim ad minima veniam, quis nostrum exercitationem ullam corporis suscipit laboriosam, nisi ut aliquid ex ea commodi consequatur. Quis autem vel eum iure reprehenderit qui in ea voluptate velit esse quam nihil molestiae consequatur.</p>
                <h2>§6 Moderacja</h2>
                <p>At vero eos et accusamus et iusto odio dignissimos ducimus qui blanditiis praesentium voluptatum deleniti atque corrupti quos dolores et quas molestias excepturi sint occaecati cupiditate non provident.</p>
                <h2>§7 Odpowiedzialność</h2>
                <p>Similique sunt in culpa qui officia deserunt mollitia animi, id est laborum et dolorum fuga. Et harum quidem rerum facilis est et expedita distinctio. Nam libero tempore, cum soluta nobis est eligendi optio cumque nihil impedit.</p>
                <h2>§8 Reklamacje</h2>
                <p>Temporibus autem quibusdam et aut officiis debitis aut rerum necessitatibus saepe eveniet ut et voluptates repudiandae sint et molestiae non recusandae. Itaque earum rerum hic tenetur a sapiente delectus.</p>
                <h2>§9 Zmiany regulaminu</h2>
                <p>Ut aut reiciendis voluptatibus maiores alias consequatur aut perferendis doloribus asperiores repellat. Lorem ipsum dolor sit amet, consectetur adipiscing elit, sed do eiusmod tempor incididunt.</p>
                <h2>§10 Postanowienia końcowe</h2>
                <p>Excepteur sint occaecat cupidatat non proident, sunt in culpa qui officia deserunt mollit anim id est laborum. Lorem ipsum dolor sit amet, consectetur adipiscing elit.</p>
                <p class="text-muted small mt-4"><em>Niniejszy dokument ma charakter poglądowy (placeholder lorem ipsum) na potrzeby pracy inżynierskiej.</em></p>
                """,
            LastUpdated = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc)
        },
        ["privacy"] = new ContentPageDto
        {
            Title = "Polityka prywatności",
            Content = """
                <h2>§1 Postanowienia ogólne</h2>
                <p>Lorem ipsum dolor sit amet, consectetur adipiscing elit. Sed do eiusmod tempor incididunt ut labore et dolore magna aliqua. Niniejszy dokument określa zasady przetwarzania danych osobowych użytkowników serwisu Smakosz.</p>
                <h2>§2 Administrator danych</h2>
                <p>Ut enim ad minim veniam, quis nostrud exercitation ullamco laboris nisi ut aliquip ex ea commodo consequat. Kontakt z administratorem danych: admin@smakosz.xyz.</p>
                <h2>§3 Zakres przetwarzania danych</h2>
                <p>Duis aute irure dolor in reprehenderit in voluptate velit esse cillum dolore eu fugiat nulla pariatur. Przetwarzamy dane podane przy rejestracji oraz dane techniczne związane z korzystaniem z serwisu.</p>
                <h2>§4 Cele i podstawy prawne przetwarzania</h2>
                <p>Excepteur sint occaecat cupidatat non proident, sunt in culpa qui officia deserunt mollit anim id est laborum. Sed ut perspiciatis unde omnis iste natus error sit voluptatem accusantium doloremque laudantium.</p>
                <h2>§5 Okresy retencji</h2>
                <p>Totam rem aperiam, eaque ipsa quae ab illo inventore veritatis et quasi architecto beatae vitae dicta sunt explicabo. Logi systemowe przechowywane są przez okresy zgodne z polityką retencji.</p>
                <h2>§6 Odbiorcy danych</h2>
                <p>Nemo enim ipsam voluptatem quia voluptas sit aspernatur aut odit aut fugit, sed quia consequuntur magni dolores eos qui ratione voluptatem sequi nesciunt. Korzystamy z usług zaufanych podmiotów przetwarzających.</p>
                <h2>§7 Prawa użytkownika</h2>
                <p>Neque porro quisquam est, qui dolorem ipsum quia dolor sit amet, consectetur, adipisci velit, sed quia non numquam eius modi tempora incidunt ut labore et dolore magnam aliquam quaerat voluptatem.</p>
                <h2>§8 Zautomatyzowane podejmowanie decyzji</h2>
                <p>Ut enim ad minima veniam, quis nostrum exercitationem ullam corporis suscipit laboriosam, nisi ut aliquid ex ea commodi consequatur. Serwis wykorzystuje algorytmy moderacji oraz rekomendacji.</p>
                <h2>§9 Pliki cookies</h2>
                <p>Quis autem vel eum iure reprehenderit qui in ea voluptate velit esse quam nihil molestiae consequatur, vel illum qui dolorem eum fugiat quo voluptas nulla pariatur.</p>
                <h2>§10 Kontakt</h2>
                <p>At vero eos et accusamus et iusto odio dignissimos ducimus qui blanditiis praesentium voluptatum deleniti atque corrupti quos dolores et quas molestias excepturi sint occaecati cupiditate. Pytania kierować na admin@smakosz.xyz.</p>
                <p class="text-muted small mt-4"><em>Niniejszy dokument ma charakter poglądowy (placeholder lorem ipsum) na potrzeby pracy inżynierskiej.</em></p>
                """,
            LastUpdated = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc)
        },
        ["contact"] = new ContentPageDto
        {
            Title = "Kontakt",
            Content = "Masz pytania? Napisz do nas na kontakt@smakosz.xyz",
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
