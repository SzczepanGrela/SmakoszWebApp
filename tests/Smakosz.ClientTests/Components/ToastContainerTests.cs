using Smakosz.ClientTests.Common;

namespace Smakosz.ClientTests.Components;

public class ToastContainerTests : BunitTestBase
{
    [Fact]
    public void InitialState_NoToasts()
    {
        var cut = RenderComponent<ToastContainer>();
        cut.FindAll(".toast").Should().BeEmpty();
    }

    [Fact]
    public void ShowSuccess_RendersToast()
    {
        var toastService = Services.GetRequiredService<ToastService>();
        var cut = RenderComponent<ToastContainer>();

        toastService.ShowSuccess("Zapisano!");

        cut.WaitForState(() => cut.FindAll(".toast").Count > 0);
        cut.Find(".toast").ClassList.Should().Contain("bg-success");
        cut.Markup.Should().Contain("Zapisano!");
    }

    [Fact]
    public void ShowError_RendersErrorToast()
    {
        var toastService = Services.GetRequiredService<ToastService>();
        var cut = RenderComponent<ToastContainer>();

        toastService.ShowError("Blad!");

        cut.WaitForState(() => cut.FindAll(".toast").Count > 0);
        cut.Find(".toast").ClassList.Should().Contain("bg-danger");
    }

    [Fact]
    public void WithTitle_RendersTitle()
    {
        var toastService = Services.GetRequiredService<ToastService>();
        var cut = RenderComponent<ToastContainer>();

        toastService.ShowSuccess("Wiadomosc", "Sukces");

        cut.WaitForState(() => cut.Markup.Contains("Sukces"));
        cut.Find("strong").TextContent.Should().Contain("Sukces");
    }

    [Fact]
    public void CloseButton_RemovesToast()
    {
        var toastService = Services.GetRequiredService<ToastService>();
        var cut = RenderComponent<ToastContainer>();

        toastService.ShowSuccess("Test");
        cut.WaitForState(() => cut.FindAll(".toast").Count > 0);

        cut.Find(".btn-close").Click();

        cut.WaitForAssertion(() =>
            cut.FindAll(".toast").Should().BeEmpty());
    }

    [Fact]
    public void Dispose_UnsubscribesFromEvents()
    {
        var cut = RenderComponent<ToastContainer>();
        cut.Dispose();
    }
}
