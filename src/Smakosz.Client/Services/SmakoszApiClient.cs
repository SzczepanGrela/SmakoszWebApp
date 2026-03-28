using System.Net.Http.Json;
using System.Text.Json;
using Smakosz.Client.Models;

namespace Smakosz.Client.Services;

public class SmakoszApiClient
{
    private readonly HttpClient _http;
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    public SmakoszApiClient(HttpClient http)
    {
        _http = http;
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

    private static async Task<ApiResponse<T>> ParseApiResponse<T>(HttpResponseMessage response)
    {
        if (response.StatusCode == System.Net.HttpStatusCode.NoContent)
            return new ApiResponse<T> { Success = true };

        try
        {
            var result = await response.Content.ReadFromJsonAsync<ApiResponse<T>>(JsonOptions);
            return result ?? new ApiResponse<T> { Success = false, Error = new ApiError { Code = "PARSE_ERROR", Message = "Failed to parse response" } };
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
