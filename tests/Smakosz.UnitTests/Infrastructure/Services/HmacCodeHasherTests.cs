using FluentAssertions;
using Smakosz.Infrastructure.Services;

namespace Smakosz.UnitTests.Infrastructure.Services;

[Trait("Category", "Services")]
public class HmacCodeHasherTests
{
    private readonly HmacCodeHasher _sut = new("test-secret-key-for-unit-tests-1234");

    [Fact]
    public void Hash_ReturnsDeterministicResult()
    {
        var hash1 = _sut.Hash("123456");
        var hash2 = _sut.Hash("123456");

        hash1.Should().Be(hash2);
    }

    [Fact]
    public void Hash_ReturnsHexString()
    {
        var hash = _sut.Hash("123456");

        hash.Should().MatchRegex("^[0-9a-f]{64}$");
    }

    [Fact]
    public void Verify_CorrectCode_ReturnsTrue()
    {
        var code = "654321";
        var hash = _sut.Hash(code);

        _sut.Verify(code, hash).Should().BeTrue();
    }

    [Fact]
    public void Verify_WrongCode_ReturnsFalse()
    {
        var hash = _sut.Hash("123456");

        _sut.Verify("654321", hash).Should().BeFalse();
    }

    [Fact]
    public void DifferentKeys_ProduceDifferentHashes()
    {
        var hasher2 = new HmacCodeHasher("different-secret-key-for-testing-5678");

        var hash1 = _sut.Hash("123456");
        var hash2 = hasher2.Hash("123456");

        hash1.Should().NotBe(hash2);
    }

    [Fact]
    public void Constructor_ThrowsOnNullOrEmptyKey()
    {
        var act1 = () => new HmacCodeHasher(null!);
        var act2 = () => new HmacCodeHasher("");
        var act3 = () => new HmacCodeHasher("   ");

        act1.Should().Throw<ArgumentException>();
        act2.Should().Throw<ArgumentException>();
        act3.Should().Throw<ArgumentException>();
    }
}
