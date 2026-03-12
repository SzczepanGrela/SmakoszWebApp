using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;

namespace Smakosz.Infrastructure.Persistence;

public static partial class SlugGenerator
{
    public static string GenerateSlug(string input)
    {
        if (string.IsNullOrWhiteSpace(input))
            return string.Empty;

        var normalized = input.Normalize(NormalizationForm.FormD);
        var sb = new StringBuilder(normalized.Length);

        foreach (var c in normalized)
        {
            if (c is 'ł' or 'Ł')
            {
                sb.Append('l');
                continue;
            }

            var category = CharUnicodeInfo.GetUnicodeCategory(c);
            if (category != UnicodeCategory.NonSpacingMark)
                sb.Append(c);
        }

        var result = sb.ToString().Normalize(NormalizationForm.FormC);

        result = result.ToLowerInvariant();

        result = NonAlphanumericRegex().Replace(result, "");

        result = WhitespaceRegex().Replace(result, "-");

        result = result.Trim('-');

        return result;
    }

    [GeneratedRegex(@"[^a-z0-9\s-]")]
    private static partial Regex NonAlphanumericRegex();

    [GeneratedRegex(@"\s+")]
    private static partial Regex WhitespaceRegex();
}
