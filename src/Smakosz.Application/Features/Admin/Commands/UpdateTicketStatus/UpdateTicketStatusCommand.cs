using ErrorOr;
using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Smakosz.Application.Common.Errors;
using Smakosz.Application.Common.Interfaces;
using Smakosz.Domain.Enums;

namespace Smakosz.Application.Features.Admin.Commands.UpdateTicketStatus;

public record UpdateTicketStatusCommand(int TicketId, string Status) : IRequest<ErrorOr<Success>>;

public class UpdateTicketStatusValidator : AbstractValidator<UpdateTicketStatusCommand>
{
    public UpdateTicketStatusValidator()
    {
        RuleFor(x => x.TicketId)
            .GreaterThan(0).WithMessage("Identyfikator zgłoszenia musi być większy od 0");

        RuleFor(x => x.Status)
            .NotEmpty().WithMessage("Status jest wymagany");
    }
}

public class UpdateTicketStatusHandler : IRequestHandler<UpdateTicketStatusCommand, ErrorOr<Success>>
{
    private readonly ISmakoszDbContext _db;
    private readonly ICurrentUserService _currentUser;

    public UpdateTicketStatusHandler(ISmakoszDbContext db, ICurrentUserService currentUser)
    {
        _db = db;
        _currentUser = currentUser;
    }

    public async Task<ErrorOr<Success>> Handle(UpdateTicketStatusCommand request, CancellationToken cancellationToken)
    {
        if (_currentUser.Role is not "Admin" and not "Moderator")
            return DomainErrors.Admin.Forbidden;

        var ticket = await _db.SystemTickets
            .FirstOrDefaultAsync(t => t.TicketId == request.TicketId, cancellationToken);

        if (ticket is null)
            return DomainErrors.Ticket.NotFound;

        if (!Enum.TryParse<TicketStatus>(request.Status, true, out var statusEnum))
            return DomainErrors.Ticket.InvalidStatus;

        ticket.Status = statusEnum;
        await _db.SaveChangesAsync(cancellationToken);

        return Result.Success;
    }
}
