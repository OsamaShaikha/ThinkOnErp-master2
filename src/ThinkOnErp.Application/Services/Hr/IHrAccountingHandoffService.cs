using ThinkOnErp.Domain.Entities.Hr;
using ThinkOnErp.Application.DTOs.Hr;

namespace ThinkOnErp.Application.Services.Hr;

public interface IHrAccountingHandoffService
{
    Task<HrAccountingHandoff> CreateApprovedPayrollHandoffAsync(long payrollRunId, string user, CancellationToken cancellationToken = default);
    Task<PayrollHandoffStatusDto?> GetPayrollHandoffStatusAsync(long payrollRunId, CancellationToken cancellationToken = default);
    Task<PayrollHandoffStatusDto?> WithdrawPayrollHandoffAsync(long payrollRunId, string reason, string user, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<AccountingHandoffInboxDto>> GetAccountingInboxAsync(CancellationToken cancellationToken = default);
    Task<AccountingHandoffPaymentSummaryDto?> GetAccountingPaymentSummaryAsync(long handoffId, CancellationToken cancellationToken = default);
    Task<PayrollHandoffPaymentStatusDto?> GetPayrollHandoffPaymentStatusAsync(long payrollRunId, CancellationToken cancellationToken = default);
    Task<HrAccountingHandoff> ReviewAsync(long handoffId, bool accept, string? reason, string user, CancellationToken cancellationToken = default);
    Task<long> CreateJournalVoucherAsync(long handoffId, string user, CancellationToken cancellationToken = default);
    Task<long> ReverseJournalVoucherAsync(long handoffId, DateTime reversalDate, string reason, string user, CancellationToken cancellationToken = default);
    Task ConfirmJournalPostedAsync(long handoffId, long voucherId, string eventKey, string user, CancellationToken cancellationToken = default);
    Task<AccountingHandoffPayment> PreparePaymentAsync(long handoffId, decimal amount, string currency, string method, string idempotencyKey, string user, CancellationToken cancellationToken = default);
    Task<AccountingHandoffPayment> FailPaymentAsync(long paymentId, string reason, string user, CancellationToken cancellationToken = default);
    Task<AccountingHandoffPayment> ReversePaymentAsync(long paymentId, string reason, string externalReference, string evidenceReference, string user, CancellationToken cancellationToken = default);
    Task<AccountingHandoffPayment> ConfirmPaymentAsync(long paymentId, string externalReference, string evidenceReference, string user, CancellationToken cancellationToken = default);
    Task<HrAccountingHandoff> CreateApprovedFundingHandoffAsync(string sourceType, long sourceId, string user, CancellationToken cancellationToken = default);
    Task ConfirmLoanFundingAsync(long handoffId, decimal fundedAmount, string currency, long fundingVoucherId, string eventKey, string externalReference, string user, CancellationToken cancellationToken = default);
    Task<long> ReverseLoanFundingAsync(long handoffId, string reason, string eventKey, string externalReference, string evidenceReference, string user, CancellationToken cancellationToken = default);
}
