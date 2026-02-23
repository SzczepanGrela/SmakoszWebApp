using FluentValidation;

namespace Smakosz.Application.Features.Reviews.Commands.UpdateReview;

public class UpdateReviewValidator : AbstractValidator<UpdateReviewCommand>
{
    public UpdateReviewValidator()
    {
        RuleFor(x => x.ReviewPublicId)
            .NotEmpty().WithMessage("Identyfikator recenzji jest wymagany");

        RuleFor(x => x.DishRating)
            .InclusiveBetween(1, 10).WithMessage("Ocena dania musi być w zakresie 1-10");

        RuleFor(x => x.ServiceRating)
            .InclusiveBetween(1, 10).WithMessage("Ocena obsługi musi być w zakresie 1-10");

        RuleFor(x => x.CleanlinessRating)
            .InclusiveBetween(1, 10).WithMessage("Ocena czystości musi być w zakresie 1-10");

        RuleFor(x => x.AmbianceRating)
            .InclusiveBetween(1, 10).WithMessage("Ocena atmosfery musi być w zakresie 1-10");

        RuleFor(x => x.Content)
            .MinimumLength(10).When(x => !string.IsNullOrEmpty(x.Content))
            .WithMessage("Treść recenzji musi mieć co najmniej 10 znaków");

        RuleFor(x => x.VisitDate)
            .NotEmpty().WithMessage("Data wizyty jest wymagana")
            .LessThanOrEqualTo(DateOnly.FromDateTime(DateTime.UtcNow))
            .WithMessage("Data wizyty nie może być w przyszłości");
    }
}
