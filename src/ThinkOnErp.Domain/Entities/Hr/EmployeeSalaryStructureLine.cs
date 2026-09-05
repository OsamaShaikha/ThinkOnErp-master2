using System;

namespace ThinkOnErp.Domain.Entities.Hr;

public sealed class EmployeeSalaryStructureLine
{
    public long Id { get; set; }
    public long StructureId { get; set; }
    public string ComponentCode { get; set; } = string.Empty;
    public decimal Amount { get; set; }
    public decimal? Percent { get; set; }
    public bool IsActive { get; set; } = true;
    public string CreationUser { get; set; } = string.Empty;
    public DateTime CreationDate { get; set; } = DateTime.UtcNow;

    public EmployeeSalaryStructure? Structure { get; set; }
    public SalaryComponent? Component { get; set; }
}
