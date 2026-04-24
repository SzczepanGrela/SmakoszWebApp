using ErrorOr;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Smakosz.Application.Common.Errors;
using Smakosz.Application.Common.Interfaces;
using Smakosz.Application.Common.Models;
using Smakosz.Application.Features.Admin.Dtos;

namespace Smakosz.Application.Features.Admin.Queries.GetEmailLogs;

public record GetEmailLogsQuery(PaginationParams Pagination, string? Status = null, string? Type = null)
    : IRequest<ErrorOr<PagedResult<EmailLogDto>>>;

public class GetEmailLogsHandler : IRequestHandler<GetEmailLogsQuery, ErrorOr<PagedResult<EmailLogDto>>>
{
    private readonly ISmakoszDbContext _db;
    private readonly ICurrentUserService _currentUser;

    public GetEmailLogsHandler(ISmakoszDbContext db, ICurrentUserService currentUser)
    {
        _db = db;
        _currentUser = currentUser;
    }

    public async Task<ErrorOr<PagedResult<EmailLogDto>>> Handle(GetEmailLogsQuery request, CancellationToken cancellationToken)
    {
        if (!_currentUser.IsAdmin)
            return DomainErrors.Admin.Forbidden;

        var query = _db.EmailLogs.AsNoTracking().AsQueryable();

        if (!string.IsNullOrWhiteSpace(request.Status))
        {
            query = query.Where(l => l.Status == request.Status);
        }

        if (!string.IsNullOrWhiteSpace(request.Type))
        {
            query = query.Where(l => l.Type == request.Type);
        }

        var totalCount = await query.CountAsync(cancellationToken);

        var items = await query
            .OrderByDescending(l => l.CreatedAt)
            .Skip((request.Pagination.Page - 1) * request.Pagination.PageSize)
            .Take(request.Pagination.PageSize)
            .Select(l => new EmailLogDto
            {
                LogId = l.LogId,
                Type = l.Type,
                Recipient = l.Recipient,
                Subject = l.Subject,
                Status = l.Status,
                Provider = l.Provider,
                ProviderMessageId = l.ProviderMessageId,
                ErrorMessage = l.ErrorMessage,
                CreatedAt = l.CreatedAt,
                SentAt = l.SentAt
            })
            .ToListAsync(cancellationToken);

        return new PagedResult<EmailLogDto>
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
