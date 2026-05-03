using ErrorOr;
using FluentValidation;
using MediatR;

namespace Smakosz.Application.Features.Admin.Commands.RejectNewRestaurantRequest;

public record RejectNewRestaurantRequestCommand(int TicketId, string Reason) : IRequest<ErrorOr<Success>>;

public class RejectNewRestaurantRequestValidator : AbstractValidator<RejectNewRestaurantRequestCommand>
{
    public RejectNewRestaurantRequestValidator()
    {
        RuleFor(x => x.TicketId).GreaterThan(0);
        RuleFor(x => x.Reason)
            .NotEmpty().WithMessage("Powod odrzucenia jest wymagany")
            .MinimumLength(10).WithMessage("Powod musi miec co najmniej 10 znakow")
            .MaximumLength(1000).WithMessage("Powod moze miec maksymalnie 1000 znakow");
    }
}
