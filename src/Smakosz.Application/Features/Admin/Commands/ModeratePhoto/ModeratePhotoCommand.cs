using ErrorOr;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Smakosz.Application.Common.Errors;
using Smakosz.Application.Common.Interfaces;
using Smakosz.Domain.Enums;

namespace Smakosz.Application.Features.Admin.Commands.ModeratePhoto;

public record ModeratePhotoCommand(Guid PublicId, bool Approve, string? RejectionReason) : IRequest<ErrorOr<Success>>;

public class ModeratePhotoHandler : IRequestHandler<ModeratePhotoCommand, ErrorOr<Success>>
{
    private readonly ISmakoszDbContext _db;
    private readonly ICurrentUserService _currentUser;

    public ModeratePhotoHandler(ISmakoszDbContext db, ICurrentUserService currentUser)
    {
        _db = db;
        _currentUser = currentUser;
    }

    public async Task<ErrorOr<Success>> Handle(ModeratePhotoCommand request, CancellationToken cancellationToken)
    {
        if (!_currentUser.IsAdmin)
            return DomainErrors.Admin.Forbidden;

        var asset = await _db.MediaAssets
            .FirstOrDefaultAsync(a => a.PublicId == request.PublicId, cancellationToken);

        if (asset is null)
            return DomainErrors.Photo.NotFound;

        asset.Status = request.Approve ? MediaAssetStatus.Approved : MediaAssetStatus.Rejected;

        if (!request.Approve)
            asset.RejectionReason = request.RejectionReason;

        await _db.SaveChangesAsync(cancellationToken);

        return Result.Success;
    }
}
