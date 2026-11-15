using System;
using System.Text.Json.Serialization;
using System.Threading;
using System.Threading.Tasks;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.DependencyInjection;
using ThinkOnErp.API.Authorization;
using ThinkOnErp.API.Swagger;
using ThinkOnErp.Application.Common;
using ThinkOnErp.Application.DTOs.Hr;
using ThinkOnErp.Application.Features.Hr.AccountingHandoffs.Commands.AcceptHandoff;
using ThinkOnErp.Application.Features.Hr.AccountingHandoffs.Commands.ConfirmLoanFunding;
using ThinkOnErp.Application.Features.Hr.AccountingHandoffs.Commands.ConfirmPayment;
using ThinkOnErp.Application.Features.Hr.AccountingHandoffs.Commands.CreateJournalVoucher;
using ThinkOnErp.Application.Features.Hr.AccountingHandoffs.Commands.FailPayment;
using ThinkOnErp.Application.Features.Hr.AccountingHandoffs.Commands.PreparePayment;
using ThinkOnErp.Application.Features.Hr.AccountingHandoffs.Commands.ReverseJournalVoucher;
using ThinkOnErp.Application.Features.Hr.AccountingHandoffs.Commands.ReverseLoanFunding;
using ThinkOnErp.Application.Features.Hr.AccountingHandoffs.Commands.ReversePayment;
using ThinkOnErp.Application.Features.Hr.AccountingHandoffs.Commands.ReturnHandoff;
using ThinkOnErp.Application.Features.Hr.AccountingHandoffs.Commands.SavePayrollPostingConfig;
using ThinkOnErp.Application.Features.Hr.AccountingHandoffs.Commands.ConfirmJournalPosted;
using ThinkOnErp.Application.Features.Hr.AccountingHandoffs.Queries.GetAccountingInbox;
using ThinkOnErp.Application.Features.Hr.AccountingHandoffs.Queries.GetAccountingPaymentSummary;
using ThinkOnErp.Application.Features.Hr.Payroll.Queries.GetPayrollPostingConfig;
using ThinkOnErp.Application.Services.Hr;
using ThinkOnErp.Domain.Entities.Hr;

namespace ThinkOnErp.API.Controllers.Hr;

