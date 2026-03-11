using System.Text.RegularExpressions;

namespace WhatsAppCloudApi.Shared.Utilities;

public static class PhoneNumberNormalizer
{
    private static readonly Regex AllowedDigitsRegex = new("^[0-9]{6,20}$", RegexOptions.Compiled);

    public static string? Normalize(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        var digitsOnly = new string(value.Trim().Where(char.IsDigit).ToArray());
        return AllowedDigitsRegex.IsMatch(digitsOnly) ? digitsOnly : null;
    }

    public static bool AreEquivalent(string? left, string? right)
    {
        var normalizedLeft = Normalize(left);
        var normalizedRight = Normalize(right);
        return normalizedLeft is not null
            && normalizedRight is not null
            && string.Equals(normalizedLeft, normalizedRight, StringComparison.Ordinal);
    }
}
