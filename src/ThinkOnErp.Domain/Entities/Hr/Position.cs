using System;
using System.Collections.Generic;

namespace ThinkOnErp.Domain.Entities.Hr;

public sealed class Position
{
    public string PositionCode { get; set; } = string.Empty;
    public string TitleAr { get; set; } = string.Empty;
    public string TitleEn { get; set; } = string.Empty;
    public string DepartmentCode { get; set; } = string.Empty;
    public string? JobGradeCode { get; set; }
    public string? ReportsToPositionCode { get; set; }
    public int Headcount { get; set; } = 1;
    public bool IsActive { get; set; } = true;
    public string CreationUser { get; set; } = string.Empty;
    public DateTime CreationDate { get; set; } = DateTime.UtcNow;
    public string? UpdateUser { get; set; }
    public DateTime? UpdateDate { get; set; }

    public Department? Department { get; set; }
    public JobGrade? JobGrade { get; set; }
    public Position? ReportsToPosition { get; set; }
    public List<Position> DirectReportPositions { get; set; } = new();
    public List<Employee> Employees { get; set; } = new();
}
