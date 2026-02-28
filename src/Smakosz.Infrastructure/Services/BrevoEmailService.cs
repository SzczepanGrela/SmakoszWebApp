using System.Net.Http.Json;
using Microsoft.Extensions.Logging;
using Smakosz.Application.Common.Interfaces;
using Smakosz.Infrastructure.Configuration;

namespace Smakosz.Infrastructure.Services;
//todo: zamienić proste html ciała na bardziej rozbudowane maile.
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
        => SendAsync(email, "Kod weryfikacyjny",
            $"""
            <h2>Weryfikacja konta Smakosz</h2>
            <p>Twój kod weryfikacyjny:</p>
            <h1 style="letter-spacing:8px;font-size:32px;text-align:center">{code}</h1>
            <p>Kod wygasa za 15 minut.</p>
            """, ct);

    public Task SendPasswordResetAsync(string email, string code, CancellationToken ct = default)
        => SendAsync(email, "Reset hasła",
            $"""
            <h2>Reset hasła - Smakosz</h2>
            <p>Twój kod do resetowania hasła:</p>
            <h1 style="letter-spacing:8px;font-size:32px;text-align:center">{code}</h1>
            <p>Kod wygasa za 15 minut. Jeśli nie prosiłeś o reset, zignoruj tę wiadomość.</p>
            """, ct);

    public Task Send2faCodeAsync(string email, string code, CancellationToken ct = default)
        => SendAsync(email, "Kod logowania 2FA",
            $"""
            <h2>Logowanie do Smakosz</h2>
            <p>Twój kod uwierzytelniania dwuskładnikowego:</p>
            <h1 style="letter-spacing:8px;font-size:32px;text-align:center">{code}</h1>
            <p>Kod wygasa za 10 minut.</p>
            """, ct);

    public Task SendDigestAsync(string email, string subject, string htmlBody, CancellationToken ct = default)
        => SendAsync(email, subject, htmlBody, ct);

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
