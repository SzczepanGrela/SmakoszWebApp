using Microsoft.AspNetCore.Components;
using Smakosz.Client.Components;
using Smakosz.ClientTests.Common;

namespace Smakosz.ClientTests.Components;

public class SearchAutocompleteTests : BunitTestBase
{
    private static List<SuggestItemDto> CreateSuggestions() =>
    [
        new()
        {
            Type = "dish", Name = "Pizza Margherita", Slug = "pizza-margherita",
            Subtitle = "Pizzeria Roma", ImageUrl = "https://example.com/pizza_tiny.webp"
        },
        new()
        {
            Type = "restaurant", Name = "Pizzeria Roma", Slug = "pizzeria-roma",
            Subtitle = "Włoska", ImageUrl = "https://example.com/restaurant_tiny.webp"
        }
    ];

    [Fact]
    public void RendersInputWithPlaceholder()
    {
        var cut = RenderComponent<SearchAutocomplete>(p => p
            .Add(c => c.Placeholder, "Szukaj..."));

        var input = cut.Find("input[type='search']");
        input.Should().NotBeNull();
        input.GetAttribute("placeholder").Should().Be("Szukaj...");
    }

    [Fact]
    public void ShortInput_NoDropdown()
    {
        var searchService = Services.GetRequiredService<ISearchService>();
        var cut = RenderComponent<SearchAutocomplete>();

        cut.Find("input").Input("a");

        cut.FindAll(".autocomplete-dropdown").Should().BeEmpty();
        searchService.DidNotReceive().SuggestAsync(Arg.Any<string>(), Arg.Any<int>());
    }

    [Fact]
    public void SuggestionsRendered_AfterDebounce()
    {
        var searchService = Services.GetRequiredService<ISearchService>();
        searchService.SuggestAsync(Arg.Any<string>(), Arg.Any<int>())
            .Returns(CreateSuggestions());

        var cut = RenderComponent<SearchAutocomplete>();
        cut.Find("input").Input("pizza");

        cut.WaitForState(() => cut.Markup.Contains("Pizza Margherita"), TimeSpan.FromSeconds(3));
        cut.Markup.Should().Contain("Pizza Margherita");
        cut.Markup.Should().Contain("Pizzeria Roma");
    }

    [Fact]
    public void ClickSuggestion_Navigates()
    {
        var searchService = Services.GetRequiredService<ISearchService>();
        searchService.SuggestAsync(Arg.Any<string>(), Arg.Any<int>())
            .Returns(CreateSuggestions());

        var nav = Services.GetRequiredService<Bunit.TestDoubles.FakeNavigationManager>();
        var cut = RenderComponent<SearchAutocomplete>();
        cut.Find("input").Input("pizza");

        cut.WaitForState(() => cut.Markup.Contains("Pizza Margherita"), TimeSpan.FromSeconds(3));
        cut.Find(".autocomplete-item").Click();

        nav.Uri.Should().Contain("/dishes/pizza-margherita");
    }

    [Fact]
    public void EnterWithoutSelection_FiresOnSearch()
    {
        var searchCalled = false;
        var cut = RenderComponent<SearchAutocomplete>(p => p
            .Add(c => c.OnSearch, EventCallback.Factory.Create<string>(this, _ => searchCalled = true)));

        cut.Find("input").Input("test query");
        cut.Find("input").KeyDown(Key.Enter);

        searchCalled.Should().BeTrue();
    }

    [Fact]
    public void EscapeKey_ClosesDropdown()
    {
        var searchService = Services.GetRequiredService<ISearchService>();
        searchService.SuggestAsync(Arg.Any<string>(), Arg.Any<int>())
            .Returns(CreateSuggestions());

        var cut = RenderComponent<SearchAutocomplete>();
        cut.Find("input").Input("pizza");

        cut.WaitForState(() => cut.Markup.Contains("Pizza Margherita"), TimeSpan.FromSeconds(3));
        cut.FindAll(".autocomplete-dropdown").Should().NotBeEmpty();

        cut.Find("input").KeyDown(Key.Escape);

        cut.FindAll(".autocomplete-dropdown").Should().BeEmpty();
    }

    [Fact]
    public void ArrowDown_SelectsFirstItem()
    {
        var searchService = Services.GetRequiredService<ISearchService>();
        searchService.SuggestAsync(Arg.Any<string>(), Arg.Any<int>())
            .Returns(CreateSuggestions());

        var cut = RenderComponent<SearchAutocomplete>();
        cut.Find("input").Input("pizza");

        cut.WaitForState(() => cut.Markup.Contains("Pizza Margherita"), TimeSpan.FromSeconds(3));

        cut.Find("input").KeyDown(Key.Down);

        cut.FindAll(".autocomplete-item.active").Should().HaveCount(1);
    }
}
