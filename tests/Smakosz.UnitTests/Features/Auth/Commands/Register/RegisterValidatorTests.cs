using FluentAssertions;
using FluentValidation.TestHelper;
using Smakosz.Application.Features.Auth.Commands.Register;
using Smakosz.UnitTests.Common.TestInfrastructure;

namespace Smakosz.UnitTests.Features.Auth.Commands.Register;

[Trait("Category", "Validators")]
public class RegisterValidatorTests
{
    private readonly RegisterValidator _validator = new(new StubValidationConfigProvider());

    private static RegisterCommand ValidCommand => new(
        Username: "testuser",
        Email: "test@example.com",
        Password: "Password1!"
    );

    [Fact]
    public void Validate_ValidCommand_NoErrors()
    {
        var result = _validator.TestValidate(ValidCommand);
        result.ShouldNotHaveAnyValidationErrors();
    }

    [Fact]
    public void Validate_EmptyUsername_HasError()
    {
        var result = _validator.TestValidate(ValidCommand with { Username = "" });
        result.ShouldHaveValidationErrorFor(x => x.Username);
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
    [InlineData("a_b")]
    public void Validate_UsernameMinLength_NoError(string username)
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
    public void Validate_UsernameMaxLength_NoError()
    {
        var result = _validator.TestValidate(ValidCommand with { Username = new string('a', 30) });
        result.ShouldNotHaveValidationErrorFor(x => x.Username);
    }

    [Theory]
    [InlineData("user name")]
    [InlineData("user@name")]
    [InlineData("user!name")]
    [InlineData("user#name")]
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

    [Fact]
    public void Validate_EmptyEmail_HasError()
    {
        var result = _validator.TestValidate(ValidCommand with { Email = "" });
        result.ShouldHaveValidationErrorFor(x => x.Email);
    }

    [Fact]
    public void Validate_InvalidEmail_HasError()
    {
        var result = _validator.TestValidate(ValidCommand with { Email = "not-an-email" });
        result.ShouldHaveValidationErrorFor(x => x.Email);
    }

    [Fact]
    public void Validate_EmptyPassword_HasError()
    {
        var result = _validator.TestValidate(ValidCommand with { Password = "" });
        result.ShouldHaveValidationErrorFor(x => x.Password);
    }

    [Fact]
    public void Validate_PasswordTooShort_HasError()
    {
        var result = _validator.TestValidate(ValidCommand with { Password = "Abc!12" });
        result.ShouldHaveValidationErrorFor(x => x.Password);
    }

    [Fact]
    public void Validate_PasswordMinLength_NoError()
    {
        var result = _validator.TestValidate(ValidCommand with { Password = "Abcdef1!" });
        result.ShouldNotHaveValidationErrorFor(x => x.Password);
    }

    [Fact]
    public void Validate_PasswordTooLong_HasError()
    {
        var result = _validator.TestValidate(ValidCommand with { Password = new string('A', 127) + "1!" });
        result.ShouldHaveValidationErrorFor(x => x.Password);
    }

    [Fact]
    public void Validate_PasswordMissingDigit_HasError()
    {
        var result = _validator.TestValidate(ValidCommand with { Password = "Password!!" });
        result.ShouldHaveValidationErrorFor(x => x.Password);
    }

    [Fact]
    public void Validate_PasswordMissingSpecialChar_HasError()
    {
        var result = _validator.TestValidate(ValidCommand with { Password = "Password12" });
        result.ShouldHaveValidationErrorFor(x => x.Password);
    }
}
