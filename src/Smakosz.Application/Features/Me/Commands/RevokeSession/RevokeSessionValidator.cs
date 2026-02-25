using FluentValidation;

namespace Smakosz.Application.Features.Me.Commands.RevokeSession;

public class RevokeSessionValidator : AbstractValidator<RevokeSessionCommand>
{
    public RevokeSessionValidator()
    {
        RuleFor(x => x.SessionId)
            .GreaterThan(0).WithMessage("Identyfikator sesji musi być większy od 0");
    }
}
