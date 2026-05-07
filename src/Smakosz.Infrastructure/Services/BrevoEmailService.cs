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

    public Task SendSecurityPasswordChangedAsync(string email, string? ipAddress, string? countryCode, DateTime occurredAt, CancellationToken ct = default)
    {
        var when = occurredAt.ToString("dd.MM.yyyy HH:mm");
        var fromInfo = BuildLocationInfo(ipAddress, countryCode);
        var inner = EmailTemplateBuilder.BuildContentSection(
            "Has&#322;o zosta&#322;o zmienione",
            "Hej,",
            $"Twoje has&#322;o zosta&#322;o zmienione {when}{fromInfo}.",
            "Je&#347;li to by&#322;e&#347; Ty &mdash; mo&#380;esz zignorowa&#263; t&#281; wiadomo&#347;&#263;. Je&#347;li nie &mdash; kliknij poni&#380;szy link &#380;eby przejrze&#263; aktywne sesje i ponownie zmieni&#263; has&#322;o.",
            BuildSecurityCtaButton());
        var html = EmailTemplateBuilder.WrapInLayout(inner);
        return SendAsync(email, "Smakosz - hasło zostało zmienione", html, ct);
    }

    public Task SendSecurityTwoFactorDisabledAsync(string email, string? ipAddress, string? countryCode, DateTime occurredAt, CancellationToken ct = default)
    {
        var when = occurredAt.ToString("dd.MM.yyyy HH:mm");
        var fromInfo = BuildLocationInfo(ipAddress, countryCode);
        var inner = EmailTemplateBuilder.BuildContentSection(
            "Wy&#322;&#261;czono dwusk&#322;adnikowe uwierzytelnianie",
            "Hej,",
            $"Dwusk&#322;adnikowe uwierzytelnianie (2FA) zosta&#322;o wy&#322;&#261;czone na Twoim koncie {when}{fromInfo}.",
            "Bez 2FA wystarczy has&#322;o &#380;eby si&#281; zalogowa&#263;. Je&#347;li to nie Ty &mdash; natychmiast zresetuj has&#322;o i w&#322;&#261;cz 2FA ponownie.",
            BuildSecurityCtaButton());
        var html = EmailTemplateBuilder.WrapInLayout(inner);
        return SendAsync(email, "Smakosz - wyłączono dwuskładnikowe uwierzytelnianie", html, ct);
    }

    public Task SendSecurityAccountLockedAsync(string email, int failedAttempts, DateTime lockUntil, string? ipAddress, string? countryCode, CancellationToken ct = default)
    {
        var until = lockUntil.ToString("dd.MM.yyyy HH:mm");
        var fromInfo = BuildLocationInfo(ipAddress, countryCode);
        var inner = EmailTemplateBuilder.BuildContentSection(
            "Wykryto pr&oacute;by w&#322;amania na Twoje konto",
            "Hej,",
            $"Kto&#347; pr&oacute;bowa&#322; si&#281; zalogowa&#263; na Twoje konto {failedAttempts} razy bez sukcesu. Konto jest zablokowane do {until}{fromInfo}.",
            "Je&#347;li to nie Ty pr&oacute;bujesz si&#281; zalogowa&#263; &mdash; natychmiast zmie&#324; has&#322;o.",
            BuildSecurityCtaButton());
        var html = EmailTemplateBuilder.WrapInLayout(inner);
        return SendAsync(email, "Smakosz - wykryto próby włamania na Twoje konto", html, ct);
    }

    public Task SendSecurityNewCountryLoginAsync(string email, string countryCode, string? ipAddress, string? userAgent, DateTime occurredAt, CancellationToken ct = default)
    {
        var when = occurredAt.ToString("dd.MM.yyyy HH:mm");
        var ipInfo = string.IsNullOrEmpty(ipAddress) ? "" : $" ({ipAddress})";
        var uaInfo = string.IsNullOrEmpty(userAgent) ? "" : $" u&#380;ywaj&#261;c {userAgent}";
        var inner = EmailTemplateBuilder.BuildContentSection(
            $"Logowanie z nowego kraju ({countryCode})",
            "Hej,",
            $"Kto&#347; zalogowa&#322; si&#281; na Twoje konto z {countryCode}{ipInfo} {when}{uaInfo}.",
            "Je&#347;li to by&#322;e&#347; Ty (np. podr&oacute;&#380;ujesz, u&#380;ywasz VPN) &mdash; mo&#380;esz zignorowa&#263;. Je&#347;li nie &mdash; natychmiast wyloguj wszystkie sesje i zmie&#324; has&#322;o.",
            BuildSecurityCtaButton());
        var html = EmailTemplateBuilder.WrapInLayout(inner);
        return SendAsync(email, $"Smakosz - logowanie z nowego kraju ({countryCode})", html, ct);
    }

    private static string BuildLocationInfo(string? ipAddress, string? countryCode)
    {
        if (string.IsNullOrEmpty(ipAddress)) return "";
        var country = string.IsNullOrEmpty(countryCode) ? "" : $" ({countryCode})";
        return $" z adresu IP {ipAddress}{country}";
    }

    private string BuildSecurityCtaButton()
    {
        var url = $"{_options.ClientBaseUrl.TrimEnd('/')}/profile/security";
        return $"<a href=\"{url}\" style=\"display:inline-block;padding:10px 20px;background:#B8860B;color:#fff;text-decoration:none;border-radius:6px;\">Zarz&#261;dzaj kontem</a>";
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
