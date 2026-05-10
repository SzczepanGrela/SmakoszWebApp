using ErrorOr;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Smakosz.Application.Common.Errors;
using Smakosz.Application.Common.Interfaces;
using Smakosz.Domain.Entities.System;
using Smakosz.Domain.Enums;

namespace Smakosz.Application.Features.Me.Commands.UpdateProfile;

public class UpdateProfileHandler : IRequestHandler<UpdateProfileCommand, ErrorOr<Success>>
{
    private readonly ISmakoszDbContext _db;
    private readonly ICurrentUserService _currentUser;
    private readonly IForbiddenWordService _forbiddenWords;

    public UpdateProfileHandler(ISmakoszDbContext db, ICurrentUserService currentUser, IForbiddenWordService forbiddenWords)
    {
        _db = db;
        _currentUser = currentUser;
        _forbiddenWords = forbiddenWords;
    }

    public async Task<ErrorOr<Success>> Handle(UpdateProfileCommand request, CancellationToken cancellationToken)
    {
        if (!_currentUser.UserId.HasValue)
            return DomainErrors.Auth.InvalidCredentials;

        var user = await _db.Users
            .FirstOrDefaultAsync(u => u.UserId == _currentUser.UserId.Value && !u.IsDeleted, cancellationToken);

        if (user is null)
            return DomainErrors.User.NotFound;

        if (request.Username is not null)
        {
            if (await _forbiddenWords.ContainsAsync(request.Username, cancellationToken, ForbiddenWordCategory.Profanity, ForbiddenWordCategory.Reserved, ForbiddenWordCategory.Offensive))
                return DomainErrors.ForbiddenWord.UsernameContainsForbiddenWord;

            var slugCandidate = request.Username.ToLowerInvariant().Replace(" ", "-");

            var slugTaken = await _db.Users.AnyAsync(
                u => u.Slug == slugCandidate && u.UserId != user.UserId, cancellationToken);

            if (slugTaken)
                return DomainErrors.User.UsernameAlreadyExists;

            user.Username = request.Username;
            user.Slug = slugCandidate;
        }

        if (request.AvatarUrl is not null && request.AvatarUrl != user.AvatarUrl)
        {
            if (user.AvatarUrl is not null)
            {
                _db.FilesToDelete.Add(new FileToDelete
                {
                    R2Key = new Uri(user.AvatarUrl).AbsolutePath.TrimStart('/'),
                    Bucket = "smakosz-photos",
                    Reason = "avatar_replaced",
                    SourceEntity = "User",
                    SourceId = user.UserId,
                    QueuedAt = DateTime.UtcNow
                });
            }

            user.AvatarUrl = request.AvatarUrl;
        }

        await _db.SaveChangesAsync(cancellationToken);
        return Result.Success;
    }
}
