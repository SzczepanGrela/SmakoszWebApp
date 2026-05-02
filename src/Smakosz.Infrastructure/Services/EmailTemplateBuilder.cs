using Smakosz.Application.Common.Models;

namespace Smakosz.Infrastructure.Services;

public static class EmailTemplateBuilder
{
    private const string BrandPrimary = "#D4A574";
    private const string BrandAccent = "#B8860B";
    private const string BrandDark = "#4A3428";
    private const string BrandBackground = "#E8DDD0";
    private const string CodeBackground = "#F2EDE6";

    public static string WrapInLayout(string innerHtml)
    {
        var year = DateTime.UtcNow.Year;
        return $"""
            <!DOCTYPE html>
            <html lang="pl">
            <head><meta charset="utf-8"/><meta name="viewport" content="width=device-width,initial-scale=1.0"/></head>
            <body style="margin:0;padding:0;background-color:{BrandBackground};font-family:Segoe UI,Roboto,Helvetica Neue,Arial,sans-serif;">
            <table role="presentation" width="100%" cellpadding="0" cellspacing="0" style="background-color:{BrandBackground};padding:32px 0;">
            <tr><td align="center">
            <table role="presentation" width="600" cellpadding="0" cellspacing="0" style="max-width:600px;width:100%;">
            <!-- Header -->
            <tr><td style="background-color:{BrandDark};padding:24px;text-align:center;border-radius:12px 12px 0 0;">
            <img src="https://smakosz.xyz/favicon-96x96.png" alt="Smakosz" width="36" height="36" style="vertical-align:middle;margin-right:10px;" /><span style="font-size:28px;color:{BrandPrimary};font-weight:bold;vertical-align:middle;">Smakosz</span>
            </td></tr>
            <!-- Content -->
            <tr><td style="background-color:#ffffff;padding:32px;border-radius:0 0 12px 12px;">
            {innerHtml}
            </td></tr>
            <!-- Footer -->
            <tr><td style="padding:24px;text-align:center;color:#999999;font-size:13px;">
            <p style="margin:0 0 8px;">Pozdrawiamy, Zesp&oacute;&#322; Smakosz</p>
            <p style="margin:0;">&copy; {year} Smakosz. Wszelkie prawa zastrze&#380;one.</p>
            </td></tr>
            </table>
            </td></tr>
            </table>
            </body>
            </html>
            """;
    }

    public static string BuildCodeSection(string heading, string label, string code, string footer)
    {
        return $"""
            <h2 style="color:{BrandDark};margin:0 0 16px;">{heading}</h2>
            <p style="color:#333333;margin:0 0 24px;">{label}</p>
            <div style="background-color:{CodeBackground};border-radius:8px;padding:24px;text-align:center;margin:0 0 24px;">
            <span style="font-size:32px;font-weight:bold;letter-spacing:8px;color:{BrandAccent};">{code}</span>
            </div>
            <p style="color:#666666;font-size:14px;margin:0;">{footer}</p>
            """;
    }

    public static string BuildContentSection(string heading, params string[] paragraphs)
    {
        var body = string.Join("", paragraphs.Select(p =>
            $"""<p style="color:#333333;margin:0 0 12px;line-height:1.6;">{p}</p>"""));

        return $"""
            <h2 style="color:{BrandDark};margin:0 0 16px;">{heading}</h2>
            {body}
            """;
    }

    public static string BuildInvitationSection(string username, string roleLabel, string link, string code)
    {
        return $"""
            <h2 style="color:{BrandDark};margin:0 0 16px;">Zaproszenie do Smakosz</h2>
            <p style="color:#333333;margin:0 0 12px;line-height:1.6;">Cze&#347;&#263; {username},</p>
            <p style="color:#333333;margin:0 0 16px;line-height:1.6;">Administrator zaprosi&#322; Ci&#281; do zespo&#322;u Smakosz w roli {roleLabel}. Aby aktywowa&#263; konto, kliknij ponizszy link i ustaw swoje has&#322;o.</p>
            <div style="text-align:center;margin:24px 0;">
            <a href="{link}" style="display:inline-block;background-color:{BrandAccent};color:#ffffff;padding:14px 28px;border-radius:8px;text-decoration:none;font-weight:bold;">Aktywuj konto</a>
            </div>
            <p style="color:#666666;font-size:14px;margin:16px 0 0;">Link wygasa za 24 godziny. Je&#347;li nie spodziewa&#322;e&#347; si&#281; tej wiadomo&#347;ci, zignoruj j&#261;.</p>
            <p style="color:#999999;font-size:12px;margin:16px 0 0;word-break:break-all;">Je&#347;li link nie dzia&#322;a, skopiuj go do przegl&#261;darki: {link}</p>
            """;
    }

    public static string BuildNotificationList(IReadOnlyList<NotificationItem> items)
    {
        var cards = string.Join(
            $"""<hr style="border:none;border-top:1px solid {BrandBackground};margin:16px 0;"/>""",
            items.Select(item => $"""
                <h3 style="color:{BrandDark};margin:0 0 4px;font-size:16px;">{item.Title}</h3>
                <p style="color:#333333;margin:0;line-height:1.5;">{item.Message}</p>
                """));

        return $"""
            <h2 style="color:{BrandDark};margin:0 0 16px;">Twoje powiadomienia</h2>
            {cards}
            """;
    }
}
