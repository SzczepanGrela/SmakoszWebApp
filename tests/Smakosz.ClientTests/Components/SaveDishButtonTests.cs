using Smakosz.ClientTests.Common;

namespace Smakosz.ClientTests.Components;

public class SaveDishButtonTests : BunitTestBase
{
    [Fact]
    public void NotSaved_OutlineStyle()
    {
        var cut = RenderComponent<SaveDishButton>(p => p.Add(c => c.IsSaved, false));

        cut.Find("button").ClassList.Should().Contain("btn-outline-danger");
        cut.Find("i").ClassList.Should().Contain("fa-regular");
    }

    [Fact]
    public void Saved_FilledStyle()
    {
        var cut = RenderComponent<SaveDishButton>(p => p.Add(c => c.IsSaved, true));

        cut.Find("button").ClassList.Should().Contain("btn-danger");
        cut.Find("i").ClassList.Should().Contain("fa-solid");
    }

    [Fact]
    public void Click_InvokesOnToggle()
    {
        var toggled = false;
        var cut = RenderComponent<SaveDishButton>(p => p
            .Add(c => c.IsSaved, false)
            .Add(c => c.OnToggle, () => toggled = true));

        cut.Find("button").Click();
        toggled.Should().BeTrue();
    }
}
