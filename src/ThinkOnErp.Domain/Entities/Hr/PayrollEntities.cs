using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations.Schema;

namespace ThinkOnErp.Domain.Entities.Hr;

/// <summary>Immutable HR-owned payload handed to Accounting. Accounting/payment state is stored separately.</summary>
public sealed class HrAccountingHandoff
{
    public long Id { get; set; }
    public long CompanyId { get; set; }
    public long? BranchId { get; set; }
    public string SourceType { get; set; } = string.Empty; // PAYROLL, LOAN, ADVANCE
    public string SourceId { get; set; } = string.Empty;
    public int SourceVersion { get; set; }
    public string Operation { get; set; } = "SUBMIT";
    public string? PayPeriod { get; set; }
    public DateTime? SourceApprovedAtUtc { get; set; }
    public string HandoffReference { get; set; } = string.Empty;
    public string PayloadHash { get; set; } = string.Empty;
    public string DeliveryStatus { get; set; } = "NOT_SENT";
    public DateTime CreatedAtUtc { get; set; }
    public string CreatedBy { get; set; } = string.Empty;
    public ICollection<HrAccountingHandoffLine> Lines { get; set; } = new List<HrAccountingHandoffLine>();
    public ICollection<HrAccountingHandoffTransition> Transitions { get; set; } = new List<HrAccountingHandoffTransition>();
    public ICollection<HrAccountingOutboxMessage> OutboxMessages { get; set; } = new List<HrAccountingOutboxMessage>();
    public ICollection<AccountingHandoffInboxMessage> InboxMessages { get; set; } = new List<AccountingHandoffInboxMessage>();
}

public sealed class HrAccountingHandoffLine
{
    public long Id { get; set; }
    public long HandoffId { get; set; }
    public string LineType { get; set; } = string.Empty;
    public string CurrencyCode { get; set; } = string.Empty;
    public decimal Amount { get; set; }
    public long? BranchId { get; set; }
    public string? CostCenterReference { get; set; }
    public string? SourceReference { get; set; }
    public HrAccountingHandoff Handoff { get; set; } = null!;
}

public sealed class HrAccountingHandoffTransition
{
    public long Id { get; set; }
    public long HandoffId { get; set; }
    public string FromStatus { get; set; } = string.Empty;
    public string ToStatus { get; set; } = string.Empty;
    public string Actor { get; set; } = string.Empty;
    public string? Reason { get; set; }
    public DateTime OccurredAtUtc { get; set; }
    public HrAccountingHandoff Handoff { get; set; } = null!;
}

public sealed class HrAccountingOutboxMessage
{
    public long Id { get; set; }
    public long HandoffId { get; set; }
    public string EventKey { get; set; } = string.Empty;
    public string EventType { get; set; } = string.Empty;
    public string PayloadHash { get; set; } = string.Empty;
    public string Status { get; set; } = "PENDING";
    public int AttemptCount { get; set; }
    public DateTime CreatedAtUtc { get; set; }
    public DateTime? LastAttemptAtUtc { get; set; }
    public string? LastError { get; set; }
    public HrAccountingHandoff Handoff { get; set; } = null!;
}

public sealed class AccountingHandoffInboxMessage
{
    public long Id { get; set; }
    public long HandoffId { get; set; }
    public string EventKey { get; set; } = string.Empty;
    public string PayloadHash { get; set; } = string.Empty;
    public DateTime ReceivedAtUtc { get; set; }
    public string Status { get; set; } = "RECEIVED";
    public HrAccountingHandoff Handoff { get; set; } = null!;
}

public sealed class AccountingHandoffPostingReceipt
{
    public long Id { get; set; }
    public long HandoffId { get; set; }
    public long VoucherId { get; set; }
    public string EventKey { get; set; } = string.Empty;
    public string PayloadHash { get; set; } = string.Empty;
    public DateTime ReceivedAtUtc { get; set; }
    public string ReceivedBy { get; set; } = string.Empty;
    public string? SettlementSnapshotJson { get; set; }
}

public sealed class AccountingHandoffJournalLink
{
    public long Id { get; set; }
    public long HandoffId { get; set; }
    public long VoucherId { get; set; }
    public DateTime CreatedAtUtc { get; set; }
    public string CreatedBy { get; set; } = string.Empty;
}

