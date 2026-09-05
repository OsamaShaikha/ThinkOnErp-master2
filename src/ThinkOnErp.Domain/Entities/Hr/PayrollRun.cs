using System;
using System.Collections.Generic;

namespace ThinkOnErp.Domain.Entities.Hr;

public sealed class PayrollRun
{
    public long Id { get; set; }
    public long? BranchId { get; set; }
    public string PayPeriod { get; set; } = string.Empty; // Format: YYYY-MM (e.g., 2026-08)
    public DateTime RunDate { get; set; } = DateTime.UtcNow;
    public string Status { get; set; } = "DRAFT"; // DRAFT, CALCULATED, APPROVED, POSTED, PAID
    public decimal TotalGrossSalary { get; set; } = 0m;
    public decimal TotalNetSalary { get; set; } = 0m;
    public decimal TotalEmployeeSsc { get; set; } = 0m;
    public decimal TotalEmployerSsc { get; set; } = 0m;
    public decimal TotalIncomeTax { get; set; } = 0m;
    public decimal TotalNationalContribution { get; set; } = 0m;
    public decimal TotalOtherDeductions { get; set; } = 0m;
    public long? JournalVoucherId { get; set; } // GL voucher ID when posted
    public string? CalculatedBy { get; set; }
    public DateTime? CalculationDate { get; set; }
    public string? ApprovedBy { get; set; }
    public DateTime? ApprovalDate { get; set; }
    public string? PostedBy { get; set; }
    public DateTime? PostDate { get; set; }
    public string? PaidBy { get; set; }
    public DateTime? PaymentDate { get; set; }
    public string CreationUser { get; set; } = string.Empty;
    public DateTime CreationDate { get; set; } = DateTime.UtcNow;
    public string? UpdateUser { get; set; }
    public DateTime? UpdateDate { get; set; }

    public List<PayrollRunLine> Lines { get; set; } = new();
}
