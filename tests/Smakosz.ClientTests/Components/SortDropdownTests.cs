using Smakosz.ClientTests.Common;

namespace Smakosz.ClientTests.Components;

public class SortDropdownTests : BunitTestBase
{
    private static List<SortOption> TestOptions =>
    [
        new() { Label = "Najnowsze", Value = "newest" },
        new() { Label = "Najlepsze", Value = "rating" },
        new() { Label = "Cena", Value = "price" }
    ];

    [Fact]
    public void RendersAllOptions()
    {
        var cut = RenderComponent<SortDropdown>(p => p
            .Add(c => c.Options, TestOptions)
            .Add(c => c.CurrentSort, "newest"));

        cut.FindAll(".dropdown-item").Should().HaveCount(3);
    }

    [Fact]
    public void CurrentSort_HasActiveClass()
    {
        var cut = RenderComponent<SortDropdown>(p => p
            .Add(c => c.Options, TestOptions)
            .Add(c => c.CurrentSort, "rating"));

        cut.Find(".dropdown-item.active").TextContent.Trim().Should().Be("Najlepsze");
    }

    [Fact]
    public void CurrentLabel_ShowsSelectedOption()
    {
        var cut = RenderComponent<SortDropdown>(p => p
            .Add(c => c.Options, TestOptions)
            .Add(c => c.CurrentSort, "price"));

        cut.Find("button.dropdown-toggle").TextContent.Should().Contain("Cena");
    }

    [Fact]
    public void NoMatchingSort_ShowsDefault()
    {
        var cut = RenderComponent<SortDropdown>(p => p
            .Add(c => c.Options, TestOptions)
            .Add(c => c.CurrentSort, "unknown"));

        cut.Find("button.dropdown-toggle").TextContent.Should().Contain("Sortuj");
    }

    [Fact]
    public void ClickOption_InvokesOnSortChange()
    {
        string? selected = null;
        var cut = RenderComponent<SortDropdown>(p => p
            .Add(c => c.Options, TestOptions)
            .Add(c => c.CurrentSort, "newest")
            .Add(c => c.OnSortChange, (string v) => selected = v));

        cut.FindAll(".dropdown-item")[1].Click(); // "rating"
        selected.Should().Be("rating");
    }
}
