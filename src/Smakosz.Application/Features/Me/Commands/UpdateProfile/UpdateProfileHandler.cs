using ErrorOr;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Smakosz.Application.Common.Errors;
using Smakosz.Application.Common.Interfaces;
using Smakosz.Domain.Enums;

namespace Smakosz.Application.Features.Me.Commands.UpdateProfile;

public class UpdateProfileHandler : IRequestHandler<UpdateProfileCommand, ErrorOr<Success>>
{
    private readonly ISmakoszDbContext _db;
    private readonly ICurrentUserService _currentUser;

    public UpdateProfileHandler(ISmakoszDbContext db, ICurrentUserService currentUser)
    {
        _db = db;
        _currentUser = currentUser;
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
            var usernameLower = request.Username.ToLowerInvariant();
            var forbiddenUsername = await _db.ForbiddenWords
                .Where(fw => fw.Category == ForbiddenWordCategory.Reserved || fw.Category == ForbiddenWordCategory.Offensive)
                .AnyAsync(fw => !fw.IsRegex && usernameLower.Contains(fw.Word.ToLower()), cancellationToken);

            if (forbiddenUsername)
                return DomainErrors.ForbiddenWord.UsernameContainsForbiddenWord;

            var slugCandidate = request.Username.ToLowerInvariant().Replace(" ", "-");

            var slugTaken = await _db.Users.AnyAsync(
                u => u.Slug == slugCandidate && u.UserId != user.UserId, cancellationToken);

            if (slugTaken)
                return DomainErrors.User.UsernameAlreadyExists;

            user.Username = request.Username;
            user.Slug = slugCandidate;
        }

        await _db.SaveChangesAsync(cancellationToken);
        return Result.Success;
    }
}
