using System;
using System.Collections.Generic;
using ThinkOnErp.Domain.Entities.Accounting;

namespace ThinkOnErp.Domain.Entities.Hr;

public sealed class Department
{
    public string DepartmentCode { get; set; } = string.Empty;
    public string NameAr { get; set; } = string.Empty;
    public string NameEn { get; set; } = string.Empty;
    public string? ParentDepartmentCode { get; set; }
    public long? BranchId { get; set; }
    public string? CostCenterCode { get; set; }
    public bool IsActive { get; set; } = true;
    public string CreationUser { get; set; } = string.Empty;
    public DateTime CreationDate { get; set; } = DateTime.UtcNow;
    public string? UpdateUser { get; set; }
    public DateTime? UpdateDate { get; set; }

    public Department? ParentDepartment { get; set; }
    public List<Department> SubDepartments { get; set; } = new();
    public SysBranch? Branch { get; set; }
    public GlCostCenter? CostCenter { get; set; }
    public List<Position> Positions { get; set; } = new();
    public List<Employee> Employees { get; set; } = new();
}
