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

        // 1. Normalize unicode and remove diacritics (ł->l, ó->o, ę->e, ą->a, etc.)
        var normalized = input.Normalize(NormalizationForm.FormD);
        var sb = new StringBuilder(normalized.Length);

        foreach (var c in normalized)
        {
            // Special case: Polish ł/Ł is not decomposed by FormD
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

        // 2. Lowercase
        result = result.ToLowerInvariant();

        // 3. Remove non-alphanumeric except spaces and hyphens
        result = NonAlphanumericRegex().Replace(result, "");

        // 4. Replace whitespace sequences with single hyphen
        result = WhitespaceRegex().Replace(result, "-");

        // 5. Trim leading/trailing hyphens
        result = result.Trim('-');

        return result;
    }

    [GeneratedRegex(@"[^a-z0-9\s-]")]
    private static partial Regex NonAlphanumericRegex();

    [GeneratedRegex(@"\s+")]
    private static partial Regex WhitespaceRegex();
}
