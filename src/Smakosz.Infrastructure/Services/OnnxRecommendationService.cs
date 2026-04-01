using System.Text.Json;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Microsoft.ML.OnnxRuntime;
using Microsoft.ML.OnnxRuntime.Tensors;
using Smakosz.Application.Common.Interfaces;
using Smakosz.Infrastructure.Configuration;

namespace Smakosz.Infrastructure.Services;

public class OnnxRecommendationService : IRecommendationProvider, IDisposable
{
    private readonly OnnxOptions _options;
    private readonly ILogger<OnnxRecommendationService> _logger;

    private InferenceSession? _session;
    private Dictionary<int, int>? _userMap;     // userId -> embedding index
    private Dictionary<int, int>? _dishMap;     // dishId -> embedding index
    private Dictionary<int, int>? _reverseDishMap; // embedding index -> dishId
    private bool _isAvailable;
    private string? _fallbackReason;

    public OnnxRecommendationService(
        IOptions<OnnxOptions> options,
        ILogger<OnnxRecommendationService> logger)
    {
        _options = options.Value;
        _logger = logger;

        TryLoadModel();
    }

    public bool IsAvailable => _isAvailable;
    public string? FallbackReason => _fallbackReason;

    private void TryLoadModel()
    {
        var currentDir = Path.Combine(_options.ModelBasePath, "current");
        var modelPath = Path.Combine(currentDir, "ncf_model.onnx");
        var mappingPath = Path.Combine(currentDir, "mapping.json");

        if (!File.Exists(modelPath))
        {
            _fallbackReason = "Model NCF nie został jeszcze pobrany. Pokazujemy popularne dania.";
            _logger.LogInformation("ONNX model not found at {Path}, falling back to trending", modelPath);
            return;
        }

        if (!File.Exists(mappingPath))
        {
            _fallbackReason = "Brak pliku mapowania NCF. Pokazujemy popularne dania.";
            _logger.LogWarning("Mapping file not found at {Path}", mappingPath);
            return;
        }

        try
        {
            var mappingJson = File.ReadAllText(mappingPath);
            var mapping = JsonSerializer.Deserialize<NcfMapping>(mappingJson);

            if (mapping?.UserMap is null || mapping.DishMap is null)
            {
                _fallbackReason = "Nieprawidłowy plik mapowania NCF.";
                _logger.LogWarning("Invalid mapping.json structure");
                return;
            }

            _userMap = mapping.UserMap.ToDictionary(
                kvp => int.Parse(kvp.Key), kvp => kvp.Value);
            _dishMap = mapping.DishMap.ToDictionary(
                kvp => int.Parse(kvp.Key), kvp => kvp.Value);
            _reverseDishMap = _dishMap.ToDictionary(kvp => kvp.Value, kvp => kvp.Key);

            _session = new InferenceSession(modelPath);

            var smokeResult = RunInference(0, 0);
            if (smokeResult < 0f || smokeResult > 10f)
            {
                _fallbackReason = "Model NCF nie przeszedł testu walidacji.";
                _logger.LogWarning("ONNX smoke test failed: result={Result}", smokeResult);
                DisposeSession();
                return;
            }

            _isAvailable = true;
            _logger.LogInformation(
                "ONNX model loaded: {Users} users, {Dishes} dishes",
                _userMap.Count, _dishMap.Count);
        }
        catch (Exception ex)
        {
            _fallbackReason = "Błąd ładowania modelu NCF. Pokazujemy popularne dania.";
            _logger.LogError(ex, "Failed to load ONNX model from {Path}", modelPath);
            DisposeSession();
        }
    }

    public Task<List<(int DishId, float Score)>> GetPersonalizedAsync(
        int userId, int count, CancellationToken ct)
    {
        if (!_isAvailable || _session is null || _userMap is null || _dishMap is null || _reverseDishMap is null)
            return Task.FromResult(new List<(int, float)>());

        if (!_userMap.TryGetValue(userId, out var userIdx))
            return Task.FromResult(new List<(int, float)>());

        var dishCount = _dishMap.Count;
        var userArr = new long[dishCount];
        var dishArr = new long[dishCount];

        for (var i = 0; i < dishCount; i++)
        {
            userArr[i] = userIdx;
            dishArr[i] = i;
        }

        var userIds = new DenseTensor<long>(userArr, [dishCount]);
        var dishIds = new DenseTensor<long>(dishArr, [dishCount]);

        var inputs = new List<NamedOnnxValue>
        {
            NamedOnnxValue.CreateFromTensor("user_id", userIds),
            NamedOnnxValue.CreateFromTensor("dish_id", dishIds)
        };

        using var results = _session.Run(inputs);
        var predictions = results.First().AsEnumerable<float>().ToArray();

        var recommendations = new List<(int DishId, float Score)>();
        for (var i = 0; i < predictions.Length; i++)
        {
            if (_reverseDishMap.TryGetValue(i, out var dishId))
                recommendations.Add((dishId, predictions[i]));
        }

        var topN = recommendations
            .OrderByDescending(r => r.Score)
            .Take(count)
            .ToList();

        return Task.FromResult(topN);
    }

    private float RunInference(int userIdx, int dishIdx)
    {
        var userIds = new DenseTensor<long>(new long[] { userIdx }, new[] { 1 });
        var dishIds = new DenseTensor<long>(new long[] { dishIdx }, new[] { 1 });

        var inputs = new List<NamedOnnxValue>
        {
            NamedOnnxValue.CreateFromTensor("user_id", userIds),
            NamedOnnxValue.CreateFromTensor("dish_id", dishIds)
        };

        using var results = _session!.Run(inputs);
        return results.First().AsEnumerable<float>().First();
    }

    private void DisposeSession()
    {
        _session?.Dispose();
        _session = null;
        _isAvailable = false;
    }

    public void Dispose()
    {
        DisposeSession();
        GC.SuppressFinalize(this);
    }

    private class NcfMapping
    {
        [System.Text.Json.Serialization.JsonPropertyName("user_map")]
        public Dictionary<string, int>? UserMap { get; set; }

        [System.Text.Json.Serialization.JsonPropertyName("dish_map")]
        public Dictionary<string, int>? DishMap { get; set; }
    }
}
