using System;
using System.Collections.Generic;

namespace ThinkOnErp.Domain.Entities.Hr;

public sealed class EmployeeLoan
{
    public long Id { get; set; }
    public string EmployeeCode { get; set; } = string.Empty;
    public string LoanType { get; set; } = "PERSONAL"; // PERSONAL, HOUSING, VEHICLE, EMERGENCY
    public decimal PrincipalAmount { get; set; }
    public decimal TotalInstallments { get; set; }
    public decimal MonthlyInstallmentAmount { get; set; }
    public decimal TotalPaidAmount { get; set; }
    public decimal RemainingBalance { get; set; }
    public DateTime StartDate { get; set; }
    public DateTime? EndDate { get; set; }
    public string Status { get; set; } = "PENDING"; // PENDING, APPROVED, ACTIVE, COMPLETED, SUSPENDED, REJECTED
    public string? ApprovedBy { get; set; }
    public DateTime? ApprovalDate { get; set; }
    public string? Notes { get; set; }
    public string CreationUser { get; set; } = string.Empty;
    public DateTime CreationDate { get; set; } = DateTime.UtcNow;
    public string? UpdateUser { get; set; }
    public DateTime? UpdateDate { get; set; }

    public Employee? Employee { get; set; }
    public ICollection<LoanRepaymentSchedule> Schedules { get; set; } = new List<LoanRepaymentSchedule>();
}
