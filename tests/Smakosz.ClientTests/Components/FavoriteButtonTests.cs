using Smakosz.ClientTests.Common;

namespace Smakosz.ClientTests.Components;

public class FavoriteButtonTests : BunitTestBase
{
    [Fact]
    public void NotFavorite_ShowsDodajDoUlubionych()
    {
        var cut = RenderComponent<FavoriteButton>(p => p.Add(c => c.IsFavorite, false));

        cut.Find("button").TextContent.Should().Contain("Dodaj do ulubionych");
        cut.Find("button").ClassList.Should().Contain("btn-outline-danger");
        cut.Find("i").ClassList.Should().Contain("fa-regular");
    }

    [Fact]
    public void Favorite_ShowsUlubiona()
    {
        var cut = RenderComponent<FavoriteButton>(p => p.Add(c => c.IsFavorite, true));

        cut.Find("button").TextContent.Should().Contain("Ulubiona");
        cut.Find("button").ClassList.Should().Contain("btn-danger");
        cut.Find("i").ClassList.Should().Contain("fa-solid");
    }

    [Fact]
    public void Click_InvokesOnToggle()
    {
        var toggled = false;
        var cut = RenderComponent<FavoriteButton>(p => p
            .Add(c => c.IsFavorite, false)
            .Add(c => c.OnToggle, () => toggled = true));

        cut.Find("button").Click();
        toggled.Should().BeTrue();
    }
}
