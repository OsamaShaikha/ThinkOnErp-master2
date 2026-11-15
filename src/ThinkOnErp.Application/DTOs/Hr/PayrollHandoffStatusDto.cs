namespace ThinkOnErp.Application.DTOs.Hr;

public sealed record PayrollHandoffStatusDto(
    long HandoffId,
    string HandoffReference,
    int SourceVersion,
    string DeliveryStatus,
    string? PayPeriod,
    DateTime? SourceApprovedAtUtc,
    DateTime CreatedAtUtc,
    DateTime? LastTransitionAtUtc,
    string? LastAccountingReason);

public sealed record AccountingHandoffLineDto(
    string LineType,
    string CurrencyCode,
    decimal Amount,
    long? BranchId,
    string? CostCenterReference,
    string? SourceReference);

public sealed record AccountingHandoffInboxDto(
    long HandoffId,
    long CompanyId,
    long? BranchId,
    string SourceType,
    string SourceId,
    int SourceVersion,
    string Operation,
    string HandoffReference,
    string PayloadHash,
    string DeliveryStatus,
    string? PayPeriod,
    DateTime? SourceApprovedAtUtc,
    DateTime CreatedAtUtc,
    string CreatedBy,
    string? LastTransitionReason,
    DateTime? LastTransitionAtUtc,
    IReadOnlyList<AccountingHandoffLineDto> Lines);

public sealed record AccountingHandoffPaymentDto(
    long PaymentId,
    int Sequence,
    string CurrencyCode,
    decimal Amount,
    string Method,
    string? PaymentMethodCode,
    string Status,
    long? PaymentVoucherId,
    string? ExternalReference,
    string? EvidenceReference,
    DateTime CreatedAtUtc,
    string CreatedBy,
    DateTime? ConfirmedAtUtc,
    string? ConfirmedBy,
    string? FailureReason,
    DateTime? FailedAtUtc,
    string? FailedBy,
    string? ReversalReason,
    string? ReversalExternalReference,
    string? ReversalEvidenceReference,
    DateTime? ReversedAtUtc,
    string? ReversedBy,
    long? ReversalVoucherId);

public sealed record AccountingHandoffPaymentSummaryDto(
    long HandoffId,
    string HandoffReference,
    string HandoffStatus,
    string CurrencyCode,
    decimal PayableAmount,
    decimal PreparedAmount,
    decimal PaidAmount,
    decimal ReversedAmount,
    decimal RemainingAmount,
    IReadOnlyList<AccountingHandoffPaymentDto> Payments);

public sealed record PayrollHandoffPaymentStatusDto(
    long HandoffId,
    string HandoffReference,
    string CurrencyCode,
    decimal PayableAmount,
    decimal PreparedAmount,
    decimal PaidAmount,
    decimal ReversedAmount,
    decimal RemainingAmount,
    string PaymentStatus);
