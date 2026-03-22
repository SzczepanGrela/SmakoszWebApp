using FluentValidation.TestHelper;
using Smakosz.Application.Features.Me.Commands.UpdateProfile;
using Smakosz.UnitTests.Common.TestInfrastructure;

namespace Smakosz.UnitTests.Features.Me.Commands.UpdateProfile;

[Trait("Category", "Validators")]
public class UpdateProfileValidatorTests
{
    private readonly UpdateProfileValidator _validator = new(new StubValidationConfigProvider());

    private static UpdateProfileCommand ValidCommand => new("testuser", "Some bio", null);

    [Fact]
    public void Validate_ValidCommand_NoErrors()
    {
        var result = _validator.TestValidate(ValidCommand);
        result.ShouldNotHaveAnyValidationErrors();
    }

    [Fact]
    public void Validate_NullUsername_NoErrors()
    {
        var result = _validator.TestValidate(ValidCommand with { Username = null });
        result.ShouldNotHaveValidationErrorFor(x => x.Username);
    }

    [Theory]
    [InlineData("ab")]
    public void Validate_UsernameTooShort_HasError(string username)
    {
        var result = _validator.TestValidate(ValidCommand with { Username = username });
        result.ShouldHaveValidationErrorFor(x => x.Username);
    }

    [Theory]
    [InlineData("abc")]
    public void Validate_UsernameAtMinLength_NoError(string username)
    {
        var result = _validator.TestValidate(ValidCommand with { Username = username });
        result.ShouldNotHaveValidationErrorFor(x => x.Username);
    }

    [Fact]
    public void Validate_UsernameTooLong_HasError()
    {
        var result = _validator.TestValidate(ValidCommand with { Username = new string('a', 31) });
        result.ShouldHaveValidationErrorFor(x => x.Username);
    }

    [Fact]
    public void Validate_UsernameAtMaxLength_NoError()
    {
        var result = _validator.TestValidate(ValidCommand with { Username = new string('a', 30) });
        result.ShouldNotHaveValidationErrorFor(x => x.Username);
    }

    [Theory]
    [InlineData("user name")]
    [InlineData("user@name")]
    [InlineData("user!name")]
    public void Validate_UsernameInvalidChars_HasError(string username)
    {
        var result = _validator.TestValidate(ValidCommand with { Username = username });
        result.ShouldHaveValidationErrorFor(x => x.Username);
    }

    [Theory]
    [InlineData("user.name")]
    [InlineData("user-name")]
    [InlineData("user_name")]
    [InlineData("UserName123")]
    public void Validate_UsernameValidChars_NoError(string username)
    {
        var result = _validator.TestValidate(ValidCommand with { Username = username });
        result.ShouldNotHaveValidationErrorFor(x => x.Username);
    }
}
