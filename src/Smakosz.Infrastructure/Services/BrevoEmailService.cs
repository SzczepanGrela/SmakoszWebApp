using System.Net.Http.Json;
using Microsoft.Extensions.Logging;
using Smakosz.Application.Common.Interfaces;
using Smakosz.Application.Common.Models;
using Smakosz.Domain.Enums;
using Smakosz.Infrastructure.Configuration;

namespace Smakosz.Infrastructure.Services;
public class BrevoEmailService : IEmailService
{
    private const string ApiUrl = "https://api.brevo.com/v3/smtp/email";

    private readonly HttpClient _httpClient;
    private readonly BrevoOptions _options;
    private readonly ILogger<BrevoEmailService> _logger;

    public BrevoEmailService(HttpClient httpClient, BrevoOptions options, ILogger<BrevoEmailService> logger)
    {
        _httpClient = httpClient;
        _options = options;
        _logger = logger;

        _httpClient.DefaultRequestHeaders.TryAddWithoutValidation("api-key", _options.ApiKey);
    }

    public Task SendVerificationCodeAsync(string email, string code, CancellationToken ct = default)
        => SendCodeEmailAsync(email, "Kod weryfikacyjny",
            "Weryfikacja konta Smakosz", "Tw&oacute;j kod weryfikacyjny:", code,
            "Kod wygasa za 15 minut.", ct);

    public Task SendPasswordResetAsync(string email, string code, CancellationToken ct = default)
        => SendCodeEmailAsync(email, "Reset hasła",
            "Reset has&#322;a &mdash; Smakosz", "Tw&oacute;j kod do resetowania has&#322;a:", code,
            "Kod wygasa za 15 minut. Je&#347;li nie prosi&#322;e&#347; o reset, zignoruj t&#281; wiadomo&#347;&#263;.", ct);

    public Task Send2faCodeAsync(string email, string code, CancellationToken ct = default)
        => SendCodeEmailAsync(email, "Kod logowania 2FA",
            "Logowanie do Smakosz", "Tw&oacute;j kod uwierzytelniania dwusk&#322;adnikowego:", code,
            "Kod wygasa za 10 minut.", ct);

    public Task SendContactConfirmationAsync(string email, string contactName, string subject, CancellationToken ct = default)
    {
        var inner = EmailTemplateBuilder.BuildContentSection(
            "Dzi&#281;kujemy za kontakt!",
            $"Cze&#347;&#263; {contactName},",
            $"Otrzymali&#347;my Twoj&#261; wiadomo&#347;&#263; dotycz&#261;c&#261;: <strong>{subject}</strong>",
            "Postaramy si&#281; odpowiedzie&#263; jak najszybciej.");
        var html = EmailTemplateBuilder.WrapInLayout(inner);
        return SendAsync(email, "Smakosz - potwierdzenie wiadomości", html, ct);
    }

    public Task SendContactResponseAsync(string email, string responseText, CancellationToken ct = default)
    {
        var inner = EmailTemplateBuilder.BuildContentSection(
            "Odpowied&#378; na Twoj&#261; wiadomo&#347;&#263;",
            responseText.Replace("\n", "<br/>"));
        var html = EmailTemplateBuilder.WrapInLayout(inner);
        return SendAsync(email, "Smakosz - odpowiedź na Twoją wiadomość", html, ct);
    }

    public Task SendNotificationDigestAsync(string email, string subject, IReadOnlyList<NotificationItem> notifications, CancellationToken ct = default)
    {
        var inner = EmailTemplateBuilder.BuildNotificationList(notifications);
        var html = EmailTemplateBuilder.WrapInLayout(inner);
        return SendAsync(email, subject, html, ct);
    }

    public Task SendAccountDeletionCodeAsync(string email, string code, CancellationToken ct = default)
        => SendCodeEmailAsync(email, "Potwierdzenie usunięcia konta",
            "Usuni&#281;cie konta Smakosz", "Tw&oacute;j kod potwierdzaj&#261;cy usuni&#281;cie konta:", code,
            "Kod wygasa za 15 minut. Je&#347;li to nie Ty &mdash; natychmiast zmie&#324; has&#322;o.", ct);

    public Task SendAccountDeletionConfirmationAsync(string email, CancellationToken ct = default)
    {
        var inner = EmailTemplateBuilder.BuildContentSection(
            "Konto oznaczone do usuni&#281;cia",
            "Twoje konto Smakosz zosta&#322;o oznaczone do trwa&#322;ego usuni&#281;cia.",
            "Wszystkie dane zostan&#261; usuni&#281;te po 30 dniach.",
            "Je&#347;li chcesz anulowa&#263; usuni&#281;cie &mdash; skontaktuj si&#281; z administracj&#261;.");
        var html = EmailTemplateBuilder.WrapInLayout(inner);
        return SendAsync(email, "Smakosz - konto oznaczone do usunięcia", html, ct);
    }

    public Task SendInvitationAsync(string email, string code, string username, UserRole role, CancellationToken ct = default)
    {
        var roleLabel = role == UserRole.Admin ? "administratora" : "moderatora";
        var encodedEmail = Uri.EscapeDataString(email);
        var link = $"{_options.ClientBaseUrl.TrimEnd('/')}/accept-invite?email={encodedEmail}&code={code}";
        var inner = EmailTemplateBuilder.BuildInvitationSection(username, roleLabel, link, code);
        var html = EmailTemplateBuilder.WrapInLayout(inner);
        return SendAsync(email, "Smakosz - zaproszenie do zespołu", html, ct);
    }

    private Task SendCodeEmailAsync(string email, string subject, string heading, string label, string code, string footer, CancellationToken ct)
    {
        var inner = EmailTemplateBuilder.BuildCodeSection(heading, label, code, footer);
        var html = EmailTemplateBuilder.WrapInLayout(inner);
        return SendAsync(email, subject, html, ct);
    }

    private async Task SendAsync(string recipientEmail, string subject, string htmlContent, CancellationToken ct)
    {
        var payload = new
        {
            sender = new { name = _options.SenderName, email = _options.SenderEmail },
            to = new[] { new { email = recipientEmail } },
            subject,
            htmlContent
        };

        var response = await _httpClient.PostAsJsonAsync(ApiUrl, payload, ct);

        if (!response.IsSuccessStatusCode)
        {
            var body = await response.Content.ReadAsStringAsync(ct);
            _logger.LogError("Brevo API error {StatusCode}: {Body}", (int)response.StatusCode, body);
            throw new HttpRequestException($"Brevo API returned {(int)response.StatusCode}");
        }

        _logger.LogInformation("Email sent to {Email}: {Subject}", recipientEmail, subject);
    }
}
