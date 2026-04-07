using FluentValidation;
using Smakosz.Application.Common.Interfaces;

namespace Smakosz.Application.Features.Reviews.Commands.CreateReview;

public class CreateReviewValidator : AbstractValidator<CreateReviewCommand>
{
    public CreateReviewValidator(IValidationConfigProvider config)
    {
        var minLength = config.GetInt("review.min_length", 10);
        var maxLength = config.GetInt("review.max_length", 2000);

        RuleFor(x => x.DishPublicId)
            .NotEmpty().WithMessage("Identyfikator dania jest wymagany");

        RuleFor(x => x.DishRating)
            .InclusiveBetween(1, 10).WithMessage("Ocena dania musi być w zakresie 1-10");

        RuleFor(x => x.ServiceRating)
            .InclusiveBetween(1, 10).WithMessage("Ocena obsługi musi być w zakresie 1-10");

        RuleFor(x => x.CleanlinessRating)
            .InclusiveBetween(1, 10).WithMessage("Ocena czystości musi być w zakresie 1-10");

        RuleFor(x => x.AmbianceRating)
            .InclusiveBetween(1, 10).WithMessage("Ocena atmosfery musi być w zakresie 1-10");

        RuleFor(x => x.Content)
            .MinimumLength(minLength).When(x => !string.IsNullOrEmpty(x.Content))
            .WithMessage($"Treść recenzji musi mieć co najmniej {minLength} znaków");

        RuleFor(x => x.Content)
            .MaximumLength(maxLength).When(x => !string.IsNullOrEmpty(x.Content))
            .WithMessage($"Treść recenzji może mieć maksymalnie {maxLength} znaków");

        RuleFor(x => x.VisitDate)
            .NotEmpty().WithMessage("Data wizyty jest wymagana")
            .LessThanOrEqualTo(DateOnly.FromDateTime(DateTime.UtcNow))
            .WithMessage("Data wizyty nie może być w przyszłości");
    }
}
