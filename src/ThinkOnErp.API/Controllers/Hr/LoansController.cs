using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using ThinkOnErp.API.Authorization;
using ThinkOnErp.API.Swagger;
using ThinkOnErp.Application.Common;
using ThinkOnErp.Application.DTOs.Hr;
using ThinkOnErp.Application.Features.Hr.Loans.Commands.ApproveAdvance;
using ThinkOnErp.Application.Features.Hr.Loans.Commands.ApproveLoan;
using ThinkOnErp.Application.Features.Hr.Loans.Commands.CancelAdvance;
using ThinkOnErp.Application.Features.Hr.Loans.Commands.CancelLoan;
using ThinkOnErp.Application.Features.Hr.Loans.Commands.CreateAdvanceRequest;
using ThinkOnErp.Application.Features.Hr.Loans.Commands.CreateLoanApplication;
using ThinkOnErp.Application.Features.Hr.Loans.Commands.SettleAdvanceEarly;
using ThinkOnErp.Application.Features.Hr.Loans.Commands.SettleLoanEarly;
using ThinkOnErp.Application.Features.Hr.Loans.Commands.SkipInstallment;
using ThinkOnErp.Application.Features.Hr.Loans.Commands.SubmitAdvanceFunding;
using ThinkOnErp.Application.Features.Hr.Loans.Commands.SubmitLoanFunding;
using ThinkOnErp.Application.Features.Hr.Loans.Queries.GetAdvances;
using ThinkOnErp.Application.Features.Hr.Loans.Queries.GetLoans;
using ThinkOnErp.Domain.Entities.Hr;

namespace ThinkOnErp.API.Controllers.Hr;

[ApiController]
[Route("api/hr/loans")]
[ApiExplorerSettings(GroupName = ApiCategories.Hr)]
[TenantScoped]
[Authorize]
public sealed class LoansController : HrControllerBase
{
    private readonly IMediator _mediator;

    public LoansController(IMediator mediator)
    {
        _mediator = mediator;
    }

    [HttpPost]
    [ProducesResponseType(typeof(ApiResponse<EmployeeLoan>), StatusCodes.Status200OK)]
    [HrPermission("hr-loans", "create", selfService: true)]
    public async Task<ActionResult<ApiResponse<EmployeeLoan>>> CreateLoan(
        [FromBody] CreateLoanApplicationDto dto,
        CancellationToken cancellationToken)
        => FromResponse(await _mediator.Send(new CreateLoanApplicationCommand(dto, CurrentUsername), cancellationToken));

    [HttpPost("{id:long}/approve")]
    [ProducesResponseType(typeof(ApiResponse<EmployeeLoan>), StatusCodes.Status200OK)]
    [HrPermission("hr-loans", "approve")]
    public async Task<ActionResult<ApiResponse<EmployeeLoan>>> ApproveLoan(
        long id,
        CancellationToken cancellationToken)
        => FromResponse(await _mediator.Send(new ApproveLoanCommand(id, CurrentUsername), cancellationToken));

    [HttpPost("{id:long}/funding-request")]
    [HrPermission("hr-loans", "approve")]
    public async Task<ActionResult<ApiResponse<object>>> SubmitLoanFunding(long id, CancellationToken cancellationToken)
        => FromResponse(await _mediator.Send(new SubmitLoanFundingCommand(id, CurrentUsername), cancellationToken));

    [HttpGet]
    [ProducesResponseType(typeof(ApiResponse<IReadOnlyList<EmployeeLoan>>), StatusCodes.Status200OK)]
    [HrPermission("hr-loans", "view", selfService: true)]
    public async Task<ActionResult<ApiResponse<IReadOnlyList<EmployeeLoan>>>> GetLoans(
        [FromQuery] string? employeeCode,
        [FromQuery] string? status,
        CancellationToken cancellationToken)
        => FromResponse(await _mediator.Send(new GetLoansQuery(employeeCode, status), cancellationToken));

    [HttpPost("advances")]
    [ProducesResponseType(typeof(ApiResponse<EmployeeAdvance>), StatusCodes.Status200OK)]
    [HrPermission("hr-loans", "create", selfService: true)]
    public async Task<ActionResult<ApiResponse<EmployeeAdvance>>> CreateAdvance(
        [FromBody] CreateAdvanceRequestDto dto,
        CancellationToken cancellationToken)
        => FromResponse(await _mediator.Send(new CreateAdvanceRequestCommand(dto, CurrentUsername), cancellationToken));

