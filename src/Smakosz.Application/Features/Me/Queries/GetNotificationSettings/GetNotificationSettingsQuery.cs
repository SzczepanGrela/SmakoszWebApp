using ErrorOr;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Smakosz.Application.Common.Errors;
using Smakosz.Application.Common.Interfaces;
using Smakosz.Application.Features.Me.Dtos;

namespace Smakosz.Application.Features.Me.Queries.GetNotificationSettings;

public record GetNotificationSettingsQuery() : IRequest<ErrorOr<NotificationSettingsDto>>;

public class GetNotificationSettingsHandler : IRequestHandler<GetNotificationSettingsQuery, ErrorOr<NotificationSettingsDto>>
{
    private readonly ISmakoszDbContext _db;
    private readonly ICurrentUserService _currentUser;

    public GetNotificationSettingsHandler(ISmakoszDbContext db, ICurrentUserService currentUser)
    {
        _db = db;
        _currentUser = currentUser;
    }

    public async Task<ErrorOr<NotificationSettingsDto>> Handle(GetNotificationSettingsQuery request, CancellationToken cancellationToken)
    {
        if (!_currentUser.UserId.HasValue)
            return DomainErrors.Auth.InvalidCredentials;

        var userId = _currentUser.UserId.Value;

        var settings = await _db.UserNotificationSettings
            .AsNoTracking()
            .FirstOrDefaultAsync(s => s.UserId == userId, cancellationToken);

        if (settings is null)
        {
            return new NotificationSettingsDto
            {
                PushLike = true,
                PushFollow = true,
                PushSystem = true,
                EmailSecurity = true,
                PushSecurity = false
            };
        }

        return new NotificationSettingsDto
        {
            PushLike = settings.PushLike,
            PushFollow = settings.PushFollow,
            PushSystem = settings.PushSystem,
            EmailSecurity = settings.EmailSecurity,
            PushSecurity = settings.PushSecurity
        };
    }
}
