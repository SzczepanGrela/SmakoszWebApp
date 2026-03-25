using ErrorOr;
using MediatR;
using Smakosz.Application.Features.Search.Dtos;

namespace Smakosz.Application.Features.Search.Queries.SearchSuggest;

public record SearchSuggestQuery(string Query, int Limit = 7) : IRequest<ErrorOr<List<SuggestItemDto>>>;
