namespace Smakosz.Infrastructure.Configuration;

public class OnnxOptions
{
    public const string SectionName = "Onnx";

    public string ModelBasePath { get; set; } = "/models/ncf";
}
