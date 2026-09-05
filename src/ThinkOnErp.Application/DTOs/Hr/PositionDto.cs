using System;
using System.Collections.Generic;

namespace ThinkOnErp.Application.DTOs.Hr;

public sealed class PositionDto
{
    public string PositionCode { get; set; } = string.Empty;
    public string TitleAr { get; set; } = string.Empty;
    public string TitleEn { get; set; } = string.Empty;
    public string DepartmentCode { get; set; } = string.Empty;
    public string? DepartmentName { get; set; }
    public string? JobGradeCode { get; set; }
    public string? JobGradeName { get; set; }
    public string? ReportsToPositionCode { get; set; }
    public string? ReportsToPositionTitle { get; set; }
    public int Headcount { get; set; }
    public int ActiveEmployeeCount { get; set; }
    public bool IsActive { get; set; }
    public DateTime CreationDate { get; set; }
}

public sealed class CreatePositionDto
{
    public string PositionCode { get; set; } = string.Empty;
    public string TitleAr { get; set; } = string.Empty;
    public string TitleEn { get; set; } = string.Empty;
    public string DepartmentCode { get; set; } = string.Empty;
    public string? JobGradeCode { get; set; }
    public string? ReportsToPositionCode { get; set; }
    public int Headcount { get; set; } = 1;
}

public sealed class UpdatePositionDto
{
    public string TitleAr { get; set; } = string.Empty;
    public string TitleEn { get; set; } = string.Empty;
    public string DepartmentCode { get; set; } = string.Empty;
    public string? JobGradeCode { get; set; }
    public string? ReportsToPositionCode { get; set; }
    public int Headcount { get; set; } = 1;
    public bool IsActive { get; set; } = true;
}

public sealed class OrgChartNodeDto
{
    public string PositionCode { get; set; } = string.Empty;
    public string TitleAr { get; set; } = string.Empty;
    public string TitleEn { get; set; } = string.Empty;
    public string DepartmentCode { get; set; } = string.Empty;
    public string DepartmentName { get; set; } = string.Empty;
    public string? JobGradeCode { get; set; }
    public List<EmployeeSummaryDto> Incumbents { get; set; } = new();
    public List<OrgChartNodeDto> DirectReports { get; set; } = new();
}
