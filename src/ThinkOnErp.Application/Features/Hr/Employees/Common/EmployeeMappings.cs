using ThinkOnErp.Application.DTOs.Hr;
using ThinkOnErp.Domain.Entities.Hr;

namespace ThinkOnErp.Application.Features.Hr.Employees.Common;

/// <summary>Entity → DTO mapping for the Employees feature. Pure functions, no permission logic.</summary>
internal static class EmployeeMappings
{
    public static SalaryStructure? ActiveStructure(Employee employee, DateTime today) =>
        employee.SalaryStructures
            .Where(s => s.IsActive && s.EffectiveFrom.Date <= today && (!s.EffectiveTo.HasValue || s.EffectiveTo.Value.Date >= today))
            .OrderByDescending(s => s.EffectiveFrom)
            .FirstOrDefault();

    public static EmployeeSummaryDto ToSummary(Employee e, DateTime today) => new(
        e.EmployeeCode,
        e.NameLocal,
        e.NameEn,
        e.NationalId,
        e.DepartmentCode,
        e.PositionCode,
        e.EmploymentStatus,
        ActiveStructure(e, today)?.BasicSalary,
        e.Phone,
        e.Email,
        e.UserId,
        e.BranchId);

    public static EmployeeTeamMemberDto ToTeamMember(Employee e) => new(
        e.EmployeeCode, e.NameLocal, e.NameEn, e.DepartmentCode, e.PositionCode, e.BranchId, e.EmploymentStatus, e.IsActive);

    public static EmployeeDetailsDto ToDetails(Employee emp)
    {
        var activeStructure = ActiveStructure(emp, DateTime.UtcNow.Date);

        return new EmployeeDetailsDto(
            emp.EmployeeCode,
            emp.NameLocal,
            emp.NameEn,
            emp.NationalId,
            emp.Nationality,
            emp.PassportNumber,
            emp.DateOfBirth,
            emp.Gender,
            emp.MaritalStatus,
            emp.Email,
            emp.Phone,
            emp.HireDate,
            emp.ProbationEndDate,
            emp.TerminationDate,
            emp.TerminationReason,
            emp.EmploymentType,
            emp.EmploymentStatus,
            emp.DepartmentCode,
            emp.PositionCode,
            emp.BranchId,
            emp.ManagerEmployeeCode,
            emp.SscNumber,
            emp.TaxExemptionCount,
            emp.IsHighRiskRole,
            emp.BankName,
            emp.BankAccountNumber,
            emp.BankIban,
            emp.IsActive,
            emp.Dependents.Select(ToDependent).ToList(),
            activeStructure == null ? null : ToStructureDetails(activeStructure),
            emp.UserId
        ) { CitizenshipCountryCode = emp.CitizenshipCountryCode };
    }

    public static DependentDto ToDependent(EmployeeDependent d) => new(
        d.Id,
        d.EmployeeCode,
        d.NameLocal,
        d.NameEn,
        d.Relationship,
        d.DateOfBirth,
        d.Gender,
        d.NationalId,
        d.IsTaxExemptionClaimed,
        d.IsMedicalCovered,
        d.IsActive);

    public static SalaryStructureDetailsDto ToStructureDetails(SalaryStructure s)
    {
        var lines = s.Lines.Select(l => new SalaryStructureLineDetailDto(
            l.Id,
            l.ComponentCode,
            l.Component?.NameLocal ?? l.ComponentCode,
            l.Component?.NameEn ?? l.ComponentCode,
            l.Component?.ComponentType ?? "ALLOWANCE",
            l.Amount,
            l.Percent,
            l.IsActive)).ToList();

        return new SalaryStructureDetailsDto(
            s.Id,
            s.EmployeeCode,
            s.EffectiveFrom,
            s.EffectiveTo,
            s.BasicSalary,
            s.CurrencyCode,
            s.PaymentMethod,
            s.IsActive,
            lines);
    }

    public static SalaryComponentDto ToSalaryComponent(SalaryComponent c) => new(
        c.ComponentCode,
        c.NameLocal,
        c.NameEn,
        c.ComponentType,
        c.CalculationType,
        c.DefaultAmount,
        c.DefaultPercent,
        c.IsTaxable,
        c.IsSscApplicable,
        c.GlAccountCode,
        c.IsActive);
}
