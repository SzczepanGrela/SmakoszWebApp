using Smakosz.ClientTests.Common;

namespace Smakosz.ClientTests.Components;

public class UserDropdownTests : BunitTestBase
{
    [Fact]
    public void Anonymous_RendersNothing()
    {
        var cut = RenderComponent<UserDropdown>();
        cut.FindAll(".dropdown").Should().BeEmpty();
    }

    [Fact]
    public void Authenticated_RendersDropdown()
    {
        SetAuthenticatedUser("JanKowalski", "User");
        var cut = RenderComponent<UserDropdown>();

        cut.Markup.Should().Contain("JanKowalski");
        cut.Find("a[href='/profile']").Should().NotBeNull();
        cut.Find("a[href='/saved']").Should().NotBeNull();
        cut.Find("a[href='/profile/settings']").Should().NotBeNull();
    }

    [Fact]
    public void UserRole_NoAdminLink()
    {
        SetAuthenticatedUser("JanKowalski", "User");
        var cut = RenderComponent<UserDropdown>();

        cut.FindAll("a[href='/admin']").Should().BeEmpty();
    }

    [Fact]
    public void AdminRole_ShowsAdminLink()
    {
        SetAuthenticatedUser("Admin", "Admin");
        var cut = RenderComponent<UserDropdown>();

        cut.Find("a[href='/admin']").Should().NotBeNull();
    }

    [Fact]
    public void RestaurantRole_ShowsRestaurantLink()
    {
        SetAuthenticatedUser("Restaurator", "Restaurant");
        var cut = RenderComponent<UserDropdown>();

        cut.Find("a[href='/restaurant/dashboard']").Should().NotBeNull();
    }

    [Fact]
    public async Task LogoutButton_CallsAuthService()
    {
        SetAuthenticatedUser("JanKowalski", "User");
        var authService = Services.GetRequiredService<IAuthService>();

        var cut = RenderComponent<UserDropdown>();
        cut.Find("button.dropdown-item.text-danger").Click();

        await authService.Received(1).LogoutAsync();
    }
}
