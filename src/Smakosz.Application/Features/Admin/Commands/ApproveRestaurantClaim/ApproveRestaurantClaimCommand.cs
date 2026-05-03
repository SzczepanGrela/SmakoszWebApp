using ErrorOr;
using MediatR;

namespace Smakosz.Application.Features.Admin.Commands.ApproveRestaurantClaim;

public record ApproveRestaurantClaimCommand(int TicketId) : IRequest<ErrorOr<Success>>;
