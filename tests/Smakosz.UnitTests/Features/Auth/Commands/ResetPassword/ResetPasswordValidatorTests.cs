using FluentAssertions;
using Smakosz.Application.Features.Auth.Commands.ResetPassword;

namespace Smakosz.UnitTests.Features.Auth.Commands.ResetPassword;

[Trait("Category", "Validators")]
public class ResetPasswordValidatorTests
{
    private readonly ResetPasswordValidator _validator = new();

    [Fact]
    public void Validate_EmptyEmail_HasError()
    {
        var command = new ResetPasswordCommand("", "123456", "NewPassword123!");
        var result = _validator.Validate(command);
        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.PropertyName == "Email");
    }

    [Fact]
    public void Validate_EmptyCode_HasError()
    {
        var command = new ResetPasswordCommand("test@example.com", "", "NewPassword123!");
        var result = _validator.Validate(command);
        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.PropertyName == "Code");
    }

    [Fact]
    public void Validate_PasswordTooShort_HasError()
    {
        var command = new ResetPasswordCommand("test@example.com", "123456", "short");
        var result = _validator.Validate(command);
        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.PropertyName == "NewPassword");
    }

    [Fact]
    public void Validate_ValidCommand_NoErrors()
    {
        var command = new ResetPasswordCommand("test@example.com", "123456", "StrongPassword123!");
        var result = _validator.Validate(command);
        result.IsValid.Should().BeTrue();
    }
}
