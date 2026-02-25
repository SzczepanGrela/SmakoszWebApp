using FluentAssertions;
using FluentValidation.TestHelper;
using Smakosz.Application.Features.Auth.Commands.Login;

namespace Smakosz.UnitTests.Features.Auth.Commands.Login;

[Trait("Category", "Validators")]
public class LoginValidatorTests
{
    private readonly LoginValidator _validator = new();

    private static LoginCommand ValidCommand => new(
        Email: "test@example.com",
        Password: "password123"
    );

    [Fact]
    public void Validate_ValidCommand_NoErrors()
    {
        var result = _validator.TestValidate(ValidCommand);
        result.ShouldNotHaveAnyValidationErrors();
    }

    [Fact]
    public void Validate_EmptyEmail_HasError()
    {
        var result = _validator.TestValidate(ValidCommand with { Email = "" });
        result.ShouldHaveValidationErrorFor(x => x.Email);
    }

    [Fact]
    public void Validate_InvalidEmail_HasError()
    {
        var result = _validator.TestValidate(ValidCommand with { Email = "invalid" });
        result.ShouldHaveValidationErrorFor(x => x.Email);
    }

    [Fact]
    public void Validate_EmptyPassword_HasError()
    {
        var result = _validator.TestValidate(ValidCommand with { Password = "" });
        result.ShouldHaveValidationErrorFor(x => x.Password);
    }

    [Fact]
    public void Validate_ValidEmailFormats_NoError()
    {
        var result = _validator.TestValidate(ValidCommand with { Email = "user@domain.co.uk" });
        result.ShouldNotHaveValidationErrorFor(x => x.Email);
    }
}
