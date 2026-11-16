using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Text.Json.Serialization;
using ThinkOnErp.API.Authorization;
using ThinkOnErp.API.Swagger;
using ThinkOnErp.Application.Common;
using ThinkOnErp.Application.Services.Hr;
using ThinkOnErp.Domain.Entities.Hr;

namespace ThinkOnErp.API.Controllers.Hr;

[ApiController]
[Route("api/accounting/hr-handoffs")]
[ApiExplorerSettings(GroupName = ApiCategories.Hr)]
[TenantScoped]
[Authorize]
public sealed class AccountingHandoffController(IHrAccountingHandoffService handoffs, IHrPayrollPostingService payrollPosting) : ControllerBase
{
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

    [HttpGet("payroll-posting-config/{branchId:long}")]
    [HrPermission("accounting-handoff", "view")]
    public async Task<IActionResult> GetPayrollPostingConfiguration(long branchId, CancellationToken cancellationToken)
    {
        var configuration = await payrollPosting.GetAsync(branchId, cancellationToken);
        return configuration == null
            ? NotFound(ApiResponse<object>.CreateFailure("Payroll posting configuration was not found.", statusCode: StatusCodes.Status404NotFound))
            : Ok(ApiResponse<object>.CreateSuccess(configuration));
    }

    [HttpPut("payroll-posting-config/{branchId:long}")]
    [HrPermission("accounting-handoff", "configure")]
    public async Task<IActionResult> SavePayrollPostingConfiguration(long branchId, [FromBody] PayrollPostingConfigurationRequest request, CancellationToken cancellationToken)
    {
        var configuration = new PayrollPostingConfiguration
        {
            BranchId = branchId,
            SalaryExpense = request.SalaryExpense,
            EmployerSscExpense = request.EmployerSscExpense,
            SalaryPayable = request.SalaryPayable,
            SscPayable = request.SscPayable,
            TaxPayable = request.TaxPayable,
            DeductionsPayable = request.DeductionsPayable,
            LoanReceivableAccount = request.LoanReceivableAccount,
            AdvanceReceivableAccount = request.AdvanceReceivableAccount,
            CurrencyId = request.CurrencyId,
            VoucherType = request.VoucherType
        };
        await payrollPosting.SaveAsync(configuration, cancellationToken);
        return Ok(ApiResponse<object>.CreateSuccess(configuration));
    }

    [HttpGet]
    [HrPermission("accounting-handoff", "view")]
    public async Task<IActionResult> Inbox(CancellationToken cancellationToken) =>
        Ok(ApiResponse<object>.CreateSuccess(await handoffs.GetAccountingInboxAsync(cancellationToken)));

    [HttpGet("{id:long}/payments")]
    [HrPermission("accounting-handoff", "view")]
    public async Task<IActionResult> PaymentStatus(long id, CancellationToken cancellationToken)
    {
        var status = await handoffs.GetAccountingPaymentSummaryAsync(id, cancellationToken);
        if (status == null) return NotFound(ApiResponse<object>.CreateFailure("Payroll handoff not found.", statusCode: StatusCodes.Status404NotFound));
        return Ok(ApiResponse<object>.CreateSuccess(status));
    }

    [HttpPost("{id:long}/accept")]
    [HrPermission("accounting-handoff", "review")]
    public async Task<IActionResult> Accept(long id, CancellationToken cancellationToken)
    {
        var handoff = await handoffs.ReviewAsync(id, true, null, User.Identity?.Name ?? "SYSTEM", cancellationToken);
        return Ok(ApiResponse<object>.CreateSuccess(new
        {
            HandoffId = handoff.Id,
            handoff.HandoffReference,
            handoff.DeliveryStatus
        }));
    }

    [HttpPost("{id:long}/return")]
    [HrPermission("accounting-handoff", "review")]
    public async Task<IActionResult> Return(long id, [FromBody] ReviewRequest request, CancellationToken cancellationToken)
    {
        var handoff = await handoffs.ReviewAsync(id, false, request.Reason, User.Identity?.Name ?? "SYSTEM", cancellationToken);
        return Ok(ApiResponse<object>.CreateSuccess(new
        {
            HandoffId = handoff.Id,
            handoff.HandoffReference,
            handoff.DeliveryStatus
        }));
    }

    [HttpPost("{id:long}/journal-voucher")]
    [HrPermission("accounting-handoff", "post")]
    public async Task<IActionResult> CreateJournalVoucher(long id, CancellationToken cancellationToken) =>
        Ok(ApiResponse<object>.CreateSuccess(new { VoucherId = await handoffs.CreateJournalVoucherAsync(id, User.Identity?.Name ?? "SYSTEM", cancellationToken) }));

