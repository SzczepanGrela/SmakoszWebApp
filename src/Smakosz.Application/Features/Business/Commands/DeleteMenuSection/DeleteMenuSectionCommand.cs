using ErrorOr;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Smakosz.Application.Common.Errors;
using Smakosz.Application.Common.Interfaces;

namespace Smakosz.Application.Features.Business.Commands.DeleteMenuSection;

public record DeleteMenuSectionCommand(int SectionId) : IRequest<ErrorOr<Success>>;

public class DeleteMenuSectionHandler : IRequestHandler<DeleteMenuSectionCommand, ErrorOr<Success>>
{
    private readonly ISmakoszDbContext _db;
    private readonly ICurrentUserService _currentUser;

    public DeleteMenuSectionHandler(ISmakoszDbContext db, ICurrentUserService currentUser)
    {
        _db = db;
        _currentUser = currentUser;
    }

    public async Task<ErrorOr<Success>> Handle(DeleteMenuSectionCommand request, CancellationToken cancellationToken)
    {
        if (!_currentUser.UserId.HasValue)
            return DomainErrors.Auth.InvalidCredentials;

        var section = await _db.MenuSections
            .Include(ms => ms.Restaurant)
            .FirstOrDefaultAsync(ms => ms.SectionId == request.SectionId, cancellationToken);

        if (section is null)
            return DomainErrors.MenuSection.NotFound;

        if (section.Restaurant.OwnerId != _currentUser.UserId.Value)
            return DomainErrors.MenuSection.NotOwner;

        _db.MenuSections.Remove(section);
        await _db.SaveChangesAsync(cancellationToken);

        return Result.Success;
    }
}
