using FluentAssertions;
using FluentValidation.TestHelper;
using Smakosz.Application.Features.Reviews.Commands.UpdateReview;

namespace Smakosz.UnitTests.Features.Reviews.Commands.UpdateReview;

[Trait("Category", "Validators")]
public class UpdateReviewValidatorTests
{
    private readonly UpdateReviewValidator _validator = new();

    private static UpdateReviewCommand ValidCommand => new(
        ReviewPublicId: Guid.NewGuid(),
        DishRating: 7,
        ServiceRating: 7,
        CleanlinessRating: 7,
        AmbianceRating: 7,
        Content: null,
        VisitDate: DateOnly.FromDateTime(DateTime.UtcNow.AddDays(-1))
    );

    [Fact]
    public void Validate_ValidCommand_NoErrors()
    {
        var result = _validator.TestValidate(ValidCommand);
        result.ShouldNotHaveAnyValidationErrors();
    }

    [Fact]
    public void Validate_EmptyReviewPublicId_HasError()
    {
        var result = _validator.TestValidate(ValidCommand with { ReviewPublicId = Guid.Empty });
        result.ShouldHaveValidationErrorFor(x => x.ReviewPublicId);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(11)]
    [InlineData(-1)]
    public void Validate_DishRatingOutOfRange_HasError(int rating)
    {
        var result = _validator.TestValidate(ValidCommand with { DishRating = rating });
        result.ShouldHaveValidationErrorFor(x => x.DishRating);
    }

    [Theory]
    [InlineData(1)]
    [InlineData(10)]
    [InlineData(5)]
    public void Validate_DishRatingInRange_NoError(int rating)
    {
        var result = _validator.TestValidate(ValidCommand with { DishRating = rating });
        result.ShouldNotHaveValidationErrorFor(x => x.DishRating);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(11)]
    public void Validate_ServiceRatingOutOfRange_HasError(int rating)
    {
        var result = _validator.TestValidate(ValidCommand with { ServiceRating = rating });
        result.ShouldHaveValidationErrorFor(x => x.ServiceRating);
    }

    [Theory]
    [InlineData(1)]
    [InlineData(10)]
    public void Validate_ServiceRatingInRange_NoError(int rating)
    {
        var result = _validator.TestValidate(ValidCommand with { ServiceRating = rating });
        result.ShouldNotHaveValidationErrorFor(x => x.ServiceRating);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(11)]
    public void Validate_CleanlinessRatingOutOfRange_HasError(int rating)
    {
        var result = _validator.TestValidate(ValidCommand with { CleanlinessRating = rating });
        result.ShouldHaveValidationErrorFor(x => x.CleanlinessRating);
    }

    [Theory]
    [InlineData(1)]
    [InlineData(10)]
    public void Validate_CleanlinessRatingInRange_NoError(int rating)
    {
        var result = _validator.TestValidate(ValidCommand with { CleanlinessRating = rating });
        result.ShouldNotHaveValidationErrorFor(x => x.CleanlinessRating);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(11)]
    public void Validate_AmbianceRatingOutOfRange_HasError(int rating)
    {
        var result = _validator.TestValidate(ValidCommand with { AmbianceRating = rating });
        result.ShouldHaveValidationErrorFor(x => x.AmbianceRating);
    }

    [Theory]
    [InlineData(1)]
    [InlineData(10)]
    public void Validate_AmbianceRatingInRange_NoError(int rating)
    {
        var result = _validator.TestValidate(ValidCommand with { AmbianceRating = rating });
        result.ShouldNotHaveValidationErrorFor(x => x.AmbianceRating);
    }

    [Fact]
    public void Validate_ContentNull_NoError()
    {
        var result = _validator.TestValidate(ValidCommand with { Content = null });
        result.ShouldNotHaveValidationErrorFor(x => x.Content);
    }

    [Fact]
    public void Validate_ContentTooShort_HasError()
    {
        var result = _validator.TestValidate(ValidCommand with { Content = "Short" });
        result.ShouldHaveValidationErrorFor(x => x.Content);
    }

    [Fact]
    public void Validate_ContentMinLength_NoError()
    {
        var result = _validator.TestValidate(ValidCommand with { Content = "1234567890" });
        result.ShouldNotHaveValidationErrorFor(x => x.Content);
    }

    [Fact]
    public void Validate_FutureVisitDate_HasError()
    {
        var result = _validator.TestValidate(ValidCommand with { VisitDate = DateOnly.FromDateTime(DateTime.UtcNow.AddDays(1)) });
        result.ShouldHaveValidationErrorFor(x => x.VisitDate);
    }

    [Fact]
    public void Validate_TodayVisitDate_NoError()
    {
        var result = _validator.TestValidate(ValidCommand with { VisitDate = DateOnly.FromDateTime(DateTime.UtcNow) });
        result.ShouldNotHaveValidationErrorFor(x => x.VisitDate);
    }
}
