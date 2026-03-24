using ErrorOr;
using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Smakosz.Application.Common.Errors;
using Smakosz.Application.Common.Interfaces;

namespace Smakosz.Application.Features.Business.Commands.UpdateMenuSection;

public record UpdateMenuSectionCommand(int SectionId, string Name) : IRequest<ErrorOr<Success>>;

public class UpdateMenuSectionHandler : IRequestHandler<UpdateMenuSectionCommand, ErrorOr<Success>>
{
    private readonly ISmakoszDbContext _db;
    private readonly ICurrentUserService _currentUser;

    public UpdateMenuSectionHandler(ISmakoszDbContext db, ICurrentUserService currentUser)
    {
        _db = db;
        _currentUser = currentUser;
    }

    public async Task<ErrorOr<Success>> Handle(UpdateMenuSectionCommand request, CancellationToken cancellationToken)
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

        section.SectionName = request.Name;
        await _db.SaveChangesAsync(cancellationToken);

        return Result.Success;
    }
}

public class UpdateMenuSectionValidator : AbstractValidator<UpdateMenuSectionCommand>
{
    public UpdateMenuSectionValidator()
    {
        RuleFor(x => x.Name)
            .NotEmpty().WithMessage("Nazwa sekcji jest wymagana")
            .MaximumLength(100).WithMessage("Nazwa sekcji może mieć maksymalnie 100 znaków");
    }
}
