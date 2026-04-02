using Amazon.S3;
using Amazon.S3.Model;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Smakosz.Application.Common.Interfaces;
using Smakosz.Infrastructure.Configuration;

namespace Smakosz.Infrastructure.Services;

public class NcfModelStorageService : INcfModelStorageService
{
    private readonly AmazonS3Client _s3;
    private readonly R2ModelOptions _options;
    private readonly ILogger<NcfModelStorageService> _logger;

    public NcfModelStorageService(
        IOptions<R2ModelOptions> options,
        ILogger<NcfModelStorageService> logger)
    {
        _options = options.Value;
        _logger = logger;

        var config = new AmazonS3Config
        {
            ServiceURL = $"https://{_options.AccountId}.r2.cloudflarestorage.com",
            ForcePathStyle = true
        };
        _s3 = new AmazonS3Client(_options.AccessKey, _options.SecretKey, config);
    }

    public async Task DownloadModelAsync(string version, string localBasePath, CancellationToken ct)
    {
        var versionDir = Path.Combine(localBasePath, version);
        Directory.CreateDirectory(versionDir);

        await DownloadFileAsync($"models/ncf/{version}/ncf_model.onnx",
            Path.Combine(versionDir, "ncf_model.onnx"), ct);
        await DownloadFileAsync($"models/ncf/{version}/mapping.json",
            Path.Combine(versionDir, "mapping.json"), ct);

        _logger.LogInformation("Downloaded NCF model {Version} to {Path}", version, versionDir);
    }

    public async Task<string> UploadTrainingDataAsync(Stream data, string key, CancellationToken ct)
    {
        await _s3.PutObjectAsync(new PutObjectRequest
        {
            BucketName = _options.BucketName,
            Key = key,
            InputStream = data,
            ContentType = "text/csv",
            DisablePayloadSigning = true,
            DisableDefaultChecksumValidation = true
        }, ct);

        _logger.LogDebug("Uploaded training data {Key} to {Bucket}", key, _options.BucketName);
        return $"s3://{_options.BucketName}/{key}";
    }

    public async Task CleanupOldFilesAsync(string prefix, int keepCount, CancellationToken ct)
    {
        var response = await _s3.ListObjectsV2Async(new ListObjectsV2Request
        {
            BucketName = _options.BucketName,
            Prefix = prefix
        }, ct);

        var grouped = response.S3Objects
            .GroupBy(o =>
            {
                var afterPrefix = o.Key[prefix.Length..];
                var slashIdx = afterPrefix.IndexOf('/');
                return slashIdx > 0 ? afterPrefix[..slashIdx] : afterPrefix;
            })
            .OrderByDescending(g => g.Max(o => o.LastModified))
            .ToList();

        var toDelete = grouped.Skip(keepCount).SelectMany(g => g).ToList();

        if (toDelete.Count == 0)
            return;

        foreach (var obj in toDelete)
        {
            try
            {
                await _s3.DeleteObjectAsync(new DeleteObjectRequest
                {
                    BucketName = _options.BucketName,
                    Key = obj.Key
                }, ct);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to delete old file {Key}", obj.Key);
            }
        }

        _logger.LogInformation("Cleaned up {Count} old files from {Prefix}", toDelete.Count, prefix);
    }

    private async Task DownloadFileAsync(string key, string localPath, CancellationToken ct)
    {
        var response = await _s3.GetObjectAsync(new GetObjectRequest
        {
            BucketName = _options.BucketName,
            Key = key
        }, ct);

        await using var fileStream = File.Create(localPath);
        await response.ResponseStream.CopyToAsync(fileStream, ct);
    }
}
