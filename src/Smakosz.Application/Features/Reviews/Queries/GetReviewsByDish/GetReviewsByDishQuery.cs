using ErrorOr;
using MediatR;
using Smakosz.Application.Common.Models;
using Smakosz.Application.Features.Reviews.Dtos;

namespace Smakosz.Application.Features.Reviews.Queries.GetReviewsByDish;

public record GetReviewsByDishQuery(
    string DishSlug,
    PaginationParams Pagination,
    string SortBy = "helpful"
) : IRequest<ErrorOr<PagedResult<ReviewCardDto>>>;
