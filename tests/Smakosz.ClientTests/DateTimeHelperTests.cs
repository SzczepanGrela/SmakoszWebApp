using FluentAssertions;
using Smakosz.Client.Helpers;

namespace Smakosz.ClientTests;

public class DateTimeHelperTests
{
    [Fact]
    public void FormatDate_ReturnsPolishShortFormat()
    {
        var date = new DateTime(2024, 3, 15);
        var result = DateTimeHelper.FormatDate(date);
        result.Should().Be("15 mar 2024");
    }

    [Fact]
    public void FormatDate_January()
    {
        var date = new DateTime(2024, 1, 1);
        var result = DateTimeHelper.FormatDate(date);
        result.Should().Be("1 sty 2024");
    }

    [Fact]
    public void FormatDate_December()
    {
        var date = new DateTime(2024, 12, 31);
        var result = DateTimeHelper.FormatDate(date);
        result.Should().Be("31 gru 2024");
    }

    [Fact]
    public void FormatDateFull_ReturnsPolishFullMonthName()
    {
        var date = new DateTime(2024, 3, 15);
        var result = DateTimeHelper.FormatDateFull(date);
        result.Should().Be("15 marca 2024");
    }

    [Fact]
    public void FormatDateFull_January()
    {
        var date = new DateTime(2024, 1, 5);
        var result = DateTimeHelper.FormatDateFull(date);
        result.Should().Be("5 stycznia 2024");
    }

    [Fact]
    public void TimeAgo_JustNow()
    {
        var date = DateTime.UtcNow.AddSeconds(-10);
        var result = DateTimeHelper.TimeAgo(date);
        result.Should().Be("przed chwila");
    }

    [Fact]
    public void TimeAgo_Minutes()
    {
        var date = DateTime.UtcNow.AddMinutes(-15);
        var result = DateTimeHelper.TimeAgo(date);
        result.Should().Be("15 min temu");
    }

    [Fact]
    public void TimeAgo_Hours()
    {
        var date = DateTime.UtcNow.AddHours(-3);
        var result = DateTimeHelper.TimeAgo(date);
        result.Should().Be("3 godz. temu");
    }

    [Fact]
    public void TimeAgo_Days()
    {
        var date = DateTime.UtcNow.AddDays(-5);
        var result = DateTimeHelper.TimeAgo(date);
        result.Should().Be("5 dni temu");
    }

    [Fact]
    public void TimeAgo_Weeks()
    {
        var date = DateTime.UtcNow.AddDays(-14);
        var result = DateTimeHelper.TimeAgo(date);
        result.Should().Be("2 tyg. temu");
    }

    [Fact]
    public void TimeAgo_Months()
    {
        var date = DateTime.UtcNow.AddDays(-60);
        var result = DateTimeHelper.TimeAgo(date);
        result.Should().Be("2 mies. temu");
    }

    [Fact]
    public void TimeAgo_OldDate_FallsBackToFormatDate()
    {
        var date = new DateTime(2020, 6, 15);
        var result = DateTimeHelper.TimeAgo(date);
        result.Should().Be("15 cze 2020");
    }

    [Fact]
    public void FormatDateOnly_WithValidDateString_ReturnsFormatted()
    {
        var result = DateTimeHelper.FormatDateOnly("2024-03-15");
        result.Should().Be("15 mar 2024");
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void FormatDateOnly_WithNullOrEmpty_ReturnsDash(string? input)
    {
        var result = DateTimeHelper.FormatDateOnly(input);
        result.Should().Be("-");
    }

    [Fact]
    public void FormatDateOnly_WithInvalidString_ReturnsOriginal()
    {
        var result = DateTimeHelper.FormatDateOnly("not-a-date");
        result.Should().Be("not-a-date");
    }
}
