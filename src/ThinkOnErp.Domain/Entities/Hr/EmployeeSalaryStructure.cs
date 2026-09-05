using System;
using System.Collections.Generic;

namespace ThinkOnErp.Domain.Entities.Hr;

public sealed class EmployeeSalaryStructure
{
    public long Id { get; set; }
    public string EmployeeCode { get; set; } = string.Empty;
    public DateTime EffectiveFrom { get; set; }
    public DateTime? EffectiveTo { get; set; }
    public decimal BasicSalary { get; set; }
    public string CurrencyCode { get; set; } = "JOD";
    public string PaymentMethod { get; set; } = "BANK_TRANSFER"; // BANK_TRANSFER, CASH, CHEQUE
    public bool IsActive { get; set; } = true;
    public string CreationUser { get; set; } = string.Empty;
    public DateTime CreationDate { get; set; } = DateTime.UtcNow;
    public string? UpdateUser { get; set; }
    public DateTime? UpdateDate { get; set; }

    public Employee? Employee { get; set; }
    public List<EmployeeSalaryStructureLine> Lines { get; set; } = new();
}