[ApiController]
[Route("api/accounting/hr-handoffs")]
[ApiExplorerSettings(GroupName = ApiCategories.Hr)]
[TenantScoped]
[Authorize]
public sealed class AccountingHandoffController : HrControllerBase
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

    private readonly IMediator? _mediator;
    private readonly IHrAccountingHandoffService? _handoffs;
    private readonly IHrPayrollPostingService? _payrollPosting;

    [ActivatorUtilitiesConstructor]
    public AccountingHandoffController(IMediator mediator)
    {
        _mediator = mediator ?? throw new ArgumentNullException(nameof(mediator));
    }

    [Obsolete("Transitional constructor for existing test suites. Production uses the IMediator constructor.")]
    public AccountingHandoffController(IHrAccountingHandoffService handoffs, IHrPayrollPostingService payrollPosting)
    {
        _handoffs = handoffs ?? throw new ArgumentNullException(nameof(handoffs));
        _payrollPosting = payrollPosting ?? throw new ArgumentNullException(nameof(payrollPosting));
    }

    [HttpGet("payroll-posting-config/{branchId:long}")]
    [HrPermission("accounting-handoff", "view")]
    public async Task<IActionResult> GetPayrollPostingConfiguration(long branchId, CancellationToken cancellationToken)
    {
        if (_mediator != null)
        {
            var response = await _mediator.Send(new GetPayrollPostingConfigQuery(branchId), cancellationToken);
            if (response.Data == null)
                return NotFound(ApiResponse<object>.CreateFailure("Payroll posting configuration was not found.", statusCode: StatusCodes.Status404NotFound));
            return Ok(ApiResponse<object>.CreateSuccess(response.Data));
        }

        var configuration = await _payrollPosting!.GetAsync(branchId, cancellationToken);
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

        if (_mediator != null)
        {
            var response = await _mediator.Send(new SavePayrollPostingConfigCommand(configuration), cancellationToken);
            return Ok(ApiResponse<object>.CreateSuccess(response.Data!));
        }

        await _payrollPosting!.SaveAsync(configuration, cancellationToken);
        return Ok(ApiResponse<object>.CreateSuccess(configuration));
    }

    [HttpGet]
    [HrPermission("accounting-handoff", "view")]
    public async Task<IActionResult> Inbox(CancellationToken cancellationToken)
    {
        if (_mediator != null)
        {
            var response = await _mediator.Send(new GetAccountingInboxQuery(), cancellationToken);
            return Ok(ApiResponse<object>.CreateSuccess(response.Data!));
        }

        return Ok(ApiResponse<object>.CreateSuccess(await _handoffs!.GetAccountingInboxAsync(cancellationToken)));
    }

    [HttpGet("{id:long}/payments")]
    [HrPermission("accounting-handoff", "view")]
    public async Task<IActionResult> PaymentStatus(long id, CancellationToken cancellationToken)
    {
        if (_mediator != null)
        {
            var response = await _mediator.Send(new GetAccountingPaymentSummaryQuery(id), cancellationToken);
            if (!response.Success || response.Data == null)
                return NotFound(ApiResponse<object>.CreateFailure("Payroll handoff not found.", statusCode: StatusCodes.Status404NotFound));
            return Ok(ApiResponse<object>.CreateSuccess(response.Data));
        }

        var status = await _handoffs!.GetAccountingPaymentSummaryAsync(id, cancellationToken);
        if (status == null) return NotFound(ApiResponse<object>.CreateFailure("Payroll handoff not found.", statusCode: StatusCodes.Status404NotFound));
        return Ok(ApiResponse<object>.CreateSuccess(status));
    }

    [HttpPost("{id:long}/accept")]
    [HrPermission("accounting-handoff", "review")]
    public async Task<IActionResult> Accept(long id, CancellationToken cancellationToken)
    {
        if (_mediator != null)
        {
            var response = await _mediator.Send(new AcceptHandoffCommand(id, CurrentUsername), cancellationToken);
            return Ok(ApiResponse<object>.CreateSuccess(new
            {
                HandoffId = response.Data!.HandoffId,
                response.Data.HandoffReference,
                response.Data.DeliveryStatus
            }));
        }

        var handoff = await _handoffs!.ReviewAsync(id, true, null, CurrentUsername, cancellationToken);
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
        if (_mediator != null)
        {
            var response = await _mediator.Send(new ReturnHandoffCommand(id, request.Reason, CurrentUsername), cancellationToken);
            return Ok(ApiResponse<object>.CreateSuccess(new
            {
                HandoffId = response.Data!.HandoffId,
                response.Data.HandoffReference,
                response.Data.DeliveryStatus
            }));
        }

        var handoff = await _handoffs!.ReviewAsync(id, false, request.Reason, CurrentUsername, cancellationToken);
        return Ok(ApiResponse<object>.CreateSuccess(new
        {
            HandoffId = handoff.Id,
            handoff.HandoffReference,
            handoff.DeliveryStatus
        }));
    }

    [HttpPost("{id:long}/journal-voucher")]
    [HrPermission("accounting-handoff", "post")]
    public async Task<IActionResult> CreateJournalVoucher(long id, CancellationToken cancellationToken)
    {
        if (_mediator != null)
        {
            var response = await _mediator.Send(new CreateJournalVoucherCommand(id, CurrentUsername), cancellationToken);
            return Ok(ApiResponse<object>.CreateSuccess(new { response.Data!.VoucherId }));
        }

        return Ok(ApiResponse<object>.CreateSuccess(new { VoucherId = await _handoffs!.CreateJournalVoucherAsync(id, CurrentUsername, cancellationToken) }));
    }

    [HttpPost("{id:long}/journal-voucher/reverse")]
    [HrPermission("accounting-handoff", "reverse")]
    public async Task<IActionResult> ReverseJournalVoucher(long id, [FromBody] ReverseJournalVoucherRequest request, CancellationToken cancellationToken)
    {
        if (_mediator != null)
        {
            var response = await _mediator.Send(new ReverseJournalVoucherCommand(id, request.ReversalDate, request.Reason, CurrentUsername), cancellationToken);
            return Ok(ApiResponse<object>.CreateSuccess(new { HandoffId = response.Data!.HandoffId, response.Data.ReversalVoucherId, response.Data.Status }));
        }

        return Ok(ApiResponse<object>.CreateSuccess(new { HandoffId = id, ReversalVoucherId = await _handoffs!.ReverseJournalVoucherAsync(id, request.ReversalDate.ToDateTime(TimeOnly.MinValue), request.Reason, CurrentUsername, cancellationToken), Status = "REVERSED" }));
    }

    [HttpPost("{id:long}/posting-receipt")]
    [HrPermission("accounting-handoff", "post")]
    public async Task<IActionResult> PostingReceipt(long id, [FromBody] PostingReceiptRequest request, CancellationToken cancellationToken)
    {
        if (_mediator != null)
        {
            var response = await _mediator.Send(new ConfirmJournalPostedCommand(id, request.VoucherId, request.EventKey, CurrentUsername), cancellationToken);
            return Ok(ApiResponse<object>.CreateSuccess(new { HandoffId = response.Data!.HandoffId, response.Data.Status }));
        }

        await _handoffs!.ConfirmJournalPostedAsync(id, request.VoucherId, request.EventKey, CurrentUsername, cancellationToken);
        return Ok(ApiResponse<object>.CreateSuccess(new { HandoffId = id, Status = "POSTED" }));
    }

    [HttpPost("{id:long}/payments")]
    [HrPermission("accounting-handoff", "pay")]
    public async Task<IActionResult> PreparePayment(long id, [FromBody] PreparePaymentRequest request, CancellationToken cancellationToken)
    {
        if (_mediator != null)
        {
            var response = await _mediator.Send(new PreparePaymentCommand(id, request.Amount, request.Currency, request.Method, request.IdempotencyKey, CurrentUsername), cancellationToken);
            return Ok(ApiResponse<object>.CreateSuccess(response.Data!));
        }

        return Ok(ApiResponse<object>.CreateSuccess(await _handoffs!.PreparePaymentAsync(id, request.Amount, request.Currency, request.Method, request.IdempotencyKey, CurrentUsername, cancellationToken)));
    }

    [HttpPost("payments/{paymentId:long}/fail")]
    [HrPermission("accounting-handoff", "pay")]
    public async Task<IActionResult> FailPayment(long paymentId, [FromBody] FailPaymentRequest request, CancellationToken cancellationToken)
    {
        if (_mediator != null)
        {
            var response = await _mediator.Send(new FailPaymentCommand(paymentId, request.Reason, CurrentUsername), cancellationToken);
            return Ok(ApiResponse<object>.CreateSuccess(response.Data!));
        }

        return Ok(ApiResponse<object>.CreateSuccess(await _handoffs!.FailPaymentAsync(paymentId, request.Reason, CurrentUsername, cancellationToken)));
    }

    [HttpPost("payments/{paymentId:long}/reverse")]
    [HrPermission("accounting-handoff", "reverse")]
    public async Task<IActionResult> ReversePayment(long paymentId, [FromBody] ReversePaymentRequest request, CancellationToken cancellationToken)
    {
        if (_mediator != null)
        {
            var response = await _mediator.Send(new ReversePaymentCommand(paymentId, request.Reason, request.ExternalReference, request.EvidenceReference, CurrentUsername), cancellationToken);
            return Ok(ApiResponse<object>.CreateSuccess(response.Data!));
        }

        return Ok(ApiResponse<object>.CreateSuccess(await _handoffs!.ReversePaymentAsync(paymentId, request.Reason, request.ExternalReference, request.EvidenceReference, CurrentUsername, cancellationToken)));
    }

    [HttpPost("payments/{paymentId:long}/confirm")]
    [HrPermission("accounting-handoff", "pay")]
    public async Task<IActionResult> ConfirmPayment(long paymentId, [FromBody] ConfirmPaymentRequest request, CancellationToken cancellationToken)
    {
        if (_mediator != null)
        {
            var response = await _mediator.Send(new ConfirmPaymentCommand(paymentId, request.ExternalReference, request.EvidenceReference, CurrentUsername), cancellationToken);
            return Ok(ApiResponse<object>.CreateSuccess(response.Data!));
        }

        return Ok(ApiResponse<object>.CreateSuccess(await _handoffs!.ConfirmPaymentAsync(paymentId, request.ExternalReference, request.EvidenceReference, CurrentUsername, cancellationToken)));
    }

    [HttpPost("{id:long}/loan-funding-receipt")]
    [HrPermission("accounting-handoff", "fund")]
    public async Task<IActionResult> ConfirmLoanFunding(long id, [FromBody] FundingReceiptRequest request, CancellationToken cancellationToken)
    {
        if (_mediator != null)
        {
            var response = await _mediator.Send(new ConfirmLoanFundingCommand(id, request.FundedAmount, request.Currency, request.FundingVoucherId, request.EventKey, request.ExternalReference, CurrentUsername), cancellationToken);
            return Ok(ApiResponse<object>.CreateSuccess(new { HandoffId = response.Data!.HandoffId, response.Data.FundingVoucherId, response.Data.Status }));
        }

        await _handoffs!.ConfirmLoanFundingAsync(id, request.FundedAmount, request.Currency, request.FundingVoucherId, request.EventKey, request.ExternalReference, CurrentUsername, cancellationToken);
        return Ok(ApiResponse<object>.CreateSuccess(new { HandoffId = id, request.FundingVoucherId, Status = "POSTED" }));
    }

    [HttpPost("{id:long}/loan-funding-reverse")]
    [HrPermission("accounting-handoff", "reverse")]
    public async Task<IActionResult> ReverseLoanFunding(long id, [FromBody] ReverseFundingRequest request, CancellationToken cancellationToken)
    {
        if (_mediator != null)
        {
            var response = await _mediator.Send(new ReverseLoanFundingCommand(id, request.Reason, request.EventKey, request.ExternalReference, request.EvidenceReference, CurrentUsername), cancellationToken);
            return Ok(ApiResponse<object>.CreateSuccess(new { HandoffId = response.Data!.HandoffId, response.Data.ReversalVoucherId, response.Data.Status }));
        }

        var reversalVoucherId = await _handoffs!.ReverseLoanFundingAsync(id, request.Reason, request.EventKey, request.ExternalReference, request.EvidenceReference, CurrentUsername, cancellationToken);
        return Ok(ApiResponse<object>.CreateSuccess(new { HandoffId = id, ReversalVoucherId = reversalVoucherId, Status = "REVERSED" }));
    }
}
