namespace ThinkOnErp.Domain.Entities.Hr;
// Existing tenant HR catalog tables; no new ERP catalog or schema is introduced.
public abstract class HrOrganizationAudit
{
    public DateTime CreationDate { get; set; }
    public string CreationUser { get; set; } = string.Empty;
    public DateTime? UpdateDate { get; set; }
    public string? UpdateUser { get; set; }
}
public sealed class HrDepartment : HrOrganizationAudit
{
    [System.ComponentModel.DataAnnotations.Schema.NotMapped]
    public string Version => OrganizationCatalogRules.Version(this);
    public string DepartmentCode { get; set; } = string.Empty;
    public long? BranchId { get; set; }
    public string NameLocal { get; set; } = string.Empty;
    public string NameEn { get; set; } = string.Empty;
    public string? ParentDepartmentCode { get; set; }
    public string? ManagerEmployeeCode { get; set; }
    public string? CostCenterCode { get; set; }
    public bool IsActive { get; set; }
}
public sealed class HrPosition : HrOrganizationAudit
{
    [System.ComponentModel.DataAnnotations.Schema.NotMapped]
    public string Version => OrganizationCatalogRules.Version(this);
    public int Headcount { get; set; }
    [System.Text.Json.Serialization.JsonIgnore]
    public string? JobGradeCode { get; set; }
    public string PositionCode { get; set; } = string.Empty;
    public string DepartmentCode { get; set; } = string.Empty;
    public string TitleLocal { get; set; } = string.Empty;
    public string TitleEn { get; set; } = string.Empty;
    public string? ReportsToPositionCode { get; set; }
    public bool IsActive { get; set; }
}
public static class EmployeeHierarchyRules
{
    public static void Validate(string employeeCode, string? managerCode, IReadOnlyDictionary<string, string?> managers)
    {
        HashSet<string> seen = new(StringComparer.OrdinalIgnoreCase) { employeeCode };
        while (!string.IsNullOrWhiteSpace(managerCode))
        {
            if (!seen.Add(managerCode))
            {
                throw new ArgumentException("Reporting relationship contains a cycle.");
            }

            if (!managers.TryGetValue(managerCode, out string? next))
            {
                throw new ArgumentException("Reporting chain contains an unavailable or foreign-company employee.");
            }

            managerCode = next;
        }
    }
}
