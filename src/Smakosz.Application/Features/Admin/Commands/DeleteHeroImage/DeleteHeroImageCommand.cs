using ErrorOr;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Smakosz.Application.Common.Errors;
using Smakosz.Application.Common.Interfaces;
using Smakosz.Domain.Entities.System;
using Smakosz.Domain.Enums;

namespace Smakosz.Application.Features.Admin.Commands.DeleteHeroImage;

public record DeleteHeroImageCommand(Guid PublicId) : IRequest<ErrorOr<Deleted>>;

public class DeleteHeroImageHandler : IRequestHandler<DeleteHeroImageCommand, ErrorOr<Deleted>>
{
    private const string Bucket = "smakosz-photos";

    private readonly ISmakoszDbContext _db;
    private readonly ICurrentUserService _currentUser;

    public DeleteHeroImageHandler(ISmakoszDbContext db, ICurrentUserService currentUser)
    {
        _db = db;
        _currentUser = currentUser;
    }

    public async Task<ErrorOr<Deleted>> Handle(DeleteHeroImageCommand request, CancellationToken cancellationToken)
    {
        if (_currentUser.Role is not "Admin" and not "Moderator")
            return DomainErrors.Admin.Forbidden;

        var asset = await _db.MediaAssets
            .FirstOrDefaultAsync(a => a.PublicId == request.PublicId, cancellationToken);

        if (asset is null)
            return DomainErrors.Photo.NotFound;

        if (asset.EntityType != MediaEntityType.Hero)
            return DomainErrors.Media.InvalidFormat;

        try
        {
            var key = new Uri(asset.Url).AbsolutePath.TrimStart('/');
            _db.FilesToDelete.Add(new FileToDelete
            {
                R2Key = key,
                Bucket = Bucket,
                Reason = "hero_deleted",
                SourceEntity = "Hero",
                SourceId = (int)asset.AssetId,
                QueuedAt = DateTime.UtcNow
            });
        }
        catch (UriFormatException)
        {
        }

        _db.MediaAssets.Remove(asset);
        await _db.SaveChangesAsync(cancellationToken);

        return Result.Deleted;
    }
}
