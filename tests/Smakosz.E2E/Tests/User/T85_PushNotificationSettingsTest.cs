using Smakosz.E2E.Infrastructure;

namespace Smakosz.E2E.Tests.User;

[TestFixture]
public class T85_PushNotificationSettingsTest : SmakoszE2ETestBase
{
    [Test]
    public async Task User_CanSeePushNotificationSection_InSettings()
    {
        await LoginViaLocalStorageAsync(TestConstants.UserEmail, TestConstants.UserPassword);
        await NavigateAndWaitAsync("/profile/notifications");

        await AssertPageContainsTextAsync("Ustawienia powiadomień");
        await AssertPageContainsTextAsync("Powiadomienia push");

        var pushCard = Page.Locator("text=Powiadomienia push").First;
        await Expect(pushCard).ToBeVisibleAsync();
    }
}
