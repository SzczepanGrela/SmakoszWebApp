using Hangfire;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Microsoft.ML.OnnxRuntime;
using Microsoft.ML.OnnxRuntime.Tensors;
using Smakosz.Application.Common.Interfaces;
using Smakosz.Domain.Entities.System;
using Smakosz.Infrastructure.Configuration;

namespace Smakosz.Orchestrator.Jobs;

public class NcfModelActivationService
{
    private readonly INcfModelStorageService _modelStorage;
    private readonly IBackgroundJobClient _jobs;
    private readonly ISmakoszDbContext _db;
    private readonly IDateTimeProvider _clock;
    private readonly OnnxOptions _onnxOptions;
    private readonly ILogger<NcfModelActivationService> _logger;

    public NcfModelActivationService(
        INcfModelStorageService modelStorage,
        IBackgroundJobClient jobs,
        ISmakoszDbContext db,
        IDateTimeProvider clock,
        IOptions<OnnxOptions> onnxOptions,
        ILogger<NcfModelActivationService> logger)
    {
        _modelStorage = modelStorage;
        _jobs = jobs;
        _db = db;
        _clock = clock;
        _onnxOptions = onnxOptions.Value;
        _logger = logger;
    }

    public async Task ActivateAsync(string modelVersion, CancellationToken ct)
    {
        var basePath = _onnxOptions.ModelBasePath;
        var versionDir = Path.Combine(basePath, modelVersion);
        var currentLink = Path.Combine(basePath, "current");

        _logger.LogInformation("Activating NCF model {Version}", modelVersion);

        await _modelStorage.DownloadModelAsync(modelVersion, basePath, ct);

        var modelPath = Path.Combine(versionDir, "ncf_model.onnx");
        if (!File.Exists(modelPath))
        {
            _logger.LogError("Downloaded model file not found at {Path}", modelPath);
            await UpsertConfigAsync("ncf.available", "false", "Whether NCF recommendations are available", _clock.UtcNow, ct);
            await _db.SaveChangesAsync(ct);
            return;
        }

        try
        {
            using var session = new InferenceSession(modelPath);
            var userIds = new DenseTensor<long>(new long[] { 0 }, new[] { 1 });
            var dishIds = new DenseTensor<long>(new long[] { 0 }, new[] { 1 });

            var inputs = new List<NamedOnnxValue>
            {
                NamedOnnxValue.CreateFromTensor("user_id", userIds),
                NamedOnnxValue.CreateFromTensor("dish_id", dishIds)
            };

            using var results = session.Run(inputs);
            var prediction = results.First().AsEnumerable<float>().First();

            if (prediction < 0f || prediction > 10f)
            {
                _logger.LogError("Smoke test failed: prediction={Prediction} out of range [0,10]", prediction);
                await UpsertConfigAsync("ncf.available", "false", "Whether NCF recommendations are available", _clock.UtcNow, ct);
                await _db.SaveChangesAsync(ct);
                return;
            }

            _logger.LogInformation("Smoke test passed: prediction={Prediction}", prediction);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Smoke test failed for model {Version}", modelVersion);
            await UpsertConfigAsync("ncf.available", "false", "Whether NCF recommendations are available", _clock.UtcNow, ct);
            await _db.SaveChangesAsync(ct);
            return;
        }

        try
        {
            if (Path.Exists(currentLink))
            {
                if (Directory.Exists(currentLink))
                    Directory.Delete(currentLink, false);
                else
                    File.Delete(currentLink);
            }

            Directory.CreateSymbolicLink(currentLink, versionDir);
            _logger.LogInformation("Symlink created: {Link} -> {Target}", currentLink, versionDir);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to create symlink");
            await UpsertConfigAsync("ncf.available", "false", "Whether NCF recommendations are available", _clock.UtcNow, ct);
            await _db.SaveChangesAsync(ct);
            return;
        }

        var now = _clock.UtcNow;

        await UpsertConfigAsync("ncf.available", "true", "Whether NCF recommendations are available", now, ct);
        await UpsertConfigAsync("ncf.activated_version", modelVersion, "Currently activated NCF model version", now, ct);

        await _db.SaveChangesAsync(ct);

        _jobs.Enqueue<INcfModelStorageService>(x => x.CleanupOldFilesAsync("models/ncf/", 2, CancellationToken.None));
        CleanupOldLocalVersions(basePath, modelVersion);

        await RestartApiContainerAsync(ct);

        _logger.LogInformation("NCF model {Version} activated successfully", modelVersion);
    }

    private async Task UpsertConfigAsync(string key, string value, string description, DateTime now, CancellationToken ct)
    {
        var config = await _db.SystemConfigs
            .FirstOrDefaultAsync(c => c.Key == key, ct);

        if (config is not null)
        {
            config.Value = value;
            config.UpdatedAt = now;
        }
        else
        {
            _db.SystemConfigs.Add(new SystemConfig
            {
                Key = key,
                Value = value,
                Description = description,
                UpdatedAt = now
            });
        }
    }

    private void CleanupOldLocalVersions(string basePath, string currentVersion)
    {
        try
        {
            if (!Directory.Exists(basePath))
                return;

            var versionDirs = Directory.GetDirectories(basePath)
                .Where(d => Path.GetFileName(d).StartsWith("v"))
                .OrderByDescending(d => Path.GetFileName(d))
                .Skip(2)
                .ToList();

            foreach (var dir in versionDirs)
            {
                Directory.Delete(dir, true);
                _logger.LogInformation("Deleted old local model version: {Dir}", Path.GetFileName(dir));
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to cleanup old local model versions");
        }
    }

    private async Task RestartApiContainerAsync(CancellationToken ct)
    {
        try
        {
            using var handler = new SocketsHttpHandler
            {
                ConnectCallback = async (context, token) =>
                {
                    var socket = new System.Net.Sockets.Socket(
                        System.Net.Sockets.AddressFamily.Unix,
                        System.Net.Sockets.SocketType.Stream,
                        System.Net.Sockets.ProtocolType.Unspecified);

                    var endpoint = new System.Net.Sockets.UnixDomainSocketEndPoint("/var/run/docker.sock");
                    await socket.ConnectAsync(endpoint, token);
                    return new System.Net.Sockets.NetworkStream(socket, ownsSocket: true);
                }
            };

            using var client = new HttpClient(handler) { BaseAddress = new Uri("http://localhost") };

            var response = await client.PostAsync(
                "/v1.45/containers/smakosz-net-api-1/restart?t=5", null, ct);

            if (response.IsSuccessStatusCode)
                _logger.LogInformation("API container restarted successfully");
            else
                _logger.LogWarning("API container restart returned {Status}", response.StatusCode);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to restart API container via Docker socket");
        }
    }
}