public sealed class AccountingHandoffPayment
{
    public long Id { get; set; }
    public long HandoffId { get; set; }
    public int PaymentSequence { get; set; }
    public string IdempotencyKey { get; set; } = string.Empty;
    public string CurrencyCode { get; set; } = string.Empty;
    public decimal Amount { get; set; }
    public string Method { get; set; } = string.Empty; // BANK, CASH, OTHER; policy-owned
    public string? PaymentMethodCode { get; set; }
    public string Status { get; set; } = "PREPARED"; // PREPARED, CONFIRMED, FAILED, REVERSED
    public string? ExternalReference { get; set; }
    public string? EvidenceReference { get; set; }
    public string? FailureReason { get; set; }
    public DateTime? FailedAtUtc { get; set; }
    public string? FailedBy { get; set; }
    public string? ReversalReason { get; set; }
    public string? ReversalExternalReference { get; set; }
    public string? ReversalEvidenceReference { get; set; }
    public DateTime? ReversedAtUtc { get; set; }
    public string? ReversedBy { get; set; }
    public long? ReversalVoucherId { get; set; }
    public DateTime CreatedAtUtc { get; set; }
    public string CreatedBy { get; set; } = string.Empty;
    public DateTime? ConfirmedAtUtc { get; set; }
    public string? ConfirmedBy { get; set; }
    [NotMapped]
    public long? PaymentVoucherId { get; set; }
}

public sealed class AccountingLoanFundingReceipt
{
    public long Id { get; set; }
    public long HandoffId { get; set; }
    public string SourceType { get; set; } = string.Empty;
    public string SourceId { get; set; } = string.Empty;
    public int SourceVersion { get; set; } = 1;
    public decimal FundedAmount { get; set; }
    public string CurrencyCode { get; set; } = string.Empty;
    public long? FundingVoucherId { get; set; }
    public long? ReversalVoucherId { get; set; }
    public string EventKey { get; set; } = string.Empty;
    public string ExternalReference { get; set; } = string.Empty;
    public string? ReversalEventKey { get; set; }
    public string? ReversalReason { get; set; }
    public string? ReversalExternalReference { get; set; }
    public string? ReversalEvidenceReference { get; set; }
    public DateTime ReceivedAtUtc { get; set; }
    public string ReceivedBy { get; set; } = string.Empty;
    public DateTime? ReversedAtUtc { get; set; }
    public string? ReversedBy { get; set; }
}

public sealed class PayrollPeriod
{
    public long Id { get; set; }
    public long CompanyId { get; set; }
    public string PeriodCode { get; set; } = string.Empty; // e.g. "2026-09"
    public string NameAr { get; set; } = string.Empty;
    public string NameEn { get; set; } = string.Empty;
    public int FiscalYear { get; set; }
    public int Month { get; set; }
    public DateTime StartDate { get; set; }
    public DateTime EndDate { get; set; }
    public DateTime PayDate { get; set; }
    public string PayrollType { get; set; } = "MONTHLY"; // MONTHLY, BIWEEKLY, SPECIAL
    public string Status { get; set; } = "OPEN"; // OPEN, PROCESSING, CLOSED

    public string CreationUser { get; set; } = string.Empty;
    public DateTime CreationDate { get; set; } = DateTime.UtcNow;
    public string? UpdateUser { get; set; }
    public DateTime? UpdateDate { get; set; }
}

public sealed class PayrollRun
{
    public long? CorrectionOfRunId { get; set; }
    public long? CorrectionRootRunId { get; set; }
    public int CorrectionVersion { get; set; } = 1;
    public string? CorrectionReason { get; set; }
    public string? CancelledBy { get; set; }
    public DateTime? CancellationDate { get; set; }
    public string? CancellationReason { get; set; }
    public string? InputFingerprint { get; set; }
    public long Id { get; set; }
    public string PayPeriod { get; set; } = string.Empty;
    public DateTime RunDate { get; set; } = DateTime.UtcNow;
    public long? BranchId { get; set; }
    public string Status { get; set; } = "DRAFT"; // DRAFT, CALCULATED, APPROVED, POSTED_TO_GL, PAID, CANCELLED

    public decimal TotalGrossSalary { get; set; }
    public decimal TotalNetSalary { get; set; }
    public decimal TotalEmployeeSsc { get; set; }
    public decimal TotalEmployerSsc { get; set; }
    public decimal TotalIncomeTax { get; set; }
    public decimal TotalNationalContrib { get; set; }
    public decimal TotalOtherDeductions { get; set; }

