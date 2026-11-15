namespace ThinkOnErp.Domain.Entities.Hr;

/// <summary>Validates and resolves a salary structure line against its configured component.</summary>
public static class SalaryStructureLineRules
{
    public static decimal ResolveInputAmount(decimal? requestedAmount, SalaryComponent component)
    {
        ArgumentNullException.ThrowIfNull(component);
        var calculationType = component.CalculationType?.Trim().ToUpperInvariant();
        if (calculationType == "FIXED")
            return requestedAmount ?? component.DefaultAmount
                ?? throw new ArgumentException($"Fixed salary component '{component.ComponentCode}' requires an amount or configured default amount.");
        if (calculationType == "PERCENTAGE")
            return requestedAmount ?? 0m;
        if (calculationType == "FORMULA")
            throw new ArgumentException($"Formula salary component '{component.ComponentCode}' cannot be assigned because no formula expression is configured.");
        throw new ArgumentException($"Salary component '{component.ComponentCode}' has an unsupported calculation type.");
    }

    public static void ValidateAssignment(SalaryStructureLine line, SalaryComponent component, decimal basicSalary)
    {
        ArgumentNullException.ThrowIfNull(component);
        if (!component.IsActive)
            throw new ArgumentException($"Salary component '{component.ComponentCode}' is inactive.");
        _ = ResolveAmount(line, component, basicSalary);
    }

    public static decimal ResolveAmount(SalaryStructureLine line, SalaryComponent component, decimal basicSalary)
    {
        ArgumentNullException.ThrowIfNull(line);
        ArgumentNullException.ThrowIfNull(component);

        var componentType = component.ComponentType?.Trim().ToUpperInvariant();
        if (componentType is not ("ALLOWANCE" or "EARNING" or "DEDUCTION"))
            throw new InvalidOperationException($"Salary component '{component.ComponentCode}' has a type that cannot be assigned to a salary structure line.");

        ValidateAmount(line.Amount);
        var calculationType = component.CalculationType?.Trim().ToUpperInvariant();
        if (calculationType == "FIXED")
        {
            if (line.Percent.HasValue && line.Percent.Value != 0m)
                throw new InvalidOperationException($"Fixed salary component '{component.ComponentCode}' cannot have a line percentage.");
            if (line.Amount < 0m)
                throw new InvalidOperationException($"Salary structure line '{component.ComponentCode}' cannot have a negative amount.");
            return line.Amount;
        }

        if (calculationType == "PERCENTAGE")
        {
            var percent = line.Percent ?? component.DefaultPercent;
            if (!percent.HasValue || percent.Value <= 0m)
                throw new InvalidOperationException($"Percentage salary component '{component.ComponentCode}' requires a positive line or default percentage.");
            if (line.Percent.HasValue && (line.Percent.Value >= 1000m || line.Percent.Value % 0.01m != 0m))
                throw new InvalidOperationException($"Salary structure line percentage for '{component.ComponentCode}' does not fit NUMBER(5,2).");
            if (!line.Percent.HasValue && (percent.Value >= 10000m || percent.Value % 0.0001m != 0m))
                throw new InvalidOperationException($"Default percentage for '{component.ComponentCode}' does not fit NUMBER(8,4).");
            return basicSalary * (percent.Value / 100m);
        }

        if (calculationType == "FORMULA")
            throw new ArgumentException($"Formula salary component '{component.ComponentCode}' cannot be assigned because no formula expression is configured.");

        throw new InvalidOperationException($"Salary component '{component.ComponentCode}' has an unsupported calculation type.");
    }

    private static void ValidateAmount(decimal amount)
    {
        if (amount < 0m || amount >= 1_000_000_000_000_000m || amount % 0.001m != 0m)
            throw new InvalidOperationException("Salary structure line amount must be non-negative and fit NUMBER(18,3).");
    }
}
