using Smakosz.ClientTests.Common;

namespace Smakosz.ClientTests.Components;

public class ModalTests : BunitTestBase
{
    [Fact]
    public void IsOpenFalse_RendersNothing()
    {
        var cut = RenderComponent<Modal>(p => p.Add(c => c.IsOpen, false));
        cut.Markup.Trim().Should().BeEmpty();
    }

    [Fact]
    public void IsOpenTrue_RendersModal()
    {
        var cut = RenderComponent<Modal>(p => p
            .Add(c => c.IsOpen, true)
            .Add(c => c.Title, "Test Modal"));

        cut.Find(".modal").Should().NotBeNull();
        cut.Find(".modal-title").TextContent.Should().Be("Test Modal");
    }

    [Fact]
    public void RendersChildContent()
    {
        var cut = RenderComponent<Modal>(p => p
            .Add(c => c.IsOpen, true)
            .AddChildContent("<p>Body content</p>"));

        cut.Find(".modal-body").InnerHtml.Should().Contain("Body content");
    }

    [Fact]
    public void CloseButton_InvokesOnClose()
    {
        var closed = false;
        var cut = RenderComponent<Modal>(p => p
            .Add(c => c.IsOpen, true)
            .Add(c => c.OnClose, () => closed = true));

        cut.Find(".btn-close").Click();
        closed.Should().BeTrue();
    }
}
