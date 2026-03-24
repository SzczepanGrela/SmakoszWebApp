using ErrorOr;
using MediatR;
using Smakosz.Application.Common.Models;
using Smakosz.Application.Features.Users.Dtos;

namespace Smakosz.Application.Features.Users.Queries.GetUserFollowing;

public record GetUserFollowingQuery(string Slug, PaginationParams Pagination) : IRequest<ErrorOr<PagedResult<UserListItemDto>>>;
