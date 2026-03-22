using ErrorOr;
using MediatR;
using Smakosz.Application.Common.Models;
using Smakosz.Application.Features.Search.Dtos;

namespace Smakosz.Application.Features.Search.Queries.SearchQuery;

public record SearchQuery(
    PaginationParams Pagination,
    string Type = "restaurants",
    string? Query = null,
    string? Location = null,
    string? Cuisines = null,
    int? MinPrice = null,
    int? MaxPrice = null,
    string? Dietary = null,
    decimal? Lat = null,
    decimal? Lng = null,
    int Radius = 5,
    string SortBy = "rating",
    string SortDir = "desc"
) : IRequest<ErrorOr<SearchResultDto>>;
