namespace ThinkOnErp.Domain.Entities.Hr;

public static class SalaryComponentRules
{
    public static void NormalizeAndValidate(SalaryComponent component)
    {
        ArgumentNullException.ThrowIfNull(component);
        component.ComponentCode = Required(component.ComponentCode, nameof(component.ComponentCode), 50).ToUpperInvariant();
        component.NameLocal = Required(component.NameLocal, nameof(component.NameLocal), 200);
        component.NameEn = Required(component.NameEn, nameof(component.NameEn), 200);
        component.ComponentType = Required(component.ComponentType, nameof(component.ComponentType), 30).ToUpperInvariant();
        component.CalculationType = Required(component.CalculationType, nameof(component.CalculationType), 30).ToUpperInvariant();
        component.GlAccountCode = Optional(component.GlAccountCode);
        component.CreationUser = Required(component.CreationUser, nameof(component.CreationUser), 100);
        component.UpdateUser = Optional(component.UpdateUser);

        if (component.UpdateUser?.Length > 100)
            throw new ArgumentException($"{nameof(component.UpdateUser)} exceeds 100 characters.");
        if (component.ComponentType is not ("BASIC" or "ALLOWANCE" or "EARNING" or "DEDUCTION" or "EMPLOYER_CONTRIB"))
            throw new ArgumentException("ComponentType must be BASIC, ALLOWANCE, EARNING, DEDUCTION or EMPLOYER_CONTRIB.");
        if (component.CalculationType is not ("FIXED" or "PERCENTAGE" or "FORMULA"))
            throw new ArgumentException("CalculationType must be FIXED, PERCENTAGE or FORMULA.");
        ValidateNumber(component.DefaultAmount, 14, nameof(component.DefaultAmount));
        ValidateNumber(component.DefaultPercent, 4, nameof(component.DefaultPercent));
        if (component.GlAccountCode?.Length > 50)
            throw new ArgumentException($"{nameof(component.GlAccountCode)} exceeds 50 characters.");
    }

    private static string Required(string? value, string field, int max)
    {
        var normalized = value?.Trim();
        if (string.IsNullOrWhiteSpace(normalized) || normalized.Length > max)
            throw new ArgumentException($"{field} is required and must not exceed {max} characters.");
        return normalized;
    }

    private static string? Optional(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    private static void ValidateNumber(decimal? value, int integerDigits, string field)
    {
        if (!value.HasValue) return;
        var limit = integerDigits == 14 ? 100_000_000_000_000m : 10_000m;
        if (value.Value <= -limit || value.Value >= limit || value.Value % 0.0001m != 0m)
            throw new ArgumentOutOfRangeException(field, $"{field} must fit its configured precision and scale (maximum 4 decimal places).");
    }
}
