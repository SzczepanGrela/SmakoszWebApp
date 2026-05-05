using System.Text.RegularExpressions;

namespace Smakosz.Domain.Common;

public static class PhoneNumberNormalizer
{
    private static readonly Regex StripChars = new(@"[ \-\(\)]", RegexOptions.Compiled);
    private static readonly Regex NineDigits = new(@"^[0-9]{9}$", RegexOptions.Compiled);
    private static readonly Regex DoubleZeroPrefix = new(@"^00", RegexOptions.Compiled);
    private static readonly Regex E164 = new(@"^\+[0-9]{7,15}$", RegexOptions.Compiled);

    public static string Normalize(string phone)
    {
        ArgumentNullException.ThrowIfNull(phone);

        var stripped = StripChars.Replace(phone, string.Empty);

        if (NineDigits.IsMatch(stripped))
            stripped = "+48" + stripped;

        if (DoubleZeroPrefix.IsMatch(stripped))
            stripped = "+" + stripped[2..];

        if (!E164.IsMatch(stripped))
            throw new ArgumentException("Nieprawidlowy format numeru telefonu. Wymagany E.164.", nameof(phone));

        return stripped;
    }
}
