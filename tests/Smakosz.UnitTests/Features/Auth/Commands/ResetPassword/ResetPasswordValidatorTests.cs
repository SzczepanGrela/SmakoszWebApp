using FluentAssertions;
using Smakosz.Application.Features.Auth.Commands.ResetPassword;
using Smakosz.UnitTests.Common.TestInfrastructure;

namespace Smakosz.UnitTests.Features.Auth.Commands.ResetPassword;

[Trait("Category", "Validators")]
public class ResetPasswordValidatorTests
{
    private readonly ResetPasswordValidator _validator = new(new StubValidationConfigProvider());

    [Fact]
    public void Validate_EmptyEmail_HasError()
    {
        var command = new ResetPasswordCommand("", "123456", "NewPassword1!");
        var result = _validator.Validate(command);
        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.PropertyName == "Email");
    }

    [Fact]
    public void Validate_EmptyCode_HasError()
    {
        var command = new ResetPasswordCommand("test@example.com", "", "NewPassword1!");
        var result = _validator.Validate(command);
        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.PropertyName == "Code");
    }

    [Fact]
    public void Validate_PasswordTooShort_HasError()
    {
        var command = new ResetPasswordCommand("test@example.com", "123456", "Sh1!");
        var result = _validator.Validate(command);
        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.PropertyName == "NewPassword");
    }

    [Fact]
    public void Validate_PasswordTooLong_HasError()
    {
        var command = new ResetPasswordCommand("test@example.com", "123456", new string('A', 127) + "1!");
        var result = _validator.Validate(command);
        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.PropertyName == "NewPassword");
    }

    [Fact]
    public void Validate_PasswordMissingDigit_HasError()
    {
        var command = new ResetPasswordCommand("test@example.com", "123456", "Password!!");
        var result = _validator.Validate(command);
        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.PropertyName == "NewPassword");
    }

    [Fact]
    public void Validate_PasswordMissingSpecial_HasError()
    {
        var command = new ResetPasswordCommand("test@example.com", "123456", "Password12");
        var result = _validator.Validate(command);
        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.PropertyName == "NewPassword");
    }

    [Fact]
    public void Validate_ValidCommand_NoErrors()
    {
        var command = new ResetPasswordCommand("test@example.com", "123456", "StrongPassword1!");
        var result = _validator.Validate(command);
        result.IsValid.Should().BeTrue();
    }
}
