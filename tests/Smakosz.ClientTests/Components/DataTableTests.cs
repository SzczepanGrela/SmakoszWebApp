using Smakosz.ClientTests.Common;

namespace Smakosz.ClientTests.Components;

public class DataTableTests : BunitTestBase
{
    [Fact]
    public void WithItems_RendersRows()
    {
        var items = new List<string> { "Apple", "Banana", "Cherry" };

        var cut = RenderComponent<DataTable<string>>(p => p
            .Add(c => c.Items, items)
            .Add(c => c.HeaderContent, b => b.AddMarkupContent(0, "<th>Nazwa</th>"))
            .Add(c => c.RowContent, item => b => b.AddMarkupContent(0, $"<td>{item}</td>")));

        cut.FindAll("tbody tr").Should().HaveCount(3);
        cut.Markup.Should().Contain("Apple");
        cut.Markup.Should().Contain("Banana");
        cut.Markup.Should().Contain("Cherry");
    }

    [Fact]
    public void EmptyItems_ShowsBrakDanych()
    {
        var cut = RenderComponent<DataTable<string>>(p => p
            .Add(c => c.Items, new List<string>())
            .Add(c => c.RowContent, item => b => b.AddMarkupContent(0, $"<td>{item}</td>")));

        cut.Markup.Should().Contain("Brak danych");
    }

    [Fact]
    public void NullItems_ShowsBrakDanych()
    {
        var cut = RenderComponent<DataTable<string>>(p => p
            .Add(c => c.RowContent, item => b => b.AddMarkupContent(0, $"<td>{item}</td>")));

        cut.Markup.Should().Contain("Brak danych");
    }

    [Fact]
    public void RendersHeaderContent()
    {
        var cut = RenderComponent<DataTable<string>>(p => p
            .Add(c => c.Items, new List<string> { "Test" })
            .Add(c => c.HeaderContent, b => b.AddMarkupContent(0, "<th>Kolumna1</th><th>Kolumna2</th>"))
            .Add(c => c.RowContent, item => b => b.AddMarkupContent(0, $"<td>{item}</td>")));

        cut.FindAll("thead th").Should().HaveCount(2);
    }
}
