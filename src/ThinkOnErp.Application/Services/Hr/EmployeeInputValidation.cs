using ThinkOnErp.Application.DTOs.Hr;
using ThinkOnErp.Domain.Entities.Hr;
namespace ThinkOnErp.Application.Services.Hr;
public static class EmployeeInputValidation
{
    public static CreateEmployeeDto Normalize(CreateEmployeeDto dto)
    {
        dto = dto with
        {
            EmployeeCode = EmployeeProfileRules.Required(dto.EmployeeCode, nameof(dto.EmployeeCode), 100).ToUpperInvariant(),
            NameLocal = EmployeeProfileRules.Required(dto.NameLocal, nameof(dto.NameLocal), 400),
            NameEn = EmployeeProfileRules.Required(dto.NameEn, nameof(dto.NameEn), 400),
            NationalId = EmployeeProfileRules.Required(dto.NationalId, nameof(dto.NationalId), 100),
            Nationality = EmployeeProfileRules.Required(dto.Nationality, nameof(dto.Nationality), 200),
            PassportNumber = EmployeeProfileRules.Optional(dto.PassportNumber),
            DateOfBirth = EmployeeProfileRules.Day(dto.DateOfBirth),
            Gender = EmployeeProfileRules.Required(dto.Gender, nameof(dto.Gender), 20).ToUpperInvariant(),
            MaritalStatus = EmployeeProfileRules.Required(dto.MaritalStatus, nameof(dto.MaritalStatus), 40).ToUpperInvariant(),
            Email = EmployeeProfileRules.Optional(dto.Email),
            Phone = EmployeeProfileRules.Optional(dto.Phone),
            HireDate = EmployeeProfileRules.Day(dto.HireDate),
            ProbationEndDate = EmployeeProfileRules.Day(dto.ProbationEndDate),
            EmploymentType = EmployeeProfileRules.Required(dto.EmploymentType, nameof(dto.EmploymentType), 60).ToUpperInvariant(),
            EmploymentStatus = EmployeeProfileRules.Required(dto.EmploymentStatus, nameof(dto.EmploymentStatus), 60).ToUpperInvariant(),
            DepartmentCode = EmployeeProfileRules.Optional(dto.DepartmentCode),
            PositionCode = EmployeeProfileRules.Optional(dto.PositionCode),
            ManagerEmployeeCode = EmployeeProfileRules.Optional(dto.ManagerEmployeeCode)?.ToUpperInvariant(),
            SscNumber = EmployeeProfileRules.Optional(dto.SscNumber),
            BankName = EmployeeProfileRules.Optional(dto.BankName),
            BankAccountNumber = EmployeeProfileRules.Optional(dto.BankAccountNumber),
            BankIban = EmployeeProfileRules.Optional(dto.BankIban),
            TerminationDate = EmployeeProfileRules.Day(dto.TerminationDate),
            TerminationReason = EmployeeProfileRules.Optional(dto.TerminationReason),
            CitizenshipCountryCode = CountryCodeRules.NormalizeOptional(dto.CitizenshipCountryCode),
        };
        if (string.Equals(dto.EmploymentStatus, "TERMINATED", StringComparison.OrdinalIgnoreCase) ||
            dto.TerminationDate.HasValue || !string.IsNullOrWhiteSpace(dto.TerminationReason))
            throw new ArgumentException("Employee termination must be completed through the End of Service workflow.", nameof(dto));
        var candidate = new Employee
        {
            EmployeeCode = dto.EmployeeCode,
            NameLocal = dto.NameLocal,
            NameEn = dto.NameEn,
            NationalId = dto.NationalId,
            Nationality = dto.Nationality,
            CitizenshipCountryCode = string.IsNullOrEmpty(dto.CitizenshipCountryCode) ? null : dto.CitizenshipCountryCode,
            PassportNumber = dto.PassportNumber,
            DateOfBirth = dto.DateOfBirth,
            Gender = dto.Gender,
            MaritalStatus = dto.MaritalStatus,
            Email = dto.Email,
            Phone = dto.Phone,
            HireDate = dto.HireDate,
            ProbationEndDate = dto.ProbationEndDate,
            EmploymentType = dto.EmploymentType,
            EmploymentStatus = dto.EmploymentStatus,
            DepartmentCode = dto.DepartmentCode,
            PositionCode = dto.PositionCode,
            BranchId = dto.BranchId,
            ManagerEmployeeCode = dto.ManagerEmployeeCode,
            SscNumber = dto.SscNumber,
            TaxExemptionCount = dto.TaxExemptionCount,
            IsHighRiskRole = dto.IsHighRiskRole,
            BankName = dto.BankName,
            BankAccountNumber = dto.BankAccountNumber,
            BankIban = dto.BankIban,
            TerminationDate = dto.TerminationDate,
            TerminationReason = dto.TerminationReason,
            IsActive = dto.EmploymentStatus != "TERMINATED",
        };
        EmployeeProfileRules.Validate(candidate);
        return dto;
    }
    public static UpdateEmployeeDto Normalize(string employeeCode, UpdateEmployeeDto dto)
    {
        dto = dto with
        {
            NameLocal = EmployeeProfileRules.Required(dto.NameLocal, nameof(dto.NameLocal), 400),
            NameEn = EmployeeProfileRules.Required(dto.NameEn, nameof(dto.NameEn), 400),
            NationalId = EmployeeProfileRules.Required(dto.NationalId, nameof(dto.NationalId), 100),
            Nationality = EmployeeProfileRules.Required(dto.Nationality, nameof(dto.Nationality), 200),
            PassportNumber = EmployeeProfileRules.Optional(dto.PassportNumber),
            DateOfBirth = EmployeeProfileRules.Day(dto.DateOfBirth),
            Gender = EmployeeProfileRules.Required(dto.Gender, nameof(dto.Gender), 20).ToUpperInvariant(),
            MaritalStatus = EmployeeProfileRules.Required(dto.MaritalStatus, nameof(dto.MaritalStatus), 40).ToUpperInvariant(),
            Email = EmployeeProfileRules.Optional(dto.Email),
            Phone = EmployeeProfileRules.Optional(dto.Phone),
            HireDate = EmployeeProfileRules.Day(dto.HireDate),
            ProbationEndDate = EmployeeProfileRules.Day(dto.ProbationEndDate),
            TerminationDate = EmployeeProfileRules.Day(dto.TerminationDate),
            TerminationReason = EmployeeProfileRules.Optional(dto.TerminationReason),
            EmploymentType = EmployeeProfileRules.Required(dto.EmploymentType, nameof(dto.EmploymentType), 60).ToUpperInvariant(),
            EmploymentStatus = EmployeeProfileRules.Required(dto.EmploymentStatus, nameof(dto.EmploymentStatus), 60).ToUpperInvariant(),
            DepartmentCode = EmployeeProfileRules.Optional(dto.DepartmentCode),
            PositionCode = EmployeeProfileRules.Optional(dto.PositionCode),
            ManagerEmployeeCode = EmployeeProfileRules.Optional(dto.ManagerEmployeeCode)?.ToUpperInvariant(),
            SscNumber = EmployeeProfileRules.Optional(dto.SscNumber),
            BankName = EmployeeProfileRules.Optional(dto.BankName),
            BankAccountNumber = EmployeeProfileRules.Optional(dto.BankAccountNumber),
            BankIban = EmployeeProfileRules.Optional(dto.BankIban),
            CitizenshipCountryCode = CountryCodeRules.NormalizeOptional(dto.CitizenshipCountryCode),
        };
        var candidate = new Employee
        {
            EmployeeCode = employeeCode.Trim().ToUpperInvariant(),
            NameLocal = dto.NameLocal,
            NameEn = dto.NameEn,
            NationalId = dto.NationalId,
            Nationality = dto.Nationality,
            CitizenshipCountryCode = string.IsNullOrEmpty(dto.CitizenshipCountryCode) ? null : dto.CitizenshipCountryCode,
            PassportNumber = dto.PassportNumber,
            DateOfBirth = dto.DateOfBirth,
            Gender = dto.Gender,
            MaritalStatus = dto.MaritalStatus,
            Email = dto.Email,
            Phone = dto.Phone,
            HireDate = dto.HireDate,
            ProbationEndDate = dto.ProbationEndDate,
            TerminationDate = dto.TerminationDate,
            TerminationReason = dto.TerminationReason,
            EmploymentType = dto.EmploymentType,
            EmploymentStatus = dto.EmploymentStatus,
            DepartmentCode = dto.DepartmentCode,
            PositionCode = dto.PositionCode,
            BranchId = dto.BranchId,
            ManagerEmployeeCode = dto.ManagerEmployeeCode,
            SscNumber = dto.SscNumber,
            TaxExemptionCount = dto.TaxExemptionCount,
            IsHighRiskRole = dto.IsHighRiskRole,
            BankName = dto.BankName,
            BankAccountNumber = dto.BankAccountNumber,
            BankIban = dto.BankIban,
            IsActive = dto.IsActive,
        };
        EmployeeProfileRules.Validate(candidate);
        return dto;
    }
    public static void NormalizeImportRow(EmployeeImportRowDto row, Employee? previous)
    {
        row.EmployeeCode = EmployeeProfileRules.Required(row.EmployeeCode, nameof(row.EmployeeCode), 100).ToUpperInvariant();
        var normalized = Normalize(row.EmployeeCode, new UpdateEmployeeDto(
            previous != null && row.PresentColumns != null && !row.PresentColumns.Contains(nameof(row.NameLocal)) ? previous.NameLocal : (row.NameLocal),
            previous != null && row.SuppliedFields != null && !row.SuppliedFields.Contains(nameof(row.NameEn)) ? previous.NameEn : (EmployeeProfileRules.Optional(row.NameEn) ?? previous?.NameEn ?? row.NameLocal),
            previous != null && row.PresentColumns != null && !row.PresentColumns.Contains(nameof(row.NationalId)) ? previous.NationalId : (row.NationalId),
            previous != null && row.PresentColumns != null && !row.PresentColumns.Contains(nameof(row.Nationality)) ? previous.Nationality : (EmployeeProfileRules.Optional(row.Nationality) ?? previous?.Nationality ?? string.Empty),
            previous != null && row.PresentColumns != null && !row.PresentColumns.Contains(nameof(row.PassportNumber)) ? previous.PassportNumber : (row.PassportNumber),
            previous != null && row.PresentColumns != null && !row.PresentColumns.Contains(nameof(row.DateOfBirth)) ? previous.DateOfBirth : (row.DateOfBirth ?? previous?.DateOfBirth ?? default),
            previous != null && row.PresentColumns != null && !row.PresentColumns.Contains(nameof(row.Gender)) ? previous.Gender : (EmployeeProfileRules.Optional(row.Gender) ?? previous?.Gender ?? string.Empty),
            previous != null && row.PresentColumns != null && !row.PresentColumns.Contains(nameof(row.MaritalStatus)) ? previous.MaritalStatus : (EmployeeProfileRules.Optional(row.MaritalStatus) ?? previous?.MaritalStatus ?? string.Empty),
            previous != null && row.PresentColumns != null && !row.PresentColumns.Contains(nameof(row.Email)) ? previous.Email : (row.Email),
            previous != null && row.PresentColumns != null && !row.PresentColumns.Contains(nameof(row.Phone)) ? previous.Phone : (row.Phone),
            previous != null && row.PresentColumns != null && !row.PresentColumns.Contains(nameof(row.HireDate)) ? previous.HireDate : (row.HireDate ?? previous?.HireDate ?? default),
            previous != null && row.PresentColumns != null && !row.PresentColumns.Contains(nameof(row.ProbationEndDate)) ? previous.ProbationEndDate : (row.ProbationEndDate),
            previous != null && row.PresentColumns != null && !row.PresentColumns.Contains(nameof(row.TerminationDate)) ? previous.TerminationDate : (row.TerminationDate),
            previous != null && row.PresentColumns != null && !row.PresentColumns.Contains(nameof(row.TerminationReason)) ? previous.TerminationReason : (row.TerminationReason),
            previous != null && row.PresentColumns != null && !row.PresentColumns.Contains(nameof(row.EmploymentType)) ? previous.EmploymentType : (EmployeeProfileRules.Optional(row.EmploymentType) ?? previous?.EmploymentType ?? string.Empty),
            previous != null && row.PresentColumns != null && !row.PresentColumns.Contains(nameof(row.EmploymentStatus)) ? previous.EmploymentStatus : (EmployeeProfileRules.Optional(row.EmploymentStatus) ?? previous?.EmploymentStatus ?? string.Empty),
            previous != null && row.PresentColumns != null && !row.PresentColumns.Contains(nameof(row.DepartmentCode)) ? previous.DepartmentCode : (row.DepartmentCode),
            previous != null && row.PresentColumns != null && !row.PresentColumns.Contains(nameof(row.PositionCode)) ? previous.PositionCode : (row.PositionCode),
            previous != null && row.PresentColumns != null && !row.PresentColumns.Contains(nameof(row.BranchId)) ? previous.BranchId : (row.BranchId),
            previous != null && row.PresentColumns != null && !row.PresentColumns.Contains(nameof(row.ManagerEmployeeCode)) ? previous.ManagerEmployeeCode : (row.ManagerEmployeeCode),
            previous != null && row.PresentColumns != null && !row.PresentColumns.Contains(nameof(row.SscNumber)) ? previous.SscNumber : (row.SscNumber),
            previous != null && row.SuppliedFields != null && !row.SuppliedFields.Contains(nameof(row.TaxExemptionCount)) ? previous.TaxExemptionCount : (row.TaxExemptionCount),
            previous != null && row.SuppliedFields != null && !row.SuppliedFields.Contains(nameof(row.IsHighRiskRole)) ? previous.IsHighRiskRole : (row.IsHighRiskRole),
            previous != null && row.PresentColumns != null && !row.PresentColumns.Contains(nameof(row.BankName)) ? previous.BankName : (row.BankName),
            previous != null && row.PresentColumns != null && !row.PresentColumns.Contains(nameof(row.BankAccountNumber)) ? previous.BankAccountNumber : (row.BankAccountNumber),
            previous != null && row.PresentColumns != null && !row.PresentColumns.Contains(nameof(row.BankIban)) ? previous.BankIban : (row.BankIban),
            previous != null && row.SuppliedFields != null && !row.SuppliedFields.Contains(nameof(row.IsActive)) ? previous.IsActive : (row.IsActive),
            previous != null && (row.PresentColumns == null || !row.PresentColumns.Contains(nameof(row.CitizenshipCountryCode)))
                ? previous.CitizenshipCountryCode : CountryCodeRules.NormalizeOptional(row.CitizenshipCountryCode)));
        var wasTerminated = string.Equals(previous?.EmploymentStatus, "TERMINATED", StringComparison.OrdinalIgnoreCase);
        var willBeTerminated = string.Equals(normalized.EmploymentStatus, "TERMINATED", StringComparison.OrdinalIgnoreCase);
        var terminationFieldsChanged = normalized.TerminationDate?.Date != previous?.TerminationDate?.Date ||
            !string.Equals(normalized.TerminationReason?.Trim(), previous?.TerminationReason?.Trim(), StringComparison.Ordinal);
        if (previous == null && (willBeTerminated || normalized.TerminationDate.HasValue || !string.IsNullOrWhiteSpace(normalized.TerminationReason)) ||
            previous != null && (wasTerminated != willBeTerminated || terminationFieldsChanged))
            throw new ArgumentException("Employee termination must be completed through the End of Service workflow.", nameof(row));
        row.NameLocal = normalized.NameLocal;
        row.NameEn = normalized.NameEn;
        row.NationalId = normalized.NationalId;
        row.Nationality = normalized.Nationality;
        row.CitizenshipCountryCode = normalized.CitizenshipCountryCode;
        row.PassportNumber = normalized.PassportNumber;
        row.DateOfBirth = normalized.DateOfBirth;
        row.Gender = normalized.Gender;
        row.MaritalStatus = normalized.MaritalStatus;
        row.Email = normalized.Email;
        row.Phone = normalized.Phone;
        row.HireDate = normalized.HireDate;
        row.ProbationEndDate = normalized.ProbationEndDate;
        row.TerminationDate = normalized.TerminationDate;
        row.TerminationReason = normalized.TerminationReason;
        row.EmploymentType = normalized.EmploymentType;
        row.EmploymentStatus = normalized.EmploymentStatus;
        row.DepartmentCode = normalized.DepartmentCode;
        row.PositionCode = normalized.PositionCode;
        row.BranchId = normalized.BranchId;
        row.ManagerEmployeeCode = normalized.ManagerEmployeeCode;
        row.SscNumber = normalized.SscNumber;
        row.TaxExemptionCount = normalized.TaxExemptionCount;
        row.IsHighRiskRole = normalized.IsHighRiskRole;
        row.BankName = normalized.BankName;
        row.BankAccountNumber = normalized.BankAccountNumber;
        row.BankIban = normalized.BankIban;
        row.IsActive = normalized.IsActive;
    }
    public static Employee ImportCandidate(EmployeeImportRowDto row) => new()
    {
        EmployeeCode = row.EmployeeCode,
        NameLocal = row.NameLocal,
        NameEn = row.NameEn,
        NationalId = row.NationalId,
        Nationality = row.Nationality,
        CitizenshipCountryCode = string.IsNullOrEmpty(row.CitizenshipCountryCode) ? null : row.CitizenshipCountryCode,
        PassportNumber = row.PassportNumber,
        DateOfBirth = row.DateOfBirth!.Value,
        Gender = row.Gender,
        MaritalStatus = row.MaritalStatus,
        Email = row.Email,
        Phone = row.Phone,
        HireDate = row.HireDate!.Value,
        ProbationEndDate = row.ProbationEndDate,
        TerminationDate = row.TerminationDate,
        TerminationReason = row.TerminationReason,
        EmploymentType = row.EmploymentType,
        EmploymentStatus = row.EmploymentStatus,
        DepartmentCode = row.DepartmentCode,
        PositionCode = row.PositionCode,
        BranchId = row.BranchId,
        ManagerEmployeeCode = row.ManagerEmployeeCode,
        SscNumber = row.SscNumber,
        TaxExemptionCount = row.TaxExemptionCount,
        IsHighRiskRole = row.IsHighRiskRole,
        BankName = row.BankName,
        BankAccountNumber = row.BankAccountNumber,
        BankIban = row.BankIban,
        IsActive = row.IsActive,
    };
}
