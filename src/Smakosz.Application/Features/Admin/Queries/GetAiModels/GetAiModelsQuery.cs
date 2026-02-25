using ErrorOr;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Smakosz.Application.Common.Errors;
using Smakosz.Application.Common.Interfaces;
using Smakosz.Application.Features.Admin.Dtos;

namespace Smakosz.Application.Features.Admin.Queries.GetAiModels;

public record GetAiModelsQuery : IRequest<ErrorOr<List<AiModelDto>>>;

public class GetAiModelsHandler : IRequestHandler<GetAiModelsQuery, ErrorOr<List<AiModelDto>>>
{
    private readonly ISmakoszDbContext _db;
    private readonly ICurrentUserService _currentUser;

    public GetAiModelsHandler(ISmakoszDbContext db, ICurrentUserService currentUser)
    {
        _db = db;
        _currentUser = currentUser;
    }

    public async Task<ErrorOr<List<AiModelDto>>> Handle(GetAiModelsQuery request, CancellationToken cancellationToken)
    {
        if (!_currentUser.IsAdmin)
            return DomainErrors.Admin.Forbidden;

        var models = await _db.AiLogs
            .AsNoTracking()
            .GroupBy(l => new { l.ModelType, l.ModelVersion })
            .Select(g => new AiModelDto
            {
                ModelType = g.Key.ModelType,
                ModelVersion = g.Key.ModelVersion,
                UsageCount = g.Count(),
                LastUsed = g.Max(l => l.CreatedAt)
            })
            .OrderByDescending(m => m.UsageCount)
            .ToListAsync(cancellationToken);

        return models;
    }
}
