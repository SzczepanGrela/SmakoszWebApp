using Smakosz.ClientTests.Common;

namespace Smakosz.ClientTests.Components;

public class ShareButtonTests : BunitTestBase
{
    [Fact]
    public void RendersButton()
    {
        var cut = RenderComponent<ShareButton>(p => p
            .Add(c => c.Url, "https://smakosz.pl/dish/pizza")
            .Add(c => c.Title, "Pizza Margherita"));

        cut.Find("button").TextContent.Should().Contain("Udostępnij");
        cut.Find("i.fa-solid.fa-share-nodes").Should().NotBeNull();
    }

    [Fact]
    public void Click_InvokesJsInterop()
    {
        var jsInterop = JSInterop;
        jsInterop.SetupVoid("navigator.share", _ => true);

        var cut = RenderComponent<ShareButton>(p => p
            .Add(c => c.Url, "https://smakosz.pl/dish/pizza")
            .Add(c => c.Title, "Pizza"));

        cut.Find("button").Click();

        jsInterop.VerifyInvoke("navigator.share");
    }
}
