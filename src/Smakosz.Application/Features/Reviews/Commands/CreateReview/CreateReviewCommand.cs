using ErrorOr;
using MediatR;
using Smakosz.Application.Features.Reviews.Dtos;

namespace Smakosz.Application.Features.Reviews.Commands.CreateReview;

public record CreateReviewCommand(
    Guid DishPublicId,
    int DishRating,
    int ServiceRating,
    int CleanlinessRating,
    int AmbianceRating,
    string? Content,
    DateOnly VisitDate
) : IRequest<ErrorOr<ReviewCardDto>>;
