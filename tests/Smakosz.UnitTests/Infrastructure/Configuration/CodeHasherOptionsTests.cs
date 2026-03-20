using FluentAssertions;
using Smakosz.Infrastructure.Configuration;

namespace Smakosz.UnitTests.Infrastructure.Configuration;

[Trait("Category", "Configuration")]
public class CodeHasherOptionsTests
{
    [Fact]
    public void SectionName_IsCodeHasher()
    {
        CodeHasherOptions.SectionName.Should().Be("CodeHasher");
    }

    [Fact]
    public void Secret_DefaultsToEmpty()
    {
        var options = new CodeHasherOptions();

        options.Secret.Should().BeEmpty();
    }
}
