using System.Net.Http.Json;
using System.Text.Json;
using Smakosz.Client.Models;

namespace Smakosz.Client.Services;

public class SmakoszApiClient
{
    private readonly HttpClient _http;
    private readonly IConcurrencyConflictService? _concurrencyConflict;
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    public SmakoszApiClient(HttpClient http, IConcurrencyConflictService? concurrencyConflict = null)
    {
        _http = http;
        _concurrencyConflict = concurrencyConflict;
    }

    public async Task<T?> GetAsync<T>(string url)
    {
        var response = await _http.GetAsync(url);
        return await HandleResponse<T>(response);
    }

    public async Task<T?> PostAsync<T>(string url, object? body = null)
    {
        var response = await _http.PostAsJsonAsync(url, body, JsonOptions);
        return await HandleResponse<T>(response);
    }

    public async Task<T?> PutAsync<T>(string url, object? body = null)
    {
        var response = await _http.PutAsJsonAsync(url, body, JsonOptions);
        return await HandleResponse<T>(response);
    }

    public async Task<bool> DeleteAsync(string url)
    {
        var response = await _http.DeleteAsync(url);
        return response.IsSuccessStatusCode;
    }

    public async Task<ApiResponse<T>> GetApiResponseAsync<T>(string url)
    {
        var response = await _http.GetAsync(url);
        return await ParseApiResponse<T>(response);
    }

    public async Task<ApiResponse<T>> PostApiResponseAsync<T>(string url, object? body = null)
    {
        var response = await _http.PostAsJsonAsync(url, body, JsonOptions);
        return await ParseApiResponse<T>(response);
    }

    public async Task<ApiResponse<T>> PutApiResponseAsync<T>(string url, object? body = null)
    {
        var response = await _http.PutAsJsonAsync(url, body, JsonOptions);
        return await ParseApiResponse<T>(response);
    }

    public async Task<ApiResponse<T>> PostMultipartApiResponseAsync<T>(string url, MultipartFormDataContent content)
    {
        var response = await _http.PostAsync(url, content);
        return await ParseApiResponse<T>(response);
    }

    public async Task<ApiResponse<T>> PutMultipartApiResponseAsync<T>(string url, MultipartFormDataContent content)
    {
        var response = await _http.PutAsync(url, content);
        return await ParseApiResponse<T>(response);
    }

    public async Task<ApiResponse<object>> DeleteApiResponseAsync(string url)
    {
        var response = await _http.DeleteAsync(url);
        return await ParseApiResponse<object>(response);
    }

    private static async Task<T?> HandleResponse<T>(HttpResponseMessage response)
    {
        if (!response.IsSuccessStatusCode)
            return default;

        var apiResponse = await response.Content.ReadFromJsonAsync<ApiResponse<T>>(JsonOptions);
        return apiResponse is { Success: true } ? apiResponse.Data : default;
    }

    private static bool IsRateLimited(HttpResponseMessage response)
        => response.StatusCode == System.Net.HttpStatusCode.TooManyRequests;

    private async Task<ApiResponse<T>> ParseApiResponse<T>(HttpResponseMessage response)
    {
        if (response.StatusCode == System.Net.HttpStatusCode.NoContent)
            return new ApiResponse<T> { Success = true };

        if (IsRateLimited(response))
            return new ApiResponse<T>
            {
                Success = false,
                Error = new ApiError { Code = "RATE_LIMIT_EXCEEDED", Message = "Zbyt wiele zapytan. Sprobuj ponownie pozniej." }
            };

        try
        {
            var result = await response.Content.ReadFromJsonAsync<ApiResponse<T>>(JsonOptions);
            var parsed = result ?? new ApiResponse<T> { Success = false, Error = new ApiError { Code = "PARSE_ERROR", Message = "Failed to parse response" } };
            if (parsed.Error?.Code == "CONCURRENCY_CONFLICT")
                _concurrencyConflict?.Show();
            return parsed;
        }
        catch
        {
            return new ApiResponse<T>
            {
                Success = false,
                Error = new ApiError { Code = response.StatusCode.ToString(), Message = $"HTTP {(int)response.StatusCode}" }
            };
        }
    }
}
