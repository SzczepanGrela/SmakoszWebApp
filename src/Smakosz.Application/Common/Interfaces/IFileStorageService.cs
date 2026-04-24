using Smakosz.Application.Common.Models;

namespace Smakosz.Application.Common.Interfaces;

public interface IFileStorageService
{
    Task<FileUploadResult> UploadAsync(Stream file, string fileName, string folder,
        IReadOnlyList<ImageVariant> variants, CancellationToken ct = default);
    Task DeleteAsync(string key, CancellationToken ct = default);
    Task<string> UploadRawAsync(Stream data, string key, string contentType, CancellationToken ct = default);
    string GetPublicUrl(string key);
    Task CheckConnectivityAsync(CancellationToken ct);
}

public record FileUploadResult(
    string Key,
    string PublicUrl,
    string? ThumbUrl,
    string? TinyUrl,
    string? HeroUrl,
    string? Blurhash,
    int? Width = null,
    int? Height = null
);