    [HttpPost("{id:long}/journal-voucher/reverse")]
    [HrPermission("accounting-handoff", "reverse")]
    public async Task<IActionResult> ReverseJournalVoucher(long id, [FromBody] ReverseJournalVoucherRequest request, CancellationToken cancellationToken) =>
        Ok(ApiResponse<object>.CreateSuccess(new { HandoffId = id, ReversalVoucherId = await handoffs.ReverseJournalVoucherAsync(id, request.ReversalDate.ToDateTime(TimeOnly.MinValue), request.Reason, User.Identity?.Name ?? "SYSTEM", cancellationToken), Status = "REVERSED" }));

    [HttpPost("{id:long}/posting-receipt")]
    [HrPermission("accounting-handoff", "post")]
    public async Task<IActionResult> PostingReceipt(long id, [FromBody] PostingReceiptRequest request, CancellationToken cancellationToken)
    {
        await handoffs.ConfirmJournalPostedAsync(id, request.VoucherId, request.EventKey, User.Identity?.Name ?? "SYSTEM", cancellationToken);
        return Ok(ApiResponse<object>.CreateSuccess(new { HandoffId = id, Status = "POSTED" }));
    }

    [HttpPost("{id:long}/payments")]
    [HrPermission("accounting-handoff", "pay")]
    public async Task<IActionResult> PreparePayment(long id, [FromBody] PreparePaymentRequest request, CancellationToken cancellationToken) =>
        Ok(ApiResponse<object>.CreateSuccess(await handoffs.PreparePaymentAsync(id, request.Amount, request.Currency, request.Method, request.IdempotencyKey, User.Identity?.Name ?? "SYSTEM", cancellationToken)));

    [HttpPost("payments/{paymentId:long}/fail")]
    [HrPermission("accounting-handoff", "pay")]
    public async Task<IActionResult> FailPayment(long paymentId, [FromBody] FailPaymentRequest request, CancellationToken cancellationToken) =>
        Ok(ApiResponse<object>.CreateSuccess(await handoffs.FailPaymentAsync(paymentId, request.Reason, User.Identity?.Name ?? "SYSTEM", cancellationToken)));

    [HttpPost("payments/{paymentId:long}/reverse")]
    [HrPermission("accounting-handoff", "reverse")]
    public async Task<IActionResult> ReversePayment(long paymentId, [FromBody] ReversePaymentRequest request, CancellationToken cancellationToken) =>
        Ok(ApiResponse<object>.CreateSuccess(await handoffs.ReversePaymentAsync(paymentId, request.Reason, request.ExternalReference, request.EvidenceReference, User.Identity?.Name ?? "SYSTEM", cancellationToken)));

    [HttpPost("payments/{paymentId:long}/confirm")]
    [HrPermission("accounting-handoff", "pay")]
    public async Task<IActionResult> ConfirmPayment(long paymentId, [FromBody] ConfirmPaymentRequest request, CancellationToken cancellationToken) =>
        Ok(ApiResponse<object>.CreateSuccess(await handoffs.ConfirmPaymentAsync(paymentId, request.ExternalReference, request.EvidenceReference, User.Identity?.Name ?? "SYSTEM", cancellationToken)));

    [HttpPost("{id:long}/loan-funding-receipt")]
    [HrPermission("accounting-handoff", "fund")]
    public async Task<IActionResult> ConfirmLoanFunding(long id, [FromBody] FundingReceiptRequest request, CancellationToken cancellationToken)
    {
        await handoffs.ConfirmLoanFundingAsync(id, request.FundedAmount, request.Currency, request.FundingVoucherId, request.EventKey, request.ExternalReference, User.Identity?.Name ?? "SYSTEM", cancellationToken);
        return Ok(ApiResponse<object>.CreateSuccess(new { HandoffId = id, request.FundingVoucherId, Status = "POSTED" }));
    }

    [HttpPost("{id:long}/loan-funding-reverse")]
    [HrPermission("accounting-handoff", "reverse")]
    public async Task<IActionResult> ReverseLoanFunding(long id, [FromBody] ReverseFundingRequest request, CancellationToken cancellationToken)
    {
        var reversalVoucherId = await handoffs.ReverseLoanFundingAsync(id, request.Reason, request.EventKey, request.ExternalReference, request.EvidenceReference, User.Identity?.Name ?? "SYSTEM", cancellationToken);
        return Ok(ApiResponse<object>.CreateSuccess(new { HandoffId = id, ReversalVoucherId = reversalVoucherId, Status = "REVERSED" }));
    }
}
