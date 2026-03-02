using Smakosz.ClientTests.Common;

namespace Smakosz.ClientTests.Components;

public class ConfirmModalTests : BunitTestBase
{
    [Fact]
    public void IsOpenTrue_RendersMessageAndButtons()
    {
        var cut = RenderComponent<ConfirmModal>(p => p
            .Add(c => c.IsOpen, true)
            .Add(c => c.Message, "Usunac?"));

        cut.Markup.Should().Contain("Usunac?");
        cut.FindAll("button").Should().Contain(b => b.TextContent.Contains("Anuluj"));
        cut.FindAll("button").Should().Contain(b => b.TextContent.Contains("Potwierdz"));
    }

    [Fact]
    public void ConfirmButton_InvokesOnConfirm()
    {
        var confirmed = false;
        var cut = RenderComponent<ConfirmModal>(p => p
            .Add(c => c.IsOpen, true)
            .Add(c => c.OnConfirm, () => confirmed = true));

        cut.Find(".btn-danger").Click();
        confirmed.Should().BeTrue();
    }

    [Fact]
    public void CancelButton_InvokesOnCancel()
    {
        var cancelled = false;
        var cut = RenderComponent<ConfirmModal>(p => p
            .Add(c => c.IsOpen, true)
            .Add(c => c.OnCancel, () => cancelled = true));

        cut.Find(".btn-outline-secondary").Click();
        cancelled.Should().BeTrue();
    }

    [Fact]
    public void DefaultMessage_ShowsDefaultText()
    {
        var cut = RenderComponent<ConfirmModal>(p => p.Add(c => c.IsOpen, true));
        cut.Markup.Should().Contain("Czy na pewno?");
    }
}
