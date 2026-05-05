using FluentAssertions;
using Smakosz.Domain.Common;

namespace Smakosz.UnitTests.Common;

public class PhoneNumberNormalizerTests
{
    [Theory]
    [InlineData("+48 123 456 789", "+48123456789")]
    [InlineData("+48-123-456-789", "+48123456789")]
    [InlineData("+48 (123) 456-789", "+48123456789")]
    [InlineData("123456789", "+48123456789")]
    [InlineData("123 456 789", "+48123456789")]
    [InlineData("0048123456789", "+48123456789")]
    [InlineData("0048 123 456 789", "+48123456789")]
    [InlineData("+48123456789", "+48123456789")]
    [InlineData("+1 555 123 4567", "+15551234567")]
    public void Normalize_ValidInputs_ReturnsCanonicalE164(string input, string expected)
    {
        PhoneNumberNormalizer.Normalize(input).Should().Be(expected);
    }

    [Theory]
    [InlineData("abc")]
    [InlineData("12345")]
    [InlineData("+48abc123")]
    [InlineData("++48123456789")]
    [InlineData("12345678")]
    [InlineData("")]
    [InlineData("   ")]
    public void Normalize_InvalidInputs_ThrowsArgumentException(string input)
    {
        var act = () => PhoneNumberNormalizer.Normalize(input);
        act.Should().Throw<ArgumentException>()
            .WithMessage("*Nieprawidlowy format numeru telefonu*");
    }

    [Fact]
    public void Normalize_NullInput_ThrowsArgumentNullException()
    {
        var act = () => PhoneNumberNormalizer.Normalize(null!);
        act.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public void Normalize_AlreadyNormalized_IsIdempotent()
    {
        var first = PhoneNumberNormalizer.Normalize("+48 123 456 789");
        var second = PhoneNumberNormalizer.Normalize(first);
        second.Should().Be(first);
    }

    [Fact]
    public void Normalize_ResultMatchesSqlTriggerOutput_ForKnownFixture()
    {
        PhoneNumberNormalizer.Normalize("+48 111 222 333").Should().Be("+48111222333");
    }
}
