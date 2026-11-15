using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using ThinkOnErp.API.Authorization;
using ThinkOnErp.API.Swagger;
using ThinkOnErp.Application.Common;
using ThinkOnErp.Application.DTOs.Hr;
using ThinkOnErp.Application.Services.Hr;
using ThinkOnErp.Domain.Entities.Hr;

namespace ThinkOnErp.API.Controllers.Hr;

[ApiController]
[Route("api/hr/loans")]
[ApiExplorerSettings(GroupName = ApiCategories.Hr)]
[TenantScoped]
[Authorize]
public sealed class LoansController : ControllerBase
{
    private readonly ILoanDeductionService _loanService;
    private readonly ILogger<LoansController> _logger;
    private readonly IHrAccountingHandoffService _handoffs;

    public LoansController(
        ILoanDeductionService loanService,
        IHrAccountingHandoffService handoffs,
        ILogger<LoansController> logger)
    {
        _loanService = loanService;
        _handoffs = handoffs;
        _logger = logger;
    }

    [HttpPost]
    [ProducesResponseType(typeof(ApiResponse<EmployeeLoan>), StatusCodes.Status200OK)]
    [HrPermission("hr-loans", "create", selfService: true)]
    public async Task<ActionResult<ApiResponse<EmployeeLoan>>> CreateLoan(
        [FromBody] CreateLoanApplicationDto dto,
        CancellationToken cancellationToken)
    {
        var user = User.Identity?.Name ?? "SYSTEM";
        var result = await _loanService.CreateLoanApplicationAsync(dto, user, cancellationToken);
        return Ok(ApiResponse<EmployeeLoan>.CreateSuccess(result, "Loan application created successfully."));
    }

    [HttpPost("{id:long}/approve")]
    [ProducesResponseType(typeof(ApiResponse<EmployeeLoan>), StatusCodes.Status200OK)]
    [HrPermission("hr-loans", "approve")]
    public async Task<ActionResult<ApiResponse<EmployeeLoan>>> ApproveLoan(
        long id,
        CancellationToken cancellationToken)
    {
        var user = User.Identity?.Name ?? "SYSTEM";
        var result = await _loanService.ApproveLoanAsync(id, user, cancellationToken);
        return Ok(ApiResponse<EmployeeLoan>.CreateSuccess(result, "Loan request approved by HR; Accounting disbursement confirmation is still required."));
    }

    [HttpPost("{id:long}/funding-request")]
    [HrPermission("hr-loans", "approve")]
    public async Task<IActionResult> SubmitLoanFunding(long id, CancellationToken cancellationToken) =>
        Ok(ApiResponse<object>.CreateSuccess(await _handoffs.CreateApprovedFundingHandoffAsync("LOAN", id, User.Identity?.Name ?? "SYSTEM", cancellationToken)));

    [HttpGet]
    [ProducesResponseType(typeof(ApiResponse<IReadOnlyList<EmployeeLoan>>), StatusCodes.Status200OK)]
    [HrPermission("hr-loans", "view", selfService: true)]
    public async Task<ActionResult<ApiResponse<IReadOnlyList<EmployeeLoan>>>> GetLoans(
        [FromQuery] string? employeeCode,
        [FromQuery] string? status,
        CancellationToken cancellationToken)
    {
        var result = await _loanService.GetLoansAsync(employeeCode, status, cancellationToken);
        return Ok(ApiResponse<IReadOnlyList<EmployeeLoan>>.CreateSuccess(result, "Loans retrieved successfully."));
    }

    [HttpPost("advances")]
    [ProducesResponseType(typeof(ApiResponse<EmployeeAdvance>), StatusCodes.Status200OK)]
    [HrPermission("hr-loans", "create", selfService: true)]
    public async Task<ActionResult<ApiResponse<EmployeeAdvance>>> CreateAdvance(
        [FromBody] CreateAdvanceRequestDto dto,
        CancellationToken cancellationToken)
    {
        var user = User.Identity?.Name ?? "SYSTEM";
        var result = await _loanService.CreateAdvanceRequestAsync(dto, user, cancellationToken);
        return Ok(ApiResponse<EmployeeAdvance>.CreateSuccess(result, "Advance request created successfully."));
    }

    [HttpPost("advances/{id:long}/approve")]
    [ProducesResponseType(typeof(ApiResponse<EmployeeAdvance>), StatusCodes.Status200OK)]
    [HrPermission("hr-loans", "approve")]
    public async Task<ActionResult<ApiResponse<EmployeeAdvance>>> ApproveAdvance(
        long id,
        CancellationToken cancellationToken)
    {
        var user = User.Identity?.Name ?? "SYSTEM";
        var result = await _loanService.ApproveAdvanceAsync(id, user, cancellationToken);
        return Ok(ApiResponse<EmployeeAdvance>.CreateSuccess(result, "Advance request approved by HR; Accounting disbursement confirmation is still required."));
    }

