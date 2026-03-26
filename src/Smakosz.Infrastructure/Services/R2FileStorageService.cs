using Amazon.S3;
using Amazon.S3.Model;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Smakosz.Application.Common.Interfaces;
using Smakosz.Application.Common.Models;
using Smakosz.Infrastructure.Configuration;

namespace Smakosz.Infrastructure.Services;

public class R2FileStorageService : IFileStorageService
{
    private readonly AmazonS3Client _s3;
    private readonly R2Options _options;
    private readonly ImageProcessingService _imageProcessor;
    private readonly ILogger<R2FileStorageService> _logger;

    public R2FileStorageService(
        IOptions<R2Options> options,
        ImageProcessingService imageProcessor,
        ILogger<R2FileStorageService> logger)
    {
        _options = options.Value;
        _imageProcessor = imageProcessor;
        _logger = logger;

        var config = new AmazonS3Config
        {
            ServiceURL = $"https://{_options.AccountId}.r2.cloudflarestorage.com",
            ForcePathStyle = true
        };
        _s3 = new AmazonS3Client(_options.AccessKey, _options.SecretKey, config);
    }

    public async Task<FileUploadResult> UploadAsync(Stream file, string fileName, string folder,
        IReadOnlyList<ImageVariant> variants, CancellationToken ct = default)
    {
        var baseName = Path.GetFileNameWithoutExtension(fileName);
        var baseKey = $"{folder}/{baseName}";

        string? thumbUrl = null, tinyUrl = null, heroUrl = null, blurhash = null;
        int? mainWidth = null, mainHeight = null;

        // Generate blurhash from original
        try
        {
            file.Position = 0;
            blurhash = await _imageProcessor.GenerateBlurhashAsync(file);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to generate blurhash for {FileName}", fileName);
        }

        // Upload requested variants
        foreach (var variant in variants)
        {
            file.Position = 0;
            var (resized, width, height) = await _imageProcessor.ResizeToWebpAsync(file, variant.MaxWidth);

            // Capture dimensions from the first variant as the main dimensions
            mainWidth ??= width;
            mainHeight ??= height;

            var key = $"{baseKey}{variant.Suffix}.webp";
            await UploadToR2Async(resized, key, "image/webp", ct);

            var url = GetPublicUrl(key);
            switch (variant.Suffix)
            {
                case "_thumb": thumbUrl = url; break;
                case "_tiny": tinyUrl = url; break;
                case "_hero": heroUrl = url; break;
            }
        }

        var fullKey = $"{baseKey}.webp";
        return new FileUploadResult(fullKey, GetPublicUrl(fullKey), thumbUrl, tinyUrl, heroUrl, blurhash, mainWidth, mainHeight);
    }

    public async Task DeleteAsync(string key, CancellationToken ct = default)
    {
        var baseName = Path.GetFileNameWithoutExtension(key);
        var folder = Path.GetDirectoryName(key)?.Replace('\\', '/') ?? "";

        // Delete all possible variants
        string[] suffixes = ["", "_thumb", "_tiny", "_hero"];
        foreach (var suffix in suffixes)
        {
            var variantKey = $"{folder}/{baseName}{suffix}.webp";
            try
            {
                await _s3.DeleteObjectAsync(new DeleteObjectRequest
                {
                    BucketName = _options.BucketName,
                    Key = variantKey
                }, ct);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to delete {Key} from R2", variantKey);
            }
        }
    }

    public async Task<string> UploadRawAsync(Stream data, string key, string contentType, CancellationToken ct = default)
    {
        await UploadToR2Async(data, key, contentType, ct);
        return GetPublicUrl(key);
    }

    public string GetPublicUrl(string key) => $"{_options.PublicUrl.TrimEnd('/')}/{key}";

    private async Task UploadToR2Async(Stream stream, string key, string contentType, CancellationToken ct)
    {
        await _s3.PutObjectAsync(new PutObjectRequest
        {
            BucketName = _options.BucketName,
            Key = key,
            InputStream = stream,
            ContentType = contentType
        }, ct);

        _logger.LogDebug("Uploaded {Key} to R2 ({Bytes} bytes)", key, stream.Length);
    }
}
