using Smakosz.ClientTests.Common;

namespace Smakosz.ClientTests.Components;

public class OpeningHoursEditorTests : BunitTestBase
{
    private static List<OpeningHoursDto> CreateWeek() =>
        Enumerable.Range(0, 7).Select(d => new OpeningHoursDto
        {
            DayOfWeek = d,
            OpenTime = "10:00",
            CloseTime = "22:00",
            IsClosed = d == 0 // Sunday closed
        }).ToList();

    [Fact]
    public void RendersAllDays()
    {
        var hours = CreateWeek();
        var cut = RenderComponent<OpeningHoursEditor>(p => p.Add(c => c.Hours, hours));

        cut.Markup.Should().Contain("Niedziela");
        cut.Markup.Should().Contain("Poniedzialek");
        cut.Markup.Should().Contain("Wtorek");
        cut.Markup.Should().Contain("Sroda");
        cut.Markup.Should().Contain("Czwartek");
        cut.Markup.Should().Contain("Piatek");
        cut.Markup.Should().Contain("Sobota");
    }

    [Fact]
    public void ClosedDay_ShowsZamkniete()
    {
        var hours = CreateWeek();
        var cut = RenderComponent<OpeningHoursEditor>(p => p.Add(c => c.Hours, hours));

        cut.Markup.Should().Contain("Zamkniete");
    }

    [Fact]
    public void OpenDay_ShowsOtwarte()
    {
        var hours = CreateWeek();
        var cut = RenderComponent<OpeningHoursEditor>(p => p.Add(c => c.Hours, hours));

        cut.Markup.Should().Contain("Otwarte");
    }

    [Fact]
    public void OpenDay_ShowsTimeInputs()
    {
        var hours = CreateWeek();
        var cut = RenderComponent<OpeningHoursEditor>(p => p.Add(c => c.Hours, hours));

        cut.FindAll("input[type='time']").Should().HaveCountGreaterOrEqualTo(12);
    }

    [Fact]
    public void ClosedDay_NoTimeInputs()
    {
        var hours = new List<OpeningHoursDto>
        {
            new() { DayOfWeek = 0, IsClosed = true }
        };

        var cut = RenderComponent<OpeningHoursEditor>(p => p.Add(c => c.Hours, hours));
        cut.FindAll("input[type='time']").Should().BeEmpty();
    }

    [Fact]
    public void ToggleDay_SwitchesIsClosed()
    {
        var hours = new List<OpeningHoursDto>
        {
            new() { DayOfWeek = 1, IsClosed = true, OpenTime = "", CloseTime = "" }
        };

        var cut = RenderComponent<OpeningHoursEditor>(p => p.Add(c => c.Hours, hours));

        cut.Find("input.form-check-input").Change(true);

        hours[0].IsClosed.Should().BeFalse();
        hours[0].OpenTime.Should().Be("10:00");
        hours[0].CloseTime.Should().Be("22:00");
    }
}
