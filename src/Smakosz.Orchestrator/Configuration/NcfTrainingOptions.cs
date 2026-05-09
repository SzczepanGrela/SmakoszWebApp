namespace Smakosz.Orchestrator.Configuration;

public class NcfTrainingOptions
{
    public const string SectionName = "NcfTraining";
    public int Epochs { get; set; } = 80;
    public int BatchSize { get; set; } = 256;
    public double LearningRate { get; set; } = 0.0005;
    public int EmbeddingDim { get; set; } = 128;
    public int ReviewWindowDays { get; set; } = 0; // 0 = all reviews (no time window filter)
}
