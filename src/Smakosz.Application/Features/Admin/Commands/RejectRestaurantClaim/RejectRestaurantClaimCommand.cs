using ErrorOr;
using FluentValidation;
using MediatR;

namespace Smakosz.Application.Features.Admin.Commands.RejectRestaurantClaim;

public record RejectRestaurantClaimCommand(int TicketId, string Reason) : IRequest<ErrorOr<Success>>;

public class RejectRestaurantClaimValidator : AbstractValidator<RejectRestaurantClaimCommand>
{
    public RejectRestaurantClaimValidator()
    {
        RuleFor(x => x.TicketId).GreaterThan(0);
        RuleFor(x => x.Reason)
            .NotEmpty().WithMessage("Powod odrzucenia jest wymagany")
            .MinimumLength(10).WithMessage("Powod musi miec co najmniej 10 znakow")
            .MaximumLength(1000).WithMessage("Powod moze miec maksymalnie 1000 znakow");
    }
}
