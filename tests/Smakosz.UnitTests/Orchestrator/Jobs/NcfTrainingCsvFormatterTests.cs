using System.Text;
using FluentAssertions;
using Smakosz.Orchestrator.Jobs;
using Xunit;

namespace Smakosz.UnitTests.Orchestrator.Jobs;

[Trait("Category", "Formatters")]
public class NcfTrainingCsvFormatterTests
{
    private static string ReadStream(Stream s)
    {
        using var reader = new StreamReader(s, Encoding.UTF8);
        return reader.ReadToEnd();
    }

    [Fact]
    public void EmptyList_ReturnsHeaderOnly()
    {
        using var stream = NcfTrainingCsvFormatter.FormatAsCsv(Array.Empty<NcfTrainingSample>());
        var content = ReadStream(stream);
        content.Should().Be("user_id,dish_id,rating" + Environment.NewLine);
    }

    [Fact]
    public void SingleSample_ProducesExpectedRow()
    {
        var samples = new[] { new NcfTrainingSample(1, 2, 5) };
        using var stream = NcfTrainingCsvFormatter.FormatAsCsv(samples);
        var content = ReadStream(stream);
        content.Should().Be(
            "user_id,dish_id,rating" + Environment.NewLine +
            "1,2,5" + Environment.NewLine);
    }

    [Fact]
    public void MultipleSamples_AllRowsPresent()
    {
        var samples = new[]
        {
            new NcfTrainingSample(1, 10, 5),
            new NcfTrainingSample(2, 11, 4),
            new NcfTrainingSample(3, 12, 3)
        };
        using var stream = NcfTrainingCsvFormatter.FormatAsCsv(samples);
        var content = ReadStream(stream);
        content.Should().Be(
            "user_id,dish_id,rating" + Environment.NewLine +
            "1,10,5" + Environment.NewLine +
            "2,11,4" + Environment.NewLine +
            "3,12,3" + Environment.NewLine);
    }
}
