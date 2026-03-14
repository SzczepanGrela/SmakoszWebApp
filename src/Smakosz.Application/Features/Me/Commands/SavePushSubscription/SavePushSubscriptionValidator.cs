using FluentValidation;

namespace Smakosz.Application.Features.Me.Commands.SavePushSubscription;

public class SavePushSubscriptionValidator : AbstractValidator<SavePushSubscriptionCommand>
{
    public SavePushSubscriptionValidator()
    {
        RuleFor(x => x.Endpoint)
            .NotEmpty().WithMessage("Endpoint jest wymagany")
            .MaximumLength(2048);

        RuleFor(x => x.P256dh)
            .NotEmpty().WithMessage("Klucz P256dh jest wymagany")
            .MaximumLength(512);

        RuleFor(x => x.Auth)
            .NotEmpty().WithMessage("Klucz Auth jest wymagany")
            .MaximumLength(512);

        RuleFor(x => x.DeviceName)
            .MaximumLength(200);
    }
}
