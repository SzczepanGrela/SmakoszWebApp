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

    [SetUp]
    public async Task ResetDatabaseBeforeEachTest()
    {
        await E2EDatabaseSeeder.ResetAsync();
    }

    protected async Task NavigateAndWaitAsync(string path, WaitUntilState waitUntil = WaitUntilState.NetworkIdle)
    {
        var url = path.StartsWith("http")
            ? path
            : $"{TestConstants.ClientBaseUrl}{path}";

        try
        {
            await Page.GotoAsync(url, new PageGotoOptions
            {
                WaitUntil = waitUntil,
                Timeout = 30_000,
            });
        }
        catch (TimeoutException)
        {
            // Some pages keep firing background requests (image loads, polling) and never reach NetworkIdle.
            // Fall back to DOMContentLoaded so the test can still proceed once Blazor is ready.
            await Page.GotoAsync(url, new PageGotoOptions
            {
                WaitUntil = WaitUntilState.DOMContentLoaded,
                Timeout = 30_000,
            });
        }

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

    // Name kept for backward compatibility across 117 test files. Implementation now uses Page.APIRequest
    // so the API Set-Cookie response lands on the same BrowserContext that the page subsequently uses,
    // delivering the HttpOnly auth cookies to every following Blazor request. HttpOnly cookies cannot be
    // set via Page.EvaluateAsync so the previous localStorage shortcut is no longer viable.
    protected async Task LoginViaLocalStorageAsync(string email, string password)
    {
        await Page.GotoAsync(TestConstants.ClientBaseUrl, new PageGotoOptions
        {
            WaitUntil = WaitUntilState.NetworkIdle,
            Timeout = 30_000,
        });

        var response = await Page.APIRequest.PostAsync(
            $"{TestConstants.ApiBaseUrl}/api/auth/login",
            new APIRequestContextOptions
            {
                DataObject = new { email, password, turnstileToken = "e2e-test" },
                Headers = new Dictionary<string, string> { ["Content-Type"] = "application/json" }
            });

        if (!response.Ok)
        {
            var body = await response.TextAsync();
            throw new InvalidOperationException($"E2E login failed for {email}: HTTP {response.Status} {body}");
        }

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
