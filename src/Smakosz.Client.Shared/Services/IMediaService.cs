namespace Smakosz.Client.Services;

public interface IMediaService
{
    Task<UploadResult?> UploadImageAsync(Stream file, string fileName, string entityType, int? entityId = null);
}

public class UploadResult
{
    public string Url { get; set; } = string.Empty;
    public string? ThumbUrl { get; set; }
    public string? Blurhash { get; set; }
}
