using ErrorOr;
using MediatR;
using Smakosz.Application.Common.Errors;
using Smakosz.Application.Common.Interfaces;

namespace Smakosz.Application.Features.Admin.Commands.ScheduleModeration;

public record ScheduleModerationCommand : IRequest<ErrorOr<Success>>;

public class ScheduleModerationHandler : IRequestHandler<ScheduleModerationCommand, ErrorOr<Success>>
{
    private readonly ICurrentUserService _currentUser;
    private readonly IModerationAggregationService _moderationService;

    public ScheduleModerationHandler(
        ICurrentUserService currentUser,
        IModerationAggregationService moderationService)
    {
        _currentUser = currentUser;
        _moderationService = moderationService;
    }

    public async Task<ErrorOr<Success>> Handle(ScheduleModerationCommand request, CancellationToken cancellationToken)
    {
        if (!_currentUser.IsAdmin)
            return DomainErrors.Admin.Forbidden;

        await _moderationService.AggregateAsync(cancellationToken);

        return Result.Success;
    }
}
