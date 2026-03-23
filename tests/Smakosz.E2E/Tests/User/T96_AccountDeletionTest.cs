using Smakosz.E2E.Infrastructure;

namespace Smakosz.E2E.Tests.User;

[TestFixture]
public class T96_AccountDeletionTest : SmakoszE2ETestBase
{
    [Test]
    public async Task Security_ShowsDeleteAccountSection_AndOpensModal()
    {
        await LoginViaLocalStorageAsync(TestConstants.User2Email, TestConstants.UserPassword);
        await NavigateAndWaitAsync("/profile/security");

        var deleteSection = Page.GetByText("Usunięcie konta").First;
        await Expect(deleteSection).ToBeVisibleAsync(new LocatorAssertionsToBeVisibleOptions { Timeout = 10_000 });

        await Page.GetByRole(AriaRole.Button, new() { Name = "Usuń konto" }).ClickAsync();

        var modal = Page.Locator(".modal.show");
        await Expect(modal).ToBeVisibleAsync(new LocatorAssertionsToBeVisibleOptions { Timeout = 5_000 });

        await modal.Locator(".input-group input[type='password']").FillAsync(TestConstants.UserPassword);

        await modal.GetByRole(AriaRole.Button, new() { Name = "Wyślij kod potwierdzający" }).ClickAsync();

        var codeMessage = Page.GetByText("Kod weryfikacyjny został wysłany").First;
        await Expect(codeMessage).ToBeVisibleAsync(new LocatorAssertionsToBeVisibleOptions { Timeout = 10_000 });
    }
}
