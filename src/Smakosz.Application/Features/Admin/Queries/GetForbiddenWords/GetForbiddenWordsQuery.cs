using ErrorOr;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Smakosz.Application.Common.Errors;
using Smakosz.Application.Common.Interfaces;
using Smakosz.Application.Common.Models;
using Smakosz.Application.Features.Admin.Dtos;

namespace Smakosz.Application.Features.Admin.Queries.GetForbiddenWords;

public record GetForbiddenWordsQuery(PaginationParams Pagination, string? Search = null)
    : IRequest<ErrorOr<PagedResult<AdminForbiddenWordDto>>>;

public class GetForbiddenWordsHandler : IRequestHandler<GetForbiddenWordsQuery, ErrorOr<PagedResult<AdminForbiddenWordDto>>>
{
    private readonly ISmakoszDbContext _db;
    private readonly ICurrentUserService _currentUser;

    public GetForbiddenWordsHandler(ISmakoszDbContext db, ICurrentUserService currentUser)
    {
        _db = db;
        _currentUser = currentUser;
    }

    public async Task<ErrorOr<PagedResult<AdminForbiddenWordDto>>> Handle(GetForbiddenWordsQuery request, CancellationToken cancellationToken)
    {
        if (!_currentUser.IsAdmin)
            return DomainErrors.Admin.Forbidden;

        var query = _db.ForbiddenWords.AsNoTracking().AsQueryable();

        if (!string.IsNullOrWhiteSpace(request.Search))
        {
            var search = request.Search.ToLower();
            query = query.Where(w => w.Word.ToLower().Contains(search));
        }

        var totalCount = await query.CountAsync(cancellationToken);

        var items = await query
            .OrderBy(w => w.Word)
            .Skip((request.Pagination.Page - 1) * request.Pagination.PageSize)
            .Take(request.Pagination.PageSize)
            .Select(w => new AdminForbiddenWordDto
            {
                WordId = w.WordId,
                Word = w.Word,
                Category = w.Category.ToString(),
                IsRegex = w.IsRegex,
                AddedByUsername = w.AddedByUser != null ? w.AddedByUser.Username : null,
                CreatedAt = w.CreatedAt
            })
            .ToListAsync(cancellationToken);

        return new PagedResult<AdminForbiddenWordDto>
        {
            Data = items,
            Pagination = new PaginationInfo
            {
                Page = request.Pagination.Page,
                PageSize = request.Pagination.PageSize,
                TotalCount = totalCount,
                TotalPages = (int)Math.Ceiling(totalCount / (double)request.Pagination.PageSize)
            }
        };
    }
}
