using Smakosz.Client.Models;

namespace Smakosz.Client.Services;

public interface IContentService
{
    Task<ContentPageDto?> GetAboutPageAsync();
    Task<ContentPageDto?> GetTermsPageAsync();
    Task<ContentPageDto?> GetPrivacyPageAsync();
    Task<ContactPageDto?> GetContactPageAsync();
    Task<bool> SendContactMessageAsync(string name, string email, string subject, string message, string? turnstileToken = null);
}
