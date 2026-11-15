using System.Net.Mail;
namespace ThinkOnErp.Domain.Entities.Hr;

public static class EmployeeProfileRules
{
    public static string Required(string? value, string field, int max)
    {
        string? normalized = value?.Trim();
        return string.IsNullOrWhiteSpace(normalized) || normalized.Length > max
            ? throw new ArgumentException($"{field} is required and must not exceed {max} characters.")
            : normalized;
    }
    public static string? Optional(string? value)
    {
        return string.IsNullOrWhiteSpace(value) ? null : value.Trim();
    }

    public static DateTime Day(DateTime value)
    {
        return DateTime.SpecifyKind(value.Date, DateTimeKind.Unspecified);
    }

    public static DateTime? Day(DateTime? value)
    {
        return value.HasValue ? Day(value.Value) : null;
    }

    public static void Validate(Employee e)
    {
        _ = Required(e.EmployeeCode, nameof(e.EmployeeCode), 50); _ = Required(e.NameLocal, nameof(e.NameLocal), 200); _ = Required(e.NameEn, nameof(e.NameEn), 200);
        _ = Required(e.NationalId, nameof(e.NationalId), 50); _ = Required(e.Nationality, nameof(e.Nationality), 100);
        _ = Required(e.Gender, nameof(e.Gender), 10); _ = Required(e.MaritalStatus, nameof(e.MaritalStatus), 20); _ = Required(e.EmploymentType, nameof(e.EmploymentType), 30);
        if (e.EmploymentStatus is not ("ACTIVE" or "SUSPENDED" or "TERMINATED"))
        {
            throw new ArgumentException("EmploymentStatus must be ACTIVE, SUSPENDED or TERMINATED.");
        }

        if (e.DateOfBirth == default || e.HireDate == default || e.DateOfBirth.Date > DateTime.UtcNow.Date || e.DateOfBirth.Date > e.HireDate.Date)
        {
            throw new ArgumentException("Birth and hire dates are required; birth cannot be in the future or after hire.");
        }

        if (e.ProbationEndDate?.Date < e.HireDate.Date || e.TerminationDate?.Date < e.HireDate.Date)
        {
            throw new ArgumentException("Probation/termination dates cannot precede hire date.");
        }

        if (e.EmploymentStatus == "TERMINATED" && (!e.TerminationDate.HasValue || e.IsActive))
        {
            throw new ArgumentException("A terminated employee requires a termination date and inactive state.");
        }

        if (e.TaxExemptionCount < 0 || e.BranchId <= 0)
        {
            throw new ArgumentException("Tax exemption count must be nonnegative and branch ID positive when supplied.");
        }

        if (e.ManagerEmployeeCode == e.EmployeeCode)
        {
            throw new ArgumentException("Employee cannot be their own manager.");
        }

        (string? Value, string Name, int Max)[] optional = new (string? Value, string Name, int Max)[] {
            (e.PassportNumber, nameof(e.PassportNumber), 50), (e.Email, nameof(e.Email), 100), (e.Phone, nameof(e.Phone), 50),
            (e.TerminationReason, nameof(e.TerminationReason), 500), (e.DepartmentCode, nameof(e.DepartmentCode), 50), (e.PositionCode, nameof(e.PositionCode), 50),
            (e.ManagerEmployeeCode, nameof(e.ManagerEmployeeCode), 50), (e.SscNumber, nameof(e.SscNumber), 50), (e.BankName, nameof(e.BankName), 100),
            (e.BankAccountNumber, nameof(e.BankAccountNumber), 50), (e.BankIban, nameof(e.BankIban), 50) };
        foreach ((string? Value, string? Name, int Max) in optional)
        {
            if (Value?.Length > Max)
            {
                throw new ArgumentException($"{Name} exceeds {Max} characters.");
            }
        }

        if (e.Email != null && (!MailAddress.TryCreate(e.Email, out MailAddress? mail) || !string.Equals(mail.Address, e.Email, StringComparison.OrdinalIgnoreCase)))
        {
            throw new ArgumentException("Email must contain a single valid address.");
        }
    }
}
