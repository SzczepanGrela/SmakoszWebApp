using Smakosz.E2E.Infrastructure;

namespace Smakosz.E2E.Tests.Anonymous;

[TestFixture]
public class T06_ProtectedRoutesTest : SmakoszE2ETestBase
{
    [Test]
    public async Task AnonymousUser_CannotAccessProtectedRoutes_RedirectsToLogin()
    {
        await NavigateAndWaitAsync("/restaurant/dashboard");
        await WaitForBlazorLoadedAsync();
        Assert.That(Page.Url, Does.Contain("/login"),
            $"Expected redirect to /login from /restaurant/dashboard, but URL is: {Page.Url}");

        await NavigateAndWaitAsync("/admin");
        await WaitForBlazorLoadedAsync();
        Assert.That(Page.Url, Does.Contain("/login"),
            $"Expected redirect to /login from /admin, but URL is: {Page.Url}");

        await NavigateAndWaitAsync("/review/add");
        await WaitForBlazorLoadedAsync();
        Assert.That(Page.Url, Does.Contain("/login"),
            $"Expected redirect to /login from /review/add, but URL is: {Page.Url}");

        await NavigateAndWaitAsync("/saved");
        await WaitForBlazorLoadedAsync();
        Assert.That(Page.Url, Does.Contain("/login"),
            $"Expected redirect to /login from /saved, but URL is: {Page.Url}");
    }
}
