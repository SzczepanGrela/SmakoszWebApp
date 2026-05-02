using System.Net.Http.Json;
using System.Text.Json;

namespace Smakosz.Client.Services;

public class MediaService : IMediaService
{
    private readonly HttpClient _http;
    private static readonly JsonSerializerOptions JsonOptions = new() { PropertyNameCaseInsensitive = true };

    public MediaService(HttpClient http) => _http = http;

    public async Task<UploadResult?> UploadImageAsync(Stream file, string fileName, string entityType, int? entityId = null)
    {
        using var content = new MultipartFormDataContent();
        using var streamContent = new StreamContent(file);
        streamContent.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue(
            GetContentType(fileName));

        content.Add(streamContent, "file", fileName);
        content.Add(new StringContent(entityType), "entityType");
        if (entityId.HasValue)
            content.Add(new StringContent(entityId.Value.ToString()), "entityId");

        var response = await _http.PostAsync("/api/media/upload", content);
        if (!response.IsSuccessStatusCode)
            return null;

        var apiResponse = await response.Content.ReadFromJsonAsync<ApiUploadResponse>(JsonOptions);
        if (apiResponse is not { Success: true, Data: not null })
            return null;

        return new UploadResult
        {
            Url = apiResponse.Data.Url,
            ThumbUrl = apiResponse.Data.ThumbUrl,
            Blurhash = apiResponse.Data.Blurhash
        };
    }

    public async Task<UploadResult?> UploadAvatarAsync(Stream file, string fileName)
    {
        using var content = new MultipartFormDataContent();
        using var streamContent = new StreamContent(file);
        streamContent.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue(GetContentType(fileName));
        content.Add(streamContent, "file", fileName);

        var response = await _http.PutAsync("/api/me/avatar", content);
        if (!response.IsSuccessStatusCode)
            return null;

        var apiResponse = await response.Content.ReadFromJsonAsync<ApiAvatarResponse>(JsonOptions);
        if (apiResponse is not { Success: true, Data: not null })
            return null;

        return new UploadResult
        {
            Url = apiResponse.Data.Url,
            Blurhash = apiResponse.Data.Blurhash
        };
    }

    public async Task<bool> DeleteAvatarAsync()
    {
        var response = await _http.DeleteAsync("/api/me/avatar");
        return response.IsSuccessStatusCode;
    }

    private static string GetContentType(string fileName) =>
        Path.GetExtension(fileName).ToLowerInvariant() switch
        {
            ".png" => "image/png",
            ".webp" => "image/webp",
            _ => "image/jpeg"
        };

    private class ApiAvatarResponse
    {
        public bool Success { get; set; }
        public AvatarData? Data { get; set; }
    }

    private class AvatarData
    {
        public string Url { get; set; } = string.Empty;
        public string? Blurhash { get; set; }
    }

    private class ApiUploadResponse
    {
        public bool Success { get; set; }
        public UploadData? Data { get; set; }
    }

    private class UploadData
    {
        public string Url { get; set; } = string.Empty;
        public string? ThumbUrl { get; set; }
        public string? Blurhash { get; set; }
    }
}
