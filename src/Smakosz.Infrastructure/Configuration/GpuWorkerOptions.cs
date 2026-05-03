namespace Smakosz.Infrastructure.Configuration;

public class GpuWorkerOptions
{
    public const string SectionName = "GpuWorker";
    public string Url { get; set; } = "http://localhost:8000";
    public string NodeId { get; set; } = "gpu-worker";
}
