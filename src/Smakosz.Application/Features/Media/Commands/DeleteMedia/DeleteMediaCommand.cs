using ErrorOr;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Smakosz.Application.Common.Errors;
using Smakosz.Application.Common.Interfaces;
using Smakosz.Domain.Enums;

namespace Smakosz.Application.Features.Media.Commands.DeleteMedia;

public record DeleteMediaCommand(Guid PublicId) : IRequest<ErrorOr<Deleted>>;

public class DeleteMediaHandler : IRequestHandler<DeleteMediaCommand, ErrorOr<Deleted>>
{
    private readonly ISmakoszDbContext _db;
    private readonly ICurrentUserService _currentUser;
    private readonly IFileStorageService _storage;

    public DeleteMediaHandler(ISmakoszDbContext db, ICurrentUserService currentUser, IFileStorageService storage)
    {
        _db = db;
        _currentUser = currentUser;
        _storage = storage;
    }

    public async Task<ErrorOr<Deleted>> Handle(DeleteMediaCommand request, CancellationToken cancellationToken)
    {
        if (!_currentUser.UserId.HasValue)
            return DomainErrors.Auth.InvalidCredentials;

        var asset = await _db.MediaAssets
            .FirstOrDefaultAsync(m => m.PublicId == request.PublicId, cancellationToken);

        if (asset is null)
            return DomainErrors.Photo.NotFound;

        if (!_currentUser.IsAdmin && asset.UploadedBy != _currentUser.UserId.Value)
            return DomainErrors.Admin.Forbidden;

        try
        {
            await _storage.DeleteAsync(asset.Url, cancellationToken);
        }
        catch
        {
            // Storage deletion is best-effort; continue with DB removal
        }

        _db.MediaAssets.Remove(asset);
        await _db.SaveChangesAsync(cancellationToken);

        return Result.Deleted;
    }
}
