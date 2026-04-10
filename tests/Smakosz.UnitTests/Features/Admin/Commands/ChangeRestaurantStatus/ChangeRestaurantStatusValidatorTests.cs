using FluentValidation.TestHelper;
using Smakosz.Application.Features.Admin.Commands.ChangeRestaurantStatus;
using Smakosz.Domain.Enums;

namespace Smakosz.UnitTests.Features.Admin.Commands.ChangeRestaurantStatus;

[Trait("Category", "Validators")]
public class ChangeRestaurantStatusValidatorTests
{
    private readonly ChangeRestaurantStatusValidator _validator = new();

    [Fact]
    public void Suspended_WithoutReason_Fails()
    {
        var cmd = new ChangeRestaurantStatusCommand(Guid.NewGuid(), RestaurantStatus.Suspended, null);
        var result = _validator.TestValidate(cmd);
        result.ShouldHaveValidationErrorFor(x => x.Reason);
    }

    [Fact]
    public void Suspended_WithShortReason_Fails()
    {
        var cmd = new ChangeRestaurantStatusCommand(Guid.NewGuid(), RestaurantStatus.Suspended, "ab");
        var result = _validator.TestValidate(cmd);
        result.ShouldHaveValidationErrorFor(x => x.Reason);
    }

    [Fact]
    public void Suspended_WithValidReason_Passes()
    {
        var cmd = new ChangeRestaurantStatusCommand(Guid.NewGuid(), RestaurantStatus.Suspended, "Naruszenie regulaminu");
        var result = _validator.TestValidate(cmd);
        result.ShouldNotHaveValidationErrorFor(x => x.Reason);
    }

    [Fact]
    public void ClosedPermanently_WithoutReason_Fails()
    {
        var cmd = new ChangeRestaurantStatusCommand(Guid.NewGuid(), RestaurantStatus.ClosedPermanently, null);
        var result = _validator.TestValidate(cmd);
        result.ShouldHaveValidationErrorFor(x => x.Reason);
    }

    [Fact]
    public void Active_WithoutReason_Passes()
    {
        var cmd = new ChangeRestaurantStatusCommand(Guid.NewGuid(), RestaurantStatus.Active, null);
        var result = _validator.TestValidate(cmd);
        result.ShouldNotHaveValidationErrorFor(x => x.Reason);
    }

    [Fact]
    public void InvalidEnum_Fails()
    {
        var cmd = new ChangeRestaurantStatusCommand(Guid.NewGuid(), (RestaurantStatus)999, null);
        var result = _validator.TestValidate(cmd);
        result.ShouldHaveValidationErrorFor(x => x.NewStatus);
    }
}
