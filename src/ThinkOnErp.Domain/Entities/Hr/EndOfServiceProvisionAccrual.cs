using System;

namespace ThinkOnErp.Domain.Entities.Hr;

public sealed class EndOfServiceProvisionAccrual
{
    public long Id { get; set; }
    public string EmployeeCode { get; set; } = string.Empty;
    public string PayPeriod { get; set; } = string.Empty; // YYYY-MM
    public decimal BasicSalary { get; set; }
    public decimal ServiceYears { get; set; }
    public decimal MonthlyAccrualAmount { get; set; }
    public decimal TotalAccumulatedProvision { get; set; }
    public long? JournalVoucherId { get; set; }
    public string CreationUser { get; set; } = string.Empty;
    public DateTime CreationDate { get; set; } = DateTime.UtcNow;

    public Employee? Employee { get; set; }
}
