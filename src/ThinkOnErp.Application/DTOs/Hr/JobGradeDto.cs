using System;

namespace ThinkOnErp.Application.DTOs.Hr;

public sealed class JobGradeDto
{
    public string GradeCode { get; set; } = string.Empty;
    public string NameAr { get; set; } = string.Empty;
    public string NameEn { get; set; } = string.Empty;
    public int Level { get; set; }
    public decimal MinSalary { get; set; }
    public decimal MidSalary { get; set; }
    public decimal MaxSalary { get; set; }
    public bool IsActive { get; set; }
    public int PositionCount { get; set; }
    public DateTime CreationDate { get; set; }
}

public sealed class CreateJobGradeDto
{
    public string GradeCode { get; set; } = string.Empty;
    public string NameAr { get; set; } = string.Empty;
    public string NameEn { get; set; } = string.Empty;
    public int Level { get; set; } = 1;
    public decimal MinSalary { get; set; }
    public decimal MidSalary { get; set; }
    public decimal MaxSalary { get; set; }
}

public sealed class UpdateJobGradeDto
{
    public string NameAr { get; set; } = string.Empty;
    public string NameEn { get; set; } = string.Empty;
    public int Level { get; set; } = 1;
    public decimal MinSalary { get; set; }
    public decimal MidSalary { get; set; }
    public decimal MaxSalary { get; set; }
    public bool IsActive { get; set; } = true;
}
