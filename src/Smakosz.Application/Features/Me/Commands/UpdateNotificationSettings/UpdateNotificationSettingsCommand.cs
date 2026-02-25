using ErrorOr;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Smakosz.Application.Common.Errors;
using Smakosz.Application.Common.Interfaces;
using Smakosz.Domain.Entities;

namespace Smakosz.Application.Features.Me.Commands.UpdateNotificationSettings;

public record UpdateNotificationSettingsCommand(bool PushLike, bool PushFollow, bool PushSystem) : IRequest<ErrorOr<Success>>;

public class UpdateNotificationSettingsHandler : IRequestHandler<UpdateNotificationSettingsCommand, ErrorOr<Success>>
{
    private readonly ISmakoszDbContext _db;
    private readonly ICurrentUserService _currentUser;

    public UpdateNotificationSettingsHandler(ISmakoszDbContext db, ICurrentUserService currentUser)
    {
        _db = db;
        _currentUser = currentUser;
    }

    public async Task<ErrorOr<Success>> Handle(UpdateNotificationSettingsCommand request, CancellationToken cancellationToken)
    {
        if (!_currentUser.UserId.HasValue)
            return DomainErrors.Auth.InvalidCredentials;

        var userId = _currentUser.UserId.Value;

        var settings = await _db.UserNotificationSettings
            .FirstOrDefaultAsync(s => s.UserId == userId, cancellationToken);

        if (settings is null)
        {
            settings = new UserNotificationSettings
            {
                UserId = userId,
                PushLike = request.PushLike,
                PushFollow = request.PushFollow,
                PushSystem = request.PushSystem,
                UpdatedAt = DateTime.UtcNow
            };
            _db.UserNotificationSettings.Add(settings);
        }
        else
        {
            settings.PushLike = request.PushLike;
            settings.PushFollow = request.PushFollow;
            settings.PushSystem = request.PushSystem;
            settings.UpdatedAt = DateTime.UtcNow;
        }

        await _db.SaveChangesAsync(cancellationToken);
        return Result.Success;
    }
}
