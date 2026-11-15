namespace ThinkOnErp.Domain.Entities.Hr;

public static class CountryCodeRules
{
    // Store a normalized ISO 3166-1 alpha-2-shaped key without embedding a country list
    // or inferring a code from the employee's free-text nationality.
    public static string? NormalizeOptional(string? value)
    {
        if (value == null)
        {
            return null;
        }

        string normalized = value.Trim();
        return normalized.Length == 0
            ? string.Empty
            : normalized.Length != 2 || normalized.Any(c => c is not (>= 'A' and <= 'Z') and not (>= 'a' and <= 'z'))
            ? throw new ArgumentException("Country code must contain exactly two ASCII letters.")
            : normalized.ToUpperInvariant();
    }
}
