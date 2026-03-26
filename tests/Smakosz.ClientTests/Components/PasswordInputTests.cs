using Smakosz.ClientTests.Common;

namespace Smakosz.ClientTests.Components;

public class PasswordInputTests : BunitTestBase
{
    [Fact]
    public void DefaultState_InputIsPassword()
    {
        var cut = RenderComponent<PasswordInput>();
        cut.Find("input").GetAttribute("type").Should().Be("password");
    }

    [Fact]
    public void ClickToggle_SwitchesToText()
    {
        var cut = RenderComponent<PasswordInput>();

        cut.Find("button").Click();
        cut.Find("input").GetAttribute("type").Should().Be("text");
    }

    [Fact]
    public void ClickToggleTwice_BackToPassword()
    {
        var cut = RenderComponent<PasswordInput>();

        cut.Find("button").Click();
        cut.Find("button").Click();
        cut.Find("input").GetAttribute("type").Should().Be("password");
    }

    [Fact]
    public void EyeIcon_TogglesWithVisibility()
    {
        var cut = RenderComponent<PasswordInput>();

        cut.Find("i").ClassList.Should().Contain("fa-eye");

        cut.Find("button").Click();
        cut.Find("i").ClassList.Should().Contain("fa-eye-slash");
    }

    [Fact]
    public void Placeholder_IsRendered()
    {
        var cut = RenderComponent<PasswordInput>(p => p
            .Add(c => c.Placeholder, "Wpisz haslo"));

        cut.Find("input").GetAttribute("placeholder").Should().Be("Wpisz haslo");
    }
}
