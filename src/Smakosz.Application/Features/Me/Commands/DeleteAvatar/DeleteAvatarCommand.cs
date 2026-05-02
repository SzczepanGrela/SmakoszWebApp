using ErrorOr;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Smakosz.Application.Common.Errors;
using Smakosz.Application.Common.Interfaces;
using Smakosz.Domain.Entities.System;
using Smakosz.Domain.Enums;

namespace Smakosz.Application.Features.Me.Commands.DeleteAvatar;

public record DeleteAvatarCommand : IRequest<ErrorOr<Deleted>>;

public class DeleteAvatarHandler : IRequestHandler<DeleteAvatarCommand, ErrorOr<Deleted>>
{
    private const string Bucket = "smakosz-photos";

    private readonly ISmakoszDbContext _db;
    private readonly ICurrentUserService _currentUser;

    public DeleteAvatarHandler(ISmakoszDbContext db, ICurrentUserService currentUser)
    {
        _db = db;
        _currentUser = currentUser;
    }

    public async Task<ErrorOr<Deleted>> Handle(DeleteAvatarCommand request, CancellationToken cancellationToken)
    {
        if (!_currentUser.UserId.HasValue)
            return DomainErrors.Auth.InvalidCredentials;

        var userId = _currentUser.UserId.Value;
        var user = await _db.Users.FirstOrDefaultAsync(u => u.UserId == userId, cancellationToken);
        if (user is null)
            return DomainErrors.Auth.InvalidCredentials;

        if (string.IsNullOrEmpty(user.AvatarUrl))
            return Result.Deleted;

        var asset = await _db.MediaAssets
            .FirstOrDefaultAsync(a => a.EntityType == MediaEntityType.User && a.EntityId == userId, cancellationToken);

        try
        {
            var key = new Uri(user.AvatarUrl).AbsolutePath.TrimStart('/');
            _db.FilesToDelete.Add(new FileToDelete
            {
                R2Key = key,
                Bucket = Bucket,
                Reason = "avatar_deleted",
                SourceEntity = "User",
                SourceId = userId,
                QueuedAt = DateTime.UtcNow
            });
        }
        catch (UriFormatException)
        {
        }

        if (asset is not null)
            _db.MediaAssets.Remove(asset);

        user.AvatarUrl = null;
        user.AvatarBlurhash = null;

        await _db.SaveChangesAsync(cancellationToken);

        return Result.Deleted;
    }
}