    [HttpPost("advances/{id:long}/approve")]
    [ProducesResponseType(typeof(ApiResponse<EmployeeAdvance>), StatusCodes.Status200OK)]
    [HrPermission("hr-loans", "approve")]
    public async Task<ActionResult<ApiResponse<EmployeeAdvance>>> ApproveAdvance(
        long id,
        CancellationToken cancellationToken)
        => FromResponse(await _mediator.Send(new ApproveAdvanceCommand(id, CurrentUsername), cancellationToken));

    [HttpPost("advances/{id:long}/funding-request")]
    [HrPermission("hr-loans", "approve")]
    public async Task<ActionResult<ApiResponse<object>>> SubmitAdvanceFunding(long id, CancellationToken cancellationToken)
        => FromResponse(await _mediator.Send(new SubmitAdvanceFundingCommand(id, CurrentUsername), cancellationToken));

    [HttpGet("advances")]
    [ProducesResponseType(typeof(ApiResponse<IReadOnlyList<EmployeeAdvance>>), StatusCodes.Status200OK)]
    [HrPermission("hr-loans", "view", selfService: true)]
    public async Task<ActionResult<ApiResponse<IReadOnlyList<EmployeeAdvance>>>> GetAdvances(
        [FromQuery] string? employeeCode,
        [FromQuery] string? targetPayPeriod,
        CancellationToken cancellationToken)
        => FromResponse(await _mediator.Send(new GetAdvancesQuery(employeeCode, targetPayPeriod), cancellationToken));

    [HttpPost("schedules/{scheduleId:long}/skip")]
    [ProducesResponseType(typeof(ApiResponse<LoanRepaymentSchedule>), StatusCodes.Status200OK)]
    [HrPermission("hr-loans", "edit")]
    public async Task<ActionResult<ApiResponse<LoanRepaymentSchedule>>> SkipInstallment(
        long scheduleId,
        [FromBody] SkipLoanInstallmentDto dto,
        CancellationToken cancellationToken)
        => FromResponse(await _mediator.Send(new SkipInstallmentCommand(scheduleId, dto.Reason, CurrentUsername), cancellationToken));

    [HttpPost("{id:long}/cancel")]
    [ProducesResponseType(typeof(ApiResponse<EmployeeLoan>), StatusCodes.Status200OK)]
    [HrPermission("hr-loans", "reverse")]
    public async Task<ActionResult<ApiResponse<EmployeeLoan>>> CancelLoan(
        long id,
        [FromBody] CancelLoanDto dto,
        CancellationToken cancellationToken)
        => FromResponse(await _mediator.Send(new CancelLoanCommand(id, dto.Reason, CurrentUsername), cancellationToken));

    [HttpPost("advances/{id:long}/cancel")]
    [ProducesResponseType(typeof(ApiResponse<EmployeeAdvance>), StatusCodes.Status200OK)]
    [HrPermission("hr-loans", "reverse")]
    public async Task<ActionResult<ApiResponse<EmployeeAdvance>>> CancelAdvance(
        long id,
        [FromBody] CancelAdvanceDto dto,
        CancellationToken cancellationToken)
        => FromResponse(await _mediator.Send(new CancelAdvanceCommand(id, dto.Reason, CurrentUsername), cancellationToken));

    [HttpPost("{id:long}/settle-early")]
    [ProducesResponseType(typeof(ApiResponse<EmployeeLoan>), StatusCodes.Status200OK)]
    [HrPermission("hr-loans", "edit")]
    public async Task<ActionResult<ApiResponse<EmployeeLoan>>> SettleLoanEarly(
        long id,
        [FromBody] SettleLoanEarlyDto dto,
        CancellationToken cancellationToken)
        => FromResponse(await _mediator.Send(new SettleLoanEarlyCommand(id, dto, CurrentUsername), cancellationToken));

    [HttpPost("advances/{id:long}/settle-early")]
    [ProducesResponseType(typeof(ApiResponse<EmployeeAdvance>), StatusCodes.Status200OK)]
    [HrPermission("hr-loans", "edit")]
    public async Task<ActionResult<ApiResponse<EmployeeAdvance>>> SettleAdvanceEarly(
        long id,
        [FromBody] SettleAdvanceEarlyDto dto,
        CancellationToken cancellationToken)
        => FromResponse(await _mediator.Send(new SettleAdvanceEarlyCommand(id, dto, CurrentUsername), cancellationToken));
}
