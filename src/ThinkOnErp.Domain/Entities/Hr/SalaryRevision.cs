using System;

namespace ThinkOnErp.Domain.Entities.Hr;

public sealed class SalaryRevision
{
    public long Id { get; set; }
    public string EmployeeCode { get; set; } = string.Empty;
    public DateTime EffectiveDate { get; set; }
    public decimal OldBasicSalary { get; set; }
    public decimal NewBasicSalary { get; set; }
    public decimal OldGrossSalary { get; set; }
    public decimal NewGrossSalary { get; set; }
    public string Reason { get; set; } = string.Empty;
    public string ApprovedBy { get; set; } = string.Empty;
    public DateTime CreationDate { get; set; } = DateTime.UtcNow;

    public Employee? Employee { get; set; }
}
