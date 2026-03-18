using System.Text;
using System.Text.Json;

namespace Smakosz.E2E.Infrastructure;

public class SmakoszE2ETestBase : PageTest
{
    public override BrowserNewContextOptions ContextOptions()
    {
        return new BrowserNewContextOptions
        {
            Locale = "pl-PL",
            TimezoneId = "Europe/Warsaw",
            ViewportSize = new ViewportSize { Width = 1280, Height = 720 },
        };
    }

    protected async Task NavigateAndWaitAsync(string path)
    {
        var url = path.StartsWith("http")
            ? path
            : $"{TestConstants.ClientBaseUrl}{path}";

        await Page.GotoAsync(url, new PageGotoOptions
        {
            WaitUntil = WaitUntilState.NetworkIdle,
            Timeout = 30_000,
        });

        await WaitForBlazorLoadedAsync();
    }

    protected async Task WaitForBlazorLoadedAsync()
    {
        await Page.WaitForFunctionAsync(
            "() => { const app = document.getElementById('app'); return app && app.children.length > 0; }",
            null,
            new PageWaitForFunctionOptions { Timeout = 30_000 });

        var spinner = Page.Locator(".spinner-border, .loading-spinner, [class*='loading']").First;
        try
        {
            await spinner.WaitForAsync(new LocatorWaitForOptions
            {
                State = WaitForSelectorState.Hidden,
                Timeout = 10_000,
            });
        }
        catch (TimeoutException)
        {
            // No spinner found or already hidden - OK
        }
    }

    protected async Task LoginViaLocalStorageAsync(string email, string password)
    {
        // First navigate to the client so we can set localStorage on its origin
        await Page.GotoAsync(TestConstants.ClientBaseUrl, new PageGotoOptions
        {
            WaitUntil = WaitUntilState.NetworkIdle,
            Timeout = 30_000,
        });

        using var http = new HttpClient();
        var loginPayload = JsonSerializer.Serialize(new { email, password, turnstileToken = "e2e-test" });
        var content = new StringContent(loginPayload, Encoding.UTF8, "application/json");

        try
        {
            var response = await http.PostAsync($"{TestConstants.ApiBaseUrl}/api/auth/login", content);

            if (response.IsSuccessStatusCode)
            {
                var json = await response.Content.ReadAsStringAsync();
                using var doc = JsonDocument.Parse(json);
                var root = doc.RootElement;

                string? token = null;
                // API returns ApiResponse<LoginResponse>: { success, data: { accessToken, ... }, error }
                if (root.TryGetProperty("data", out var dataProp) &&
                    dataProp.TryGetProperty("accessToken", out var accessTokenProp))
                    token = accessTokenProp.GetString();
                else if (root.TryGetProperty("token", out var tokenProp))
                    token = tokenProp.GetString();

                if (!string.IsNullOrEmpty(token))
                {
                    await Page.EvaluateAsync($"localStorage.setItem('auth_token', '{token}')");
                    await Page.ReloadAsync(new PageReloadOptions
                    {
                        WaitUntil = WaitUntilState.NetworkIdle,
                    });
                    await WaitForBlazorLoadedAsync();
                    return;
                }
            }
        }
        catch
        {
        }

        var generatedToken = email switch
        {
            TestConstants.BusinessEmail => E2EAuthHelper.GenerateBusinessToken(),
            TestConstants.AdminEmail => E2EAuthHelper.GenerateAdminToken(),
            TestConstants.ModeratorEmail => E2EAuthHelper.GenerateModeratorToken(),
            TestConstants.User2Email => E2EAuthHelper.GenerateToken(2, TestConstants.User2Username, TestConstants.User2Email, "User"),
            _ => E2EAuthHelper.GenerateUserToken(),
        };

        await Page.EvaluateAsync($"localStorage.setItem('auth_token', '{generatedToken}')");
        await Page.ReloadAsync(new PageReloadOptions
        {
            WaitUntil = WaitUntilState.NetworkIdle,
        });
        await WaitForBlazorLoadedAsync();
    }

    protected async Task LoginViaUIAsync(string email, string password)
    {
        await NavigateAndWaitAsync("/login");

        await Page.Locator("input[type='email']").FillAsync(email);
        await Page.Locator(".input-group input[type='password']").FillAsync(password);

        await WaitForTurnstileAsync();

        await Page.GetByRole(AriaRole.Button, new() { Name = "Zaloguj się" }).ClickAsync();

        await Page.WaitForURLAsync(url => !url.Contains("/login"), new PageWaitForURLOptions
        {
            Timeout = 15_000,
        });

        await WaitForBlazorLoadedAsync();
    }

    protected async Task WaitForTurnstileAsync(int timeoutMs = 10_000)
    {
        try
        {
            await Page.WaitForFunctionAsync(
                "() => document.querySelector('[id^=\"turnstile-\"] iframe') !== null || typeof turnstile === 'undefined'",
                null,
                new PageWaitForFunctionOptions { Timeout = timeoutMs });
            await Page.WaitForTimeoutAsync(1000);
        }
        catch (TimeoutException)
        {
            // Turnstile not loaded - OK in E2E with test keys
        }
    }

    protected async Task AssertPageContainsTextAsync(string text, int timeoutMs = 10_000)
    {
        // MainLayout uses <main>, AdminLayout/BusinessLayout use <div class="flex-grow-1 ...">,
        // AuthLayout uses <div class="auth-layout">
        var contentArea = Page.Locator("main, div.flex-grow-1, div.auth-layout");
        await Expect(contentArea.GetByText(text).First).ToBeVisibleAsync(
            new LocatorAssertionsToBeVisibleOptions { Timeout = timeoutMs });
    }

    protected async Task AssertToastAsync(string text, int timeoutMs = 10_000)
    {
        var toast = Page.GetByText(text).First;
        await Expect(toast).ToBeVisibleAsync(
            new LocatorAssertionsToBeVisibleOptions { Timeout = timeoutMs });
    }
}
