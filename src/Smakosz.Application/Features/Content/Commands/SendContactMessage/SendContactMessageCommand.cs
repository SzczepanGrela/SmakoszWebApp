using ErrorOr;
using FluentValidation;
using MediatR;
using Microsoft.Extensions.Logging;
using Smakosz.Application.Common.Errors;
using Smakosz.Application.Common.Interfaces;
using Smakosz.Domain.Entities.System;
using Smakosz.Domain.Enums;

namespace Smakosz.Application.Features.Content.Commands.SendContactMessage;

public record SendContactMessageCommand(
    string Name,
    string Email,
    string Subject,
    string Message,
    string? TurnstileToken = null) : IRequest<ErrorOr<Success>>;

public class SendContactMessageValidator : AbstractValidator<SendContactMessageCommand>
{
    public SendContactMessageValidator()
    {
        RuleFor(x => x.Name)
            .NotEmpty().WithMessage("Imię jest wymagane")
            .MaximumLength(100).WithMessage("Imię może mieć maksymalnie 100 znaków");

        RuleFor(x => x.Email)
            .NotEmpty().WithMessage("Email jest wymagany")
            .EmailAddress().WithMessage("Nieprawidłowy format adresu email");

        RuleFor(x => x.Subject)
            .NotEmpty().WithMessage("Temat jest wymagany")
            .MaximumLength(200).WithMessage("Temat może mieć maksymalnie 200 znaków");

        RuleFor(x => x.Message)
            .NotEmpty().WithMessage("Wiadomość jest wymagana")
            .MinimumLength(10).WithMessage("Wiadomość musi mieć co najmniej 10 znaków")
            .MaximumLength(5000).WithMessage("Wiadomość może mieć maksymalnie 5000 znaków");
    }
}

public class SendContactMessageHandler : IRequestHandler<SendContactMessageCommand, ErrorOr<Success>>
{
    private readonly ISmakoszDbContext _db;
    private readonly IDateTimeProvider _dateTime;
    private readonly IEmailService _email;
    private readonly ILogger<SendContactMessageHandler> _logger;
    private readonly ITurnstileService _turnstile;

    public SendContactMessageHandler(
        ISmakoszDbContext db,
        IDateTimeProvider dateTime,
        IEmailService email,
        ILogger<SendContactMessageHandler> logger,
        ITurnstileService turnstile)
    {
        _db = db;
        _dateTime = dateTime;
        _email = email;
        _logger = logger;
        _turnstile = turnstile;
    }

    public async Task<ErrorOr<Success>> Handle(SendContactMessageCommand request, CancellationToken cancellationToken)
    {
        if (!await _turnstile.VerifyAsync(request.TurnstileToken ?? string.Empty, cancellationToken))
        {
            return DomainErrors.Captcha.VerificationFailed;
        }

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

        try
        {
            await _email.SendContactConfirmationAsync(request.Email, request.Name, request.Subject, cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to send confirmation email to {Email} for contact ticket #{TicketId}",
                request.Email, ticket.TicketId);
        }

        return Result.Success;
    }
}
