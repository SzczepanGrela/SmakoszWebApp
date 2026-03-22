using ErrorOr;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Smakosz.Application.Common.Errors;
using Smakosz.Application.Common.Interfaces;

namespace Smakosz.Application.Features.Admin.Commands.ScheduleModeration;

public record ScheduleModerationCommand : IRequest<ErrorOr<Success>>;

public class ScheduleModerationHandler : IRequestHandler<ScheduleModerationCommand, ErrorOr<Success>>
{
    private readonly ICurrentUserService _currentUser;
    private readonly IModerationAggregationService _moderationService;
    private readonly ISmakoszDbContext _db;

    public ScheduleModerationHandler(
        ICurrentUserService currentUser,
        IModerationAggregationService moderationService,
        ISmakoszDbContext db)
    {
        _currentUser = currentUser;
        _moderationService = moderationService;
        _db = db;
    }

    public async Task<ErrorOr<Success>> Handle(ScheduleModerationCommand request, CancellationToken cancellationToken)
    {
        if (!_currentUser.IsAdmin)
            return DomainErrors.Admin.Forbidden;

        var configs = await _db.SystemConfigs
            .Where(c => c.Key == "moderation.text_batch_size" || c.Key == "moderation.image_batch_size")
            .ToDictionaryAsync(c => c.Key, c => c.Value, cancellationToken);

        var textBatchSize = configs.TryGetValue("moderation.text_batch_size", out var t) && int.TryParse(t, out var ti) ? ti : 100;
        var imageBatchSize = configs.TryGetValue("moderation.image_batch_size", out var i) && int.TryParse(i, out var ii) ? ii : 10;

        await _moderationService.AggregateAsync(textBatchSize, imageBatchSize, cancellationToken);

        return Result.Success;
    }
}
