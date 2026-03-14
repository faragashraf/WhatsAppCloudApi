using System.Text.RegularExpressions;

namespace WhatsAppCloudApi.Shared.Utilities;

public static class PhoneNumberNormalizer
{
    private static readonly Regex AllowedDigitsRegex = new("^[0-9]{6,20}$", RegexOptions.Compiled);
    private static readonly Regex EgyptLocalMobileRegex = new("^01[0-9]{9}$", RegexOptions.Compiled);
    private static readonly Regex EgyptMobileWithoutPrefixRegex = new("^1[0-9]{9}$", RegexOptions.Compiled);
    private const string EgyptCountryCode = "20";

    public static string? Normalize(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        var digitsOnly = new string(value.Trim().Where(char.IsDigit).ToArray());
        if (!AllowedDigitsRegex.IsMatch(digitsOnly))
        {
            return null;
        }

        return CanonicalizeDigits(digitsOnly);
    }

    public static IReadOnlyCollection<string> GetEquivalentForms(string? value)
    {
        var normalized = Normalize(value);
        if (normalized is null)
        {
            return Array.Empty<string>();
        }

        var forms = new HashSet<string>(StringComparer.Ordinal)
        {
            normalized,
            $"00{normalized}"
        };

        if (normalized.StartsWith(EgyptCountryCode, StringComparison.Ordinal)
            && normalized.Length == 12
            && normalized[2] == '1')
        {
            var local = normalized[2..];
            forms.Add($"0{local}");
            forms.Add(local);
        }

        return forms.ToArray();
    }

    public static bool AreEquivalent(string? left, string? right)
    {
        var normalizedLeft = Normalize(left);
        var normalizedRight = Normalize(right);
        return normalizedLeft is not null
            && normalizedRight is not null
            && string.Equals(normalizedLeft, normalizedRight, StringComparison.Ordinal);
    }

    private static string CanonicalizeDigits(string digitsOnly)
    {
        var normalized = digitsOnly;

        while (normalized.StartsWith("00", StringComparison.Ordinal) && normalized.Length > 2)
        {
            normalized = normalized[2..];
        }

        if (EgyptLocalMobileRegex.IsMatch(normalized))
        {
            return $"{EgyptCountryCode}{normalized[1..]}";
        }

        if (EgyptMobileWithoutPrefixRegex.IsMatch(normalized))
        {
            return $"{EgyptCountryCode}{normalized}";
        }

        return normalized;
    }
}
