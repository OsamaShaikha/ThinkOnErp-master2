using System;
using System.Text.Json.Serialization;

namespace ThinkOnErp.Application.DTOs.Hr;

public sealed record ReviewHandoffResultDto(long HandoffId, string HandoffReference, string DeliveryStatus);
public sealed record CreateJournalVoucherResultDto(long VoucherId);
public sealed record ReverseJournalVoucherResultDto(long HandoffId, long ReversalVoucherId, string Status);
public sealed record JournalPostedResultDto(long HandoffId, string Status);
public sealed record LoanFundingReceiptResultDto(long HandoffId, long FundingVoucherId, string Status);
public sealed record ReverseLoanFundingResultDto(long HandoffId, long ReversalVoucherId, string Status);

public sealed record ReviewRequest(string? Reason);
public sealed record PostingReceiptRequest(long VoucherId, string EventKey);
public sealed record ReverseJournalVoucherRequest(
    [property: JsonRequired] DateOnly ReversalDate,
    [property: JsonRequired] string Reason);
public sealed record PreparePaymentRequest(decimal Amount, string Currency, string Method, string IdempotencyKey);
public sealed record FailPaymentRequest(string Reason);
public sealed record ReversePaymentRequest(string Reason, string ExternalReference, string EvidenceReference);
public sealed record ConfirmPaymentRequest(string ExternalReference, string EvidenceReference);
public sealed record FundingReceiptRequest(decimal FundedAmount, string Currency, long FundingVoucherId, string EventKey, string ExternalReference);
public sealed record ReverseFundingRequest(string Reason, string EventKey, string ExternalReference, string EvidenceReference);

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record PayrollPostingConfigurationRequest(
    [property: JsonRequired] string SalaryExpense,
    [property: JsonRequired] string EmployerSscExpense,
    [property: JsonRequired] string SalaryPayable,
    [property: JsonRequired] string SscPayable,
    [property: JsonRequired] string TaxPayable,
    [property: JsonRequired] string DeductionsPayable,
    string? LoanReceivableAccount,
    string? AdvanceReceivableAccount,
    [property: JsonRequired] long CurrencyId,
    [property: JsonRequired] int VoucherType);
