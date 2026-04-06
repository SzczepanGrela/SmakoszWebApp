namespace Smakosz.Infrastructure.Configuration;

public class CodeHasherOptions
{
    public const string SectionName = "CodeHasher";

    public string Secret { get; set; } = string.Empty;
}
