using ErrorOr;
using MediatR;
using Smakosz.Application.Common.Models;
using Smakosz.Application.Features.Reviews.Dtos;

namespace Smakosz.Application.Features.Reviews.Queries.GetReviewsByUser;

public record GetReviewsByUserQuery(
    string UserSlug,
    PaginationParams Pagination
) : IRequest<ErrorOr<PagedResult<ReviewCardDto>>>;