    public string? CalculatedBy { get; set; }
    public DateTime? CalculationDate { get; set; }
    public string? ApprovedBy { get; set; }
    public DateTime? ApprovalDate { get; set; }
    public string? PostedBy { get; set; }
    public DateTime? PostDate { get; set; }
    public string? PaidBy { get; set; }
    public DateTime? PaymentDate { get; set; }
    public long? JournalVoucherId { get; set; } // GL link

    public string CreationUser { get; set; } = string.Empty;
    public DateTime CreationDate { get; set; } = DateTime.UtcNow;
    public string? UpdateUser { get; set; }
    public DateTime? UpdateDate { get; set; }

    public List<PayrollRunLine> Lines { get; set; } = new();
}

public sealed class PayrollRunLine
{
    public long Id { get; set; }
    public long PayrollRunId { get; set; }
    public string EmployeeCode { get; set; } = string.Empty;
    public long? BranchId { get; set; }
    public string? DepartmentCode { get; set; }
    public string? CostCenterCode { get; set; }

    public decimal BasicSalary { get; set; }
    public decimal TotalEarnings { get; set; }
    public decimal GrossSalary { get; set; }
    public decimal SscEligibleSalary { get; set; }
    public decimal SscEmployeeContrib { get; set; }
    public decimal SscEmployerContrib { get; set; }
    public decimal TaxableGross { get; set; }
    public decimal AnnualExemptions { get; set; }
    public decimal AnnualTaxableNet { get; set; }
    public decimal IncomeTaxWithheld { get; set; }
    public decimal NationalContribWithheld { get; set; }
    public decimal OtherDeductions { get; set; }
    public decimal TotalDeductions { get; set; }
    public decimal NetPay { get; set; }

    public string PaymentMethod { get; set; } = "BANK_TRANSFER";
    public string? CurrencyCode { get; set; }
    public string? BeneficiaryName { get; set; }
    public string? BeneficiaryNationalId { get; set; }
    public string? BankCode { get; set; }
    public string? Iban { get; set; }
    public string Status { get; set; } = "CALCULATED";

    public string CreationUser { get; set; } = string.Empty;
    public DateTime CreationDate { get; set; } = DateTime.UtcNow;

    public PayrollRun? PayrollRun { get; set; }
    public Employee? Employee { get; set; }
    public List<PayrollRunLineComponent> Components { get; set; } = new();
    public PayrollCalculationSnapshot? Snapshot { get; set; }
}

public sealed class PayrollRunLineComponent
{
    public long Id { get; set; }
    public long PayrollLineId { get; set; }
    public string ComponentCode { get; set; } = string.Empty;
    public string ComponentNameLocal { get; set; } = string.Empty;
    public string ComponentNameEn { get; set; } = string.Empty;
    public string ComponentType { get; set; } = "EARNING"; // EARNING, DEDUCTION, EMPLOYER_CONTRIB
    public decimal Amount { get; set; }

    public PayrollRunLine? PayrollRunLine { get; set; }
}

public sealed class PayrollCalculationSnapshot
{
    public long Id { get; set; }
    public long PayrollRunLineId { get; set; }
    public string EmployeeCode { get; set; } = string.Empty;
    public string PayPeriod { get; set; } = string.Empty;
    public DateTime CalculationTimestamp { get; set; } = DateTime.UtcNow;

    public decimal TotalBaseDays { get; set; }
    public decimal EligibleDays { get; set; }
    public decimal ProrationFactor { get; set; } = 1.0m;
    public string ProrationPolicyCode { get; set; } = string.Empty;
    public string ProrationMethodUsed { get; set; } = string.Empty;

    public decimal OvertimeHoursApplied { get; set; }
    public decimal OvertimeEarningsApplied { get; set; }

    public string SscPolicyCode { get; set; } = string.Empty;
    public decimal SscEmpRateApplied { get; set; }
    public decimal SscEmprRateApplied { get; set; }
    public decimal SscCapApplied { get; set; }

    public string TaxPolicyCode { get; set; } = string.Empty;
    public decimal TaxExemptionsApplied { get; set; }

    public decimal LoanDeductionsApplied { get; set; }
    public decimal LoanDeductionsCarriedFwd { get; set; }

    public string ExplanationJson { get; set; } = string.Empty; // Full JSON breakdown for explanation service

    public PayrollRunLine? PayrollRunLine { get; set; }
}
