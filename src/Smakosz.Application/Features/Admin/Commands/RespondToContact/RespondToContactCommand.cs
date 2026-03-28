using System.Text.RegularExpressions;
using ErrorOr;
using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Smakosz.Application.Common.Errors;
using Smakosz.Application.Common.Interfaces;
using Smakosz.Domain.Entities.System;
using Smakosz.Domain.Enums;

namespace Smakosz.Application.Features.Admin.Commands.RespondToContact;

public record RespondToContactCommand(int TicketId, string Response) : IRequest<ErrorOr<Success>>;

public class RespondToContactValidator : AbstractValidator<RespondToContactCommand>
{
    public RespondToContactValidator()
    {
        RuleFor(x => x.TicketId).GreaterThan(0);
        RuleFor(x => x.Response).NotEmpty().MaximumLength(5000);
    }
}

public class RespondToContactHandler : IRequestHandler<RespondToContactCommand, ErrorOr<Success>>
{
    private readonly ISmakoszDbContext _db;
    private readonly ICurrentUserService _currentUser;
    private readonly IEmailService _email;
    private readonly IDateTimeProvider _dateTime;

    public RespondToContactHandler(
        ISmakoszDbContext db,
        ICurrentUserService currentUser,
        IEmailService email,
        IDateTimeProvider dateTime)
    {
        _db = db;
        _currentUser = currentUser;
        _email = email;
        _dateTime = dateTime;
    }

    public async Task<ErrorOr<Success>> Handle(RespondToContactCommand request, CancellationToken cancellationToken)
    {
        if (_currentUser.Role is not "Admin" and not "Moderator")
            return DomainErrors.Admin.Forbidden;

        var ticket = await _db.SystemTickets
            .FirstOrDefaultAsync(t => t.TicketId == request.TicketId, cancellationToken);

        if (ticket is null)
            return DomainErrors.Ticket.NotFound;

        if (ticket.TicketType != TicketType.Contact)
            return Error.Validation("TICKET_NOT_CONTACT", "To zgłoszenie nie jest typu Kontakt");

        if (ticket.Status is TicketStatus.Resolved or TicketStatus.Closed)
            return Error.Validation("TICKET_ALREADY_RESOLVED", "To zgłoszenie zostało już rozwiązane");

        var emailMatch = Regex.Match(ticket.Description ?? string.Empty, @"<(.+?)>");
        if (!emailMatch.Success)
            return Error.Unexpected("CONTACT_EMAIL_NOT_FOUND", "Nie udało się odczytać adresu email ze zgłoszenia");

        var contactEmail = emailMatch.Groups[1].Value;

        await _email.SendDigestAsync(contactEmail, "Smakosz - odpowiedź na Twoją wiadomość",
            $"""
            <h2>Odpowiedź na Twoją wiadomość</h2>
            <p>{request.Response.Replace("\n", "<br/>")}</p>
            <br/>
            <p>Pozdrawiamy,<br/>Zespół Smakosz</p>
            """, cancellationToken);

        _db.ModerationLogs.Add(new ModerationLog
        {
            EntityType = ModerationEntityType.Ticket,
            EntityId = ticket.TicketId,
            Actor = ModerationActor.Admin,
            Verdict = ModerationVerdict.Resolved,
            AdminNote = request.Response,
            ProcessedBy = _currentUser.UserId,
            CreatedAt = _dateTime.UtcNow
        });

        ticket.Status = TicketStatus.Resolved;
        ticket.AssignedAdminId = _currentUser.UserId;

        await _db.SaveChangesAsync(cancellationToken);

        return Result.Success;
    }
}
