using ErrorOr;
using MediatR;

namespace Smakosz.Application.Features.Reviews.Commands.DeleteReview;

public record DeleteReviewCommand(Guid ReviewPublicId) : IRequest<ErrorOr<Deleted>>;
