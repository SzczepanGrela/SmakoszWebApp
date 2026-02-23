using ErrorOr;
using MediatR;
using Smakosz.Application.Features.Reviews.Dtos;

namespace Smakosz.Application.Features.Reviews.Commands.UpdateReview;

public record UpdateReviewCommand(
    Guid ReviewPublicId,
    int DishRating,
    int ServiceRating,
    int CleanlinessRating,
    int AmbianceRating,
    string? Content,
    DateOnly VisitDate
) : IRequest<ErrorOr<ReviewCardDto>>;
