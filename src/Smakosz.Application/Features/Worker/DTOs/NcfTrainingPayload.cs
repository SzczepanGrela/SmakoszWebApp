using System.Text.Json.Serialization;

namespace Smakosz.Application.Features.Worker.DTOs;

public sealed record NcfTrainingPayload(
    [property: JsonPropertyName("csv_url")] string CsvUrl,
    [property: JsonPropertyName("epochs")] int Epochs,
    [property: JsonPropertyName("batch_size")] int BatchSize,
    [property: JsonPropertyName("learning_rate")] double LearningRate,
    [property: JsonPropertyName("embedding_dim")] int EmbeddingDim,
    [property: JsonPropertyName("review_count")] int ReviewCount);
