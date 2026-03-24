namespace Smakosz.Infrastructure.Configuration;

public class R2Options
{
    public const string SectionName = "R2";

    public string AccountId { get; set; } = string.Empty;
    public string AccessKey { get; set; } = string.Empty;
    public string SecretKey { get; set; } = string.Empty;
    public string BucketName { get; set; } = "smakosz";
    public string PublicUrl { get; set; } = string.Empty;
}
