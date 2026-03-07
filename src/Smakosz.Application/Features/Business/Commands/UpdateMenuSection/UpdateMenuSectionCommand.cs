using ErrorOr;
using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Smakosz.Application.Common.Errors;
using Smakosz.Application.Common.Interfaces;
using Smakosz.Domain.Entities;
using Smakosz.Domain.Entities.System;
using Smakosz.Domain.Enums;

namespace Smakosz.Application.Features.Business.Commands.UpdateMenuSection;

public record UpdateMenuSectionCommand(int SectionId, string Name) : IRequest<ErrorOr<Success>>;

public class UpdateMenuSectionHandler : IRequestHandler<UpdateMenuSectionCommand, ErrorOr<Success>>
{
    private readonly ISmakoszDbContext _db;
    private readonly ICurrentUserService _currentUser;
    private readonly IForbiddenWordService _forbiddenWords;

    public UpdateMenuSectionHandler(ISmakoszDbContext db, ICurrentUserService currentUser, IForbiddenWordService forbiddenWords)
    {
        _db = db;
        _currentUser = currentUser;
        _forbiddenWords = forbiddenWords;
    }

    public async Task<ErrorOr<Success>> Handle(UpdateMenuSectionCommand request, CancellationToken cancellationToken)
    {
        if (!_currentUser.UserId.HasValue)
            return DomainErrors.Auth.InvalidCredentials;

        if (await _forbiddenWords.ContainsAsync(request.Name, cancellationToken, ForbiddenWordCategory.Profanity, ForbiddenWordCategory.Offensive))
            return DomainErrors.ForbiddenWord.ContentContainsForbiddenWord;

        var section = await _db.MenuSections
            .Include(ms => ms.Restaurant)
            .FirstOrDefaultAsync(ms => ms.SectionId == request.SectionId, cancellationToken);

        if (section is null)
            return DomainErrors.MenuSection.NotFound;

        if (section.Restaurant.OwnerId != _currentUser.UserId.Value)
            return DomainErrors.MenuSection.NotOwner;

        var editRequest = new RestaurantEditRequest
        {
            RestaurantId = section.RestaurantId,
            UserId = _currentUser.UserId.Value,
            ChangeType = EditRequestChangeType.SectionUpdate,
            ChangeScope = EditRequestChangeScope.Section,
            TargetEntityId = section.SectionId,
            Payload = "{}",
            NewName = request.Name,
            Status = EditRequestStatus.Pending,
            ModerationStatus = ContentModerationStatus.Pending,
            CreatedAt = DateTime.UtcNow
        };

        _db.RestaurantEditRequests.Add(editRequest);
        await _db.SaveChangesAsync(cancellationToken);

        _db.SystemTickets.Add(new SystemTicket
        {
            TicketType = TicketType.EditRequest,
            ReferenceId = editRequest.RequestId,
            Status = TicketStatus.Open,
            Priority = 3,
            Description = $"Edycja sekcji menu \"{section.SectionName}\" (via UpdateMenuSection)"
        });

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
