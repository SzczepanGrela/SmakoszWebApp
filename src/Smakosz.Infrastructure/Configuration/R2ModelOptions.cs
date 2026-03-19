namespace Smakosz.Infrastructure.Configuration;

public class R2ModelOptions
{
    public const string SectionName = "R2Models";

    public string AccountId { get; set; } = string.Empty;
    public string AccessKey { get; set; } = string.Empty;
    public string SecretKey { get; set; } = string.Empty;
    public string BucketName { get; set; } = string.Empty;
}
