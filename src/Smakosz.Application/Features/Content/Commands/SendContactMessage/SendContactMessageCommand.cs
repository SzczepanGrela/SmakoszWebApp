using ErrorOr;
using FluentValidation;
using MediatR;
using Microsoft.Extensions.Logging;
using Smakosz.Application.Common.Interfaces;
using Smakosz.Domain.Entities.System;
using Smakosz.Domain.Enums;

namespace Smakosz.Application.Features.Content.Commands.SendContactMessage;

public record SendContactMessageCommand(
    string Name,
    string Email,
    string Subject,
    string Message) : IRequest<ErrorOr<Success>>;

public class SendContactMessageValidator : AbstractValidator<SendContactMessageCommand>
{
    public SendContactMessageValidator()
    {
        RuleFor(x => x.Name)
            .NotEmpty().WithMessage("Imie jest wymagane")
            .MaximumLength(100).WithMessage("Imie moze miec maksymalnie 100 znakow");

        RuleFor(x => x.Email)
            .NotEmpty().WithMessage("Email jest wymagany")
            .EmailAddress().WithMessage("Nieprawidłowy format adresu email");

        RuleFor(x => x.Subject)
            .NotEmpty().WithMessage("Temat jest wymagany")
            .MaximumLength(200).WithMessage("Temat moze miec maksymalnie 200 znakow");

        RuleFor(x => x.Message)
            .NotEmpty().WithMessage("Wiadomosc jest wymagana")
            .MinimumLength(10).WithMessage("Wiadomosc musi miec co najmniej 10 znakow")
            .MaximumLength(5000).WithMessage("Wiadomosc moze miec maksymalnie 5000 znakow");
    }
}

public class SendContactMessageHandler : IRequestHandler<SendContactMessageCommand, ErrorOr<Success>>
{
    private readonly ISmakoszDbContext _db;
    private readonly IDateTimeProvider _dateTime;
    private readonly IEmailService _email;
    private readonly ILogger<SendContactMessageHandler> _logger;

    public SendContactMessageHandler(
        ISmakoszDbContext db,
        IDateTimeProvider dateTime,
        IEmailService email,
        ILogger<SendContactMessageHandler> logger)
    {
        _db = db;
        _dateTime = dateTime;
        _email = email;
        _logger = logger;
    }

    public async Task<ErrorOr<Success>> Handle(SendContactMessageCommand request, CancellationToken cancellationToken)
    {
        var ticket = new SystemTicket
        {
            TicketType = TicketType.Contact,
            ReferenceId = 0,
            Status = TicketStatus.Open,
            Priority = 3,
            Description = $"Od: {request.Name} <{request.Email}>\nTemat: {request.Subject}\n\n{request.Message}",
            CreatedAt = _dateTime.UtcNow
        };

        _db.SystemTickets.Add(ticket);
        await _db.SaveChangesAsync(cancellationToken);

        // Send confirmation email to user (best-effort, ticket is already saved)
        try
        {
            await _email.SendDigestAsync(request.Email, "Smakosz - potwierdzenie wiadomosci",
                $"""
                <h2>Dziekujemy za kontakt!</h2>
                <p>Czesc {request.Name},</p>
                <p>Otrzymalismy Twoja wiadomosc dotyczaca: <strong>{request.Subject}</strong></p>
                <p>Postaramy sie odpowiedziec jak najszybciej.</p>
                <br/>
                <p>Pozdrawiamy,<br/>Zespol Smakosz</p>
                """, cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to send confirmation email to {Email} for contact ticket #{TicketId}",
                request.Email, ticket.TicketId);
        }

        return Result.Success;
    }
}
