using Smakosz.E2E.Infrastructure;

namespace Smakosz.E2E.Tests.Moderator;

[TestFixture]
public class T24_ModeratorAccessRestrictionTest : SmakoszE2ETestBase
{
    [Test]
    public async Task Moderator_CannotAccessAdminOnlyPages()
    {
        await LoginViaLocalStorageAsync(TestConstants.ModeratorEmail, TestConstants.ModeratorPassword);

        var restrictedPages = new[]
        {
            "/admin/users",
            "/admin/restaurants",
            "/admin/jobs",
            "/admin/system-config",
        };

        foreach (var pagePath in restrictedPages)
        {
            await NavigateAndWaitAsync(pagePath);
            await WaitForBlazorLoadedAsync();

            var pageContent = await Page.ContentAsync();
            var pageUrl = Page.Url;

            // Moderator should be blocked: redirect to login, "Nie masz uprawnien", or 403
            var isBlocked = pageUrl.Contains("/login")
                || pageContent.Contains("Nie masz uprawnien")
                || pageContent.Contains("403")
                || pageContent.Contains("Brak dostepu")
                || pageContent.Contains("Unauthorized");

            Assert.That(isBlocked, Is.True,
                $"Moderator should NOT have access to {pagePath}. URL: {pageUrl}");
        }
    }
}
