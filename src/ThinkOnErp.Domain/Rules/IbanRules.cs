namespace ThinkOnErp.Domain.Rules;

/// <summary>
/// Validates the country-neutral IBAN syntax and MOD 97-10 checksum.
/// Country-specific lengths and bank file rules belong to a configured bank profile.
/// </summary>
public static class IbanRules
{
    public static bool TryNormalize(string? value, out string normalized)
    {
        normalized = string.Empty;
        if (string.IsNullOrWhiteSpace(value))
        {
            return false;
        }

        string candidate = new string(value.Where(c => !char.IsWhiteSpace(c)).ToArray()).ToUpperInvariant();
        if (candidate.Length is < 15 or > 34 ||
            !IsAsciiLetter(candidate[0]) || !IsAsciiLetter(candidate[1]) ||
            !IsAsciiDigit(candidate[2]) || !IsAsciiDigit(candidate[3]))
        {
            return false;
        }

        for (int i = 4; i < candidate.Length; i++)
        {
            if (!IsAsciiLetter(candidate[i]) && !IsAsciiDigit(candidate[i]))
            {
                return false;
            }
        }

        // Stream the rearranged IBAN through mod 97 without constructing a large integer.
        int remainder = 0;
        for (int i = 4; i < candidate.Length + 4; i++)
        {
            char c = candidate[i < candidate.Length ? i : i - candidate.Length];
            if (IsAsciiDigit(c))
            {
                remainder = ((remainder * 10) + c - '0') % 97;
                continue;
            }

            int valueForLetter = c - 'A' + 10;
            remainder = ((remainder * 10) + (valueForLetter / 10)) % 97;
            remainder = ((remainder * 10) + (valueForLetter % 10)) % 97;
        }

        if (remainder != 1)
        {
            return false;
        }

        normalized = candidate;
        return true;
    }

    private static bool IsAsciiLetter(char value)
    {
        return value is >= 'A' and <= 'Z';
    }

    private static bool IsAsciiDigit(char value)
    {
        return value is >= '0' and <= '9';
    }
}
