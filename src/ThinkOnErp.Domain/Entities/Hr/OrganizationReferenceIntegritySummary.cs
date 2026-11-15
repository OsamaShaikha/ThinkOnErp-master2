namespace ThinkOnErp.Domain.Entities.Hr;

/// <summary>Read-only counts of legacy or cross-scope organization references within one tenant.</summary>
public sealed record OrganizationReferenceIntegritySummary(
    long EmployeeDepartmentMissingOrInactive,
    long EmployeePositionMissingOrInactive,
    long EmployeeManagerMissing,
    long EmployeeBranchOutsideCompany,
    long DepartmentParentMissingOrInactive,
    long DepartmentManagerMissing,
    long DepartmentBranchOutsideCompany,
    long PositionDepartmentMissingOrInactive,
    long PositionReportsToMissingOrInactive,
    DateTime CheckedAtUtc)
{
    public long TotalIssues => EmployeeDepartmentMissingOrInactive + EmployeePositionMissingOrInactive + EmployeeManagerMissing +
        EmployeeBranchOutsideCompany + DepartmentParentMissingOrInactive + DepartmentManagerMissing +
        DepartmentBranchOutsideCompany + PositionDepartmentMissingOrInactive + PositionReportsToMissingOrInactive;
}
