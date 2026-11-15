namespace ThinkOnErp.Domain.Entities.Hr;

public static class EmployeeDependentRules
{
    public static void NormalizeAndValidate(EmployeeDependent dependent)
    {
        ArgumentNullException.ThrowIfNull(dependent);

        dependent.EmployeeCode = Required(dependent.EmployeeCode, nameof(dependent.EmployeeCode), 50);
        dependent.NameLocal = Required(dependent.NameLocal, nameof(dependent.NameLocal), 200);
        dependent.NameEn = Required(dependent.NameEn, nameof(dependent.NameEn), 200);
        dependent.Relationship = Required(dependent.Relationship, nameof(dependent.Relationship), 30);
        dependent.Gender = Required(dependent.Gender, nameof(dependent.Gender), 10);
        dependent.CreationUser = Required(dependent.CreationUser, nameof(dependent.CreationUser), 100);
        dependent.NationalId = Optional(dependent.NationalId);
        dependent.UpdateUser = Optional(dependent.UpdateUser);

        if (dependent.NationalId?.Length > 50)
        {
            throw new ArgumentException($"{nameof(dependent.NationalId)} exceeds 50 characters.");
        }

        if (dependent.UpdateUser?.Length > 100)
        {
            throw new ArgumentException($"{nameof(dependent.UpdateUser)} exceeds 100 characters.");
        }

        dependent.DateOfBirth = EmployeeProfileRules.Day(dependent.DateOfBirth);
        if (dependent.DateOfBirth == default || dependent.DateOfBirth.Date > DateTime.UtcNow.Date)
        {
            throw new ArgumentException("Dependent date of birth is required and cannot be in the future.");
        }
    }

    private static string Required(string? value, string field, int max)
    {
        string? normalized = value?.Trim();
        return string.IsNullOrWhiteSpace(normalized) || normalized.Length > max
            ? throw new ArgumentException($"{field} is required and must not exceed {max} characters.")
            : normalized;
    }

    private static string? Optional(string? value)
    {
        return string.IsNullOrWhiteSpace(value) ? null : value.Trim();
    }
}
