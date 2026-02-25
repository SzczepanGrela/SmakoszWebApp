using ErrorOr;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Smakosz.Application.Common.Errors;
using Smakosz.Application.Common.Interfaces;
using Smakosz.Application.Features.Admin.Dtos;

namespace Smakosz.Application.Features.Admin.Queries.GetSystemConfig;

public record GetSystemConfigQuery : IRequest<ErrorOr<List<SystemConfigDto>>>;

public class GetSystemConfigHandler : IRequestHandler<GetSystemConfigQuery, ErrorOr<List<SystemConfigDto>>>
{
    private readonly ISmakoszDbContext _db;
    private readonly ICurrentUserService _currentUser;

    public GetSystemConfigHandler(ISmakoszDbContext db, ICurrentUserService currentUser)
    {
        _db = db;
        _currentUser = currentUser;
    }

    public async Task<ErrorOr<List<SystemConfigDto>>> Handle(GetSystemConfigQuery request, CancellationToken cancellationToken)
    {
        if (!_currentUser.IsAdmin)
            return DomainErrors.Admin.Forbidden;

        var configs = await _db.SystemConfigs
            .AsNoTracking()
            .OrderBy(c => c.Key)
            .Select(c => new SystemConfigDto
            {
                Key = c.Key,
                Value = c.IsSecret ? "***" : c.Value,
                Description = c.Description,
                IsSecret = c.IsSecret,
                IsPublic = c.IsPublic
            })
            .ToListAsync(cancellationToken);

        return configs;
    }
}
