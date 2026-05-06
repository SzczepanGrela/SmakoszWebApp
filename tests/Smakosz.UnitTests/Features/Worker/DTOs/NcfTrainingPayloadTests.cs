using System.Text.Json;
using FluentAssertions;
using Smakosz.Application.Features.Worker.DTOs;
using Xunit;

namespace Smakosz.UnitTests.Features.Worker.DTOs;

[Trait("Category", "Contracts")]
public class NcfTrainingPayloadTests
{
    [Fact]
    public void Serialize_UsesSnakeCaseProperties_MatchesPythonContract()
    {
        var payload = new NcfTrainingPayload(
            CsvUrl: "https://r2.example/test.csv",
            Epochs: 50,
            BatchSize: 256,
            LearningRate: 0.001,
            EmbeddingDim: 64,
            ReviewCount: 1234);

        var json = JsonSerializer.Serialize(payload);

        json.Should().Contain("\"csv_url\":\"https://r2.example/test.csv\"");
        json.Should().Contain("\"epochs\":50");
        json.Should().Contain("\"batch_size\":256");
        json.Should().Contain("\"learning_rate\":0.001");
        json.Should().Contain("\"embedding_dim\":64");
        json.Should().Contain("\"review_count\":1234");
    }
}
