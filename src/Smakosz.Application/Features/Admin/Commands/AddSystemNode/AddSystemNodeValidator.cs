using FluentValidation;

namespace Smakosz.Application.Features.Admin.Commands.AddSystemNode;

public class AddSystemNodeValidator : AbstractValidator<AddSystemNodeCommand>
{
    public AddSystemNodeValidator()
    {
        RuleFor(x => x.NodeId)
            .NotEmpty()
            .MaximumLength(64)
            .Matches("^[a-z0-9-]+$").WithMessage("NodeId: tylko małe litery, cyfry, myślnik");

        RuleFor(x => x.NodeType)
            .NotEmpty()
            .Must(t => string.Equals(t, "gpu", StringComparison.OrdinalIgnoreCase)
                       || string.Equals(t, "orchestrator", StringComparison.OrdinalIgnoreCase)
                       || string.Equals(t, "api", StringComparison.OrdinalIgnoreCase)
                       || string.Equals(t, "rbpigateway", StringComparison.OrdinalIgnoreCase))
            .WithMessage("NodeType musi być gpu/orchestrator/api/rbpigateway");

        When(x => string.Equals(x.NodeType, "gpu", StringComparison.OrdinalIgnoreCase), () =>
        {
            RuleFor(x => x.MacAddress)
                .NotEmpty()
                .Matches("^([0-9A-Fa-f]{2}:){5}[0-9A-Fa-f]{2}$")
                .WithMessage("MacAddress: format AA:BB:CC:DD:EE:FF");
            RuleFor(x => x.WolGatewayId).NotEmpty();
        });
    }
}
