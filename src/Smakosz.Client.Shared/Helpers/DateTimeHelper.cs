namespace Smakosz.Client.Helpers;

public static class DateTimeHelper
{
    private static readonly string[] PolishMonths = { "sty", "lut", "mar", "kwi", "maj", "cze", "lip", "sie", "wrz", "paz", "lis", "gru" };
    private static readonly string[] PolishMonthsFull = { "stycznia", "lutego", "marca", "kwietnia", "maja", "czerwca", "lipca", "sierpnia", "wrzesnia", "pazdziernika", "listopada", "grudnia" };

    public static string FormatDate(DateTime date)
        => $"{date.Day} {PolishMonths[date.Month - 1]} {date.Year}";

    public static string FormatDateFull(DateTime date)
        => $"{date.Day} {PolishMonthsFull[date.Month - 1]} {date.Year}";

    public static string TimeAgo(DateTime date)
    {
        var diff = DateTime.UtcNow - date;
        if (diff.TotalMinutes < 1) return "przed chwila";
        if (diff.TotalMinutes < 60) return $"{(int)diff.TotalMinutes} min temu";
        if (diff.TotalHours < 24) return $"{(int)diff.TotalHours} godz. temu";
        if (diff.TotalDays < 7) return $"{(int)diff.TotalDays} dni temu";
        if (diff.TotalDays < 30) return $"{(int)(diff.TotalDays / 7)} tyg. temu";
        if (diff.TotalDays < 365) return $"{(int)(diff.TotalDays / 30)} mies. temu";
        return FormatDate(date);
    }

    public static string FormatDateOnly(string? dateStr)
    {
        if (string.IsNullOrWhiteSpace(dateStr)) return "-";
        if (DateTime.TryParse(dateStr, out var date)) return FormatDate(date);
        return dateStr;
    }
}