    [HttpPost("advances/{id:long}/funding-request")]
    [HrPermission("hr-loans", "approve")]
    public async Task<IActionResult> SubmitAdvanceFunding(long id, CancellationToken cancellationToken) =>
        Ok(ApiResponse<object>.CreateSuccess(await _handoffs.CreateApprovedFundingHandoffAsync("ADVANCE", id, User.Identity?.Name ?? "SYSTEM", cancellationToken)));

    [HttpGet("advances")]
    [ProducesResponseType(typeof(ApiResponse<IReadOnlyList<EmployeeAdvance>>), StatusCodes.Status200OK)]
    [HrPermission("hr-loans", "view", selfService: true)]
    public async Task<ActionResult<ApiResponse<IReadOnlyList<EmployeeAdvance>>>> GetAdvances(
        [FromQuery] string? employeeCode,
        [FromQuery] string? targetPayPeriod,
        CancellationToken cancellationToken)
    {
        var result = await _loanService.GetAdvancesAsync(employeeCode, targetPayPeriod, cancellationToken);
        return Ok(ApiResponse<IReadOnlyList<EmployeeAdvance>>.CreateSuccess(result, "Advances retrieved successfully."));
    }

    [HttpPost("schedules/{scheduleId:long}/skip")]
    [ProducesResponseType(typeof(ApiResponse<LoanRepaymentSchedule>), StatusCodes.Status200OK)]
    [HrPermission("hr-loans", "edit")]
    public async Task<ActionResult<ApiResponse<LoanRepaymentSchedule>>> SkipInstallment(
        long scheduleId,
        [FromBody] SkipLoanInstallmentDto dto,
        CancellationToken cancellationToken)
    {
        var user = User.Identity?.Name ?? "SYSTEM";
        var result = await _loanService.SkipInstallmentAsync(scheduleId, dto.Reason, user, cancellationToken);
        return Ok(ApiResponse<LoanRepaymentSchedule>.CreateSuccess(result, "Loan installment skipped and deferred successfully."));
    }

    [HttpPost("{id:long}/cancel")]
    [ProducesResponseType(typeof(ApiResponse<EmployeeLoan>), StatusCodes.Status200OK)]
    [HrPermission("hr-loans", "reverse")]
    public async Task<ActionResult<ApiResponse<EmployeeLoan>>> CancelLoan(
        long id,
        [FromBody] CancelLoanDto dto,
        CancellationToken cancellationToken)
    {
        var user = User.Identity?.Name ?? "SYSTEM";
        var result = await _loanService.CancelLoanAsync(id, dto.Reason, user, cancellationToken);
        return Ok(ApiResponse<EmployeeLoan>.CreateSuccess(result, "Loan cancelled successfully."));
    }

    [HttpPost("advances/{id:long}/cancel")]
    [ProducesResponseType(typeof(ApiResponse<EmployeeAdvance>), StatusCodes.Status200OK)]
    [HrPermission("hr-loans", "reverse")]
    public async Task<ActionResult<ApiResponse<EmployeeAdvance>>> CancelAdvance(
        long id,
        [FromBody] CancelAdvanceDto dto,
        CancellationToken cancellationToken)
    {
        var user = User.Identity?.Name ?? "SYSTEM";
        var result = await _loanService.CancelAdvanceAsync(id, dto.Reason, user, cancellationToken);
        return Ok(ApiResponse<EmployeeAdvance>.CreateSuccess(result, "Advance cancelled successfully."));
    }

    [HttpPost("{id:long}/settle-early")]
    [ProducesResponseType(typeof(ApiResponse<EmployeeLoan>), StatusCodes.Status200OK)]
    [HrPermission("hr-loans", "edit")]
    public async Task<ActionResult<ApiResponse<EmployeeLoan>>> SettleLoanEarly(
        long id,
        [FromBody] SettleLoanEarlyDto dto,
        CancellationToken cancellationToken)
    {
        var user = User.Identity?.Name ?? "SYSTEM";
        var result = await _loanService.SettleLoanEarlyAsync(id, dto, user, cancellationToken);
        return Ok(ApiResponse<EmployeeLoan>.CreateSuccess(result, "Loan settled early successfully."));
    }

    [HttpPost("advances/{id:long}/settle-early")]
    [ProducesResponseType(typeof(ApiResponse<EmployeeAdvance>), StatusCodes.Status200OK)]
    [HrPermission("hr-loans", "edit")]
    public async Task<ActionResult<ApiResponse<EmployeeAdvance>>> SettleAdvanceEarly(
        long id,
        [FromBody] SettleAdvanceEarlyDto dto,
        CancellationToken cancellationToken)
    {
        var user = User.Identity?.Name ?? "SYSTEM";
        var result = await _loanService.SettleAdvanceEarlyAsync(id, dto, user, cancellationToken);
        return Ok(ApiResponse<EmployeeAdvance>.CreateSuccess(result, "Advance settled early successfully."));
    }
}
