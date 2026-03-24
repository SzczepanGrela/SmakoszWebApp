using FluentValidation;

namespace Smakosz.Application.Features.Me.Commands.MarkNotificationRead;

public class MarkNotificationReadValidator : AbstractValidator<MarkNotificationReadCommand>
{
    public MarkNotificationReadValidator()
    {
        RuleFor(x => x.PublicId)
            .NotEmpty().WithMessage("Identyfikator powiadomienia jest wymagany");
    }
}
