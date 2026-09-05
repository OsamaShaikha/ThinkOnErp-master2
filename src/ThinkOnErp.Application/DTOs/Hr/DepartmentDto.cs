using System;
using System.Collections.Generic;

namespace ThinkOnErp.Application.DTOs.Hr;

public sealed class DepartmentDto
{
    public string DepartmentCode { get; set; } = string.Empty;
    public string NameAr { get; set; } = string.Empty;
    public string NameEn { get; set; } = string.Empty;
    public string? ParentDepartmentCode { get; set; }
    public string? ParentDepartmentName { get; set; }
    public long? BranchId { get; set; }
    public string? BranchName { get; set; }
    public string? CostCenterCode { get; set; }
    public string? CostCenterName { get; set; }
    public bool IsActive { get; set; }
    public int EmployeeCount { get; set; }
    public int PositionCount { get; set; }
    public DateTime CreationDate { get; set; }
}

public sealed class CreateDepartmentDto
{
    public string DepartmentCode { get; set; } = string.Empty;
    public string NameAr { get; set; } = string.Empty;
    public string NameEn { get; set; } = string.Empty;
    public string? ParentDepartmentCode { get; set; }
    public long? BranchId { get; set; }
    public string? CostCenterCode { get; set; }
}

public sealed class UpdateDepartmentDto
{
    public string NameAr { get; set; } = string.Empty;
    public string NameEn { get; set; } = string.Empty;
    public string? ParentDepartmentCode { get; set; }
    public long? BranchId { get; set; }
    public string? CostCenterCode { get; set; }
    public bool IsActive { get; set; } = true;
}

public sealed class DepartmentTreeNodeDto
{
    public string DepartmentCode { get; set; } = string.Empty;
    public string NameAr { get; set; } = string.Empty;
    public string NameEn { get; set; } = string.Empty;
    public string? ParentDepartmentCode { get; set; }
    public long? BranchId { get; set; }
    public string? CostCenterCode { get; set; }
    public int EmployeeCount { get; set; }
    public List<DepartmentTreeNodeDto> Children { get; set; } = new();
}
