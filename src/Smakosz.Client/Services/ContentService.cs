using Smakosz.Client.Models;

namespace Smakosz.Client.Services;

public class ContentService : IContentService
{
    private readonly SmakoszApiClient _api;

    public ContentService(SmakoszApiClient api) => _api = api;

    public Task<ContentPageDto?> GetAboutPageAsync()
        => _api.GetAsync<ContentPageDto>("/api/content/about");

    public Task<ContentPageDto?> GetTermsPageAsync()
        => _api.GetAsync<ContentPageDto>("/api/content/terms");

    public Task<ContentPageDto?> GetPrivacyPageAsync()
        => _api.GetAsync<ContentPageDto>("/api/content/privacy");

    public Task<ContactPageDto?> GetContactPageAsync()
        => _api.GetAsync<ContactPageDto>("/api/content/contact-info");

    public async Task<ContactMessageResult> SendContactMessageAsync(string name, string email, string subject, string message, string? turnstileToken = null)
    {
        var response = await _api.PostApiResponseAsync<object>("/api/content/contact", new
        {
            Name = name,
            Email = email,
            Subject = subject,
            Message = message,
            TurnstileToken = turnstileToken
        });
        return response.Success
            ? new ContactMessageResult(true, null)
            : new ContactMessageResult(false, response.Error?.GetDisplayMessage());
    }
}
