using ErrorOr;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Smakosz.Application.Common.Errors;
using Smakosz.Application.Common.Interfaces;

namespace Smakosz.Application.Features.Me.Commands.ChangePassword;

public record ChangePasswordCommand(string CurrentPassword, string NewPassword) : IRequest<ErrorOr<Success>>;

public class ChangePasswordHandler : IRequestHandler<ChangePasswordCommand, ErrorOr<Success>>
{
    private readonly ISmakoszDbContext _db;
    private readonly ICurrentUserService _currentUser;
    private readonly IPasswordHasher _passwordHasher;

    public ChangePasswordHandler(ISmakoszDbContext db, ICurrentUserService currentUser, IPasswordHasher passwordHasher)
    {
        _db = db;
        _currentUser = currentUser;
        _passwordHasher = passwordHasher;
    }

    public async Task<ErrorOr<Success>> Handle(ChangePasswordCommand request, CancellationToken cancellationToken)
    {
        if (!_currentUser.UserId.HasValue)
            return DomainErrors.Auth.InvalidCredentials;

        var user = await _db.Users
            .FirstOrDefaultAsync(u => u.UserId == _currentUser.UserId.Value && !u.IsDeleted, cancellationToken);

        if (user is null)
            return DomainErrors.User.NotFound;

        if (!_passwordHasher.Verify(request.CurrentPassword, user.PasswordHash))
            return DomainErrors.Auth.InvalidCredentials;

        user.PasswordHash = _passwordHasher.Hash(request.NewPassword);
        user.SecurityStamp = Guid.NewGuid().ToString();
        await _db.SaveChangesAsync(cancellationToken);

        return Result.Success;
    }
}
