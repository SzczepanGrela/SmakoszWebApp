using FluentAssertions;
using Smakosz.Application.Features.Auth.Commands.ForgotPassword;

namespace Smakosz.UnitTests.Features.Auth.Commands.ForgotPassword;

[Trait("Category", "Validators")]
public class ForgotPasswordValidatorTests
{
    private readonly ForgotPasswordValidator _validator = new();

    [Fact]
    public void Validate_EmptyEmail_HasError()
    {
        var command = new ForgotPasswordCommand("");
        var result = _validator.Validate(command);
        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.PropertyName == "Email");
    }

    [Fact]
    public void Validate_InvalidEmailFormat_HasError()
    {
        var command = new ForgotPasswordCommand("not-an-email");
        var result = _validator.Validate(command);
        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.PropertyName == "Email");
    }

    [Fact]
    public void Validate_ValidEmail_NoErrors()
    {
        var command = new ForgotPasswordCommand("test@example.com");
        var result = _validator.Validate(command);
        result.IsValid.Should().BeTrue();
    }
}
