using ErrorOr;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Smakosz.Application.Common.Interfaces;
using Smakosz.Application.Features.Search.Dtos;

namespace Smakosz.Application.Features.Search.Queries.SearchSuggest;

public class SearchSuggestHandler : IRequestHandler<SearchSuggestQuery, ErrorOr<List<SuggestItemDto>>>
{
    private const string SimilarityThresholdKey = "search.suggest.similarity_threshold";
    private const double DefaultSimilarityThreshold = 0.2;

    private readonly ISmakoszDbContext _db;
    private readonly IPublicConfigProvider _config;

    public SearchSuggestHandler(ISmakoszDbContext db, IPublicConfigProvider config)
    {
        _db = db;
        _config = config;
    }

    public async Task<ErrorOr<List<SuggestItemDto>>> Handle(SearchSuggestQuery request, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(request.Query) || request.Query.Trim().Length < 2)
            return new List<SuggestItemDto>();

        var term = request.Query.Trim().ToLowerInvariant();
        var limit = Math.Clamp(request.Limit, 1, 10);
        var threshold = await _config.GetDoubleAsync(SimilarityThresholdKey, DefaultSimilarityThreshold, ct);

        // Read from search_autocomplete view so Postgres pushes similarity through UNION ALL onto trgm_idx_restaurants_name and trgm_idx_dishes_name. Prefix matches win first because typing the start of a name is the strongest signal of intent, then trigram similarity, then priority (restaurants before dishes when scores tie).
        var rows = await _db.SearchAutocompletes
            .FromSqlInterpolated($@"
                SELECT type, id, name, slug, subtitle, icon, image_blurhash, name_normalized, priority
                FROM search_autocomplete
                WHERE similarity(name_normalized, f_unaccent(lower({term}))) >= {threshold}
                ORDER BY
                    CASE WHEN name_normalized LIKE f_unaccent(lower({term})) || '%' THEN 0 ELSE 1 END,
                    similarity(name_normalized, f_unaccent(lower({term}))) DESC,
                    priority ASC
                LIMIT {limit}")
            .AsNoTracking()
            .ToListAsync(ct);

        return rows
            .Select(r => new SuggestItemDto
            {
                Type = r.Type,
                Name = r.Name,
                Slug = r.Slug ?? string.Empty,
                Subtitle = r.Subtitle,
                ImageUrl = r.Icon is not null ? r.Icon.Replace(".webp", "_tiny.webp") : null,
                ImageBlurhash = r.ImageBlurhash
            })
            .ToList();
    }
}
