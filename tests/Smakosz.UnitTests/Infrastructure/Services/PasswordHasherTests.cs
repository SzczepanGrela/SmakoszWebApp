using FluentAssertions;
using Smakosz.Infrastructure.Services;

namespace Smakosz.UnitTests.Infrastructure.Services;

[Trait("Category", "Services")]
public class PasswordHasherTests
{
    private readonly PasswordHasher _sut = new();

    [Fact]
    public void Hash_ReturnsNonEmptyString()
    {
        var password = "TestPassword123!";

        var hash = _sut.Hash(password);

        hash.Should().NotBeNullOrEmpty();
    }

    [Fact]
    public void Hash_ReturnsDifferentHashForSamePassword()
    {
        var password = "TestPassword123!";

        var hash1 = _sut.Hash(password);
        var hash2 = _sut.Hash(password);

        hash1.Should().NotBe(hash2);
    }

    [Fact]
    public void Verify_CorrectPassword_ReturnsTrue()
    {
        var password = "TestPassword123!";
        var hash = _sut.Hash(password);

        var result = _sut.Verify(password, hash);

        result.Should().BeTrue();
    }

    [Fact]
    public void Verify_WrongPassword_ReturnsFalse()
    {
        var hash = _sut.Hash("CorrectPassword123!");

        var result = _sut.Verify("WrongPassword456!", hash);

        result.Should().BeFalse();
    }
}
