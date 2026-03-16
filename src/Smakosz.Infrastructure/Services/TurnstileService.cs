using System.Net.Http.Json;
using System.Text.Json.Serialization;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Smakosz.Application.Common.Interfaces;

namespace Smakosz.Infrastructure.Services;

public class TurnstileService : ITurnstileService
{
    private readonly HttpClient _httpClient;
    private readonly string _secretKey;
    private readonly ILogger<TurnstileService> _logger;

    public TurnstileService(HttpClient httpClient, IConfiguration configuration, ILogger<TurnstileService> logger)
    {
        _httpClient = httpClient;
        _secretKey = configuration["Turnstile:SecretKey"]
            ?? throw new InvalidOperationException("Turnstile:SecretKey is required");
        _logger = logger;
    }

    public async Task<bool> VerifyAsync(string token, CancellationToken cancellationToken = default)
    {
        try
        {
            var response = await _httpClient.PostAsJsonAsync(
                "https://challenges.cloudflare.com/turnstile/v0/siteverify",
                new { secret = _secretKey, response = token },
                cancellationToken);

            var result = await response.Content.ReadFromJsonAsync<TurnstileResponse>(cancellationToken);

            if (result?.Success != true)
            {
                _logger.LogWarning("Turnstile verification failed. Error codes: {ErrorCodes}",
                    string.Join(", ", result?.ErrorCodes ?? []));
            }

            return result?.Success == true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Turnstile verification request failed");
            return false;
        }
    }

    private sealed class TurnstileResponse
    {
        [JsonPropertyName("success")]
        public bool Success { get; set; }

        [JsonPropertyName("error-codes")]
        public string[]? ErrorCodes { get; set; }
    }
}
