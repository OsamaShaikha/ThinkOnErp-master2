using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using ThinkOnErp.API.Authorization;
using ThinkOnErp.API.Swagger;
using ThinkOnErp.Application.Common;
using ThinkOnErp.Application.DTOs.Hr;
using ThinkOnErp.Application.Features.Hr.Payroll.Commands.ApprovePayroll;
using ThinkOnErp.Application.Features.Hr.Payroll.Commands.CalculatePayroll;
using ThinkOnErp.Application.Features.Hr.Payroll.Commands.CancelPayroll;
using ThinkOnErp.Application.Features.Hr.Payroll.Commands.ClosePeriod;
using ThinkOnErp.Application.Features.Hr.Payroll.Commands.CorrectPayroll;
using ThinkOnErp.Application.Features.Hr.Payroll.Commands.CreatePeriod;
using ThinkOnErp.Application.Features.Hr.Payroll.Commands.ReopenPeriod;
using ThinkOnErp.Application.Features.Hr.Payroll.Commands.WithdrawPayrollHandoff;
using ThinkOnErp.Application.Features.Hr.Payroll.Queries.ComparePayrollRun;
using ThinkOnErp.Application.Features.Hr.Payroll.Queries.GetCalculationExplanation;
using ThinkOnErp.Application.Features.Hr.Payroll.Queries.GetEmployeePayslip;
using ThinkOnErp.Application.Features.Hr.Payroll.Queries.GetMyPayslips;
using ThinkOnErp.Application.Features.Hr.Payroll.Queries.GetPayrollHandoffPaymentStatus;
using ThinkOnErp.Application.Features.Hr.Payroll.Queries.GetPayrollHandoffStatus;
using ThinkOnErp.Application.Features.Hr.Payroll.Queries.GetPayrollRunById;
using ThinkOnErp.Application.Features.Hr.Payroll.Queries.GetPayrollRuns;
using ThinkOnErp.Application.Features.Hr.Payroll.Queries.GetPeriods;
using ThinkOnErp.Application.Services.Hr;
using ThinkOnErp.Domain.Entities.Hr;
using ThinkOnErp.Domain.Models;

namespace ThinkOnErp.API.Controllers.Hr;

[ApiController]
[Route("api/hr/payroll")]
[ApiExplorerSettings(GroupName = ApiCategories.Hr)]
[TenantScoped]
[Authorize]
public sealed class PayrollController : HrControllerBase
{
    private readonly IMediator? _mediator;
    private readonly IPayrollService? _payrollService;
    private readonly IPayrollExplanationService? _explanationService;
    private readonly ILogger<PayrollController>? _logger;

    [ActivatorUtilitiesConstructor]
    public PayrollController(IMediator mediator)
    {
        _mediator = mediator ?? throw new ArgumentNullException(nameof(mediator));
    }

    [Obsolete("Transitional constructor for existing test suites. Production uses the IMediator constructor.")]
    public PayrollController(
        IPayrollService payrollService,
        IPayrollExplanationService explanationService,
        ILogger<PayrollController> logger)
    {
        _payrollService = payrollService ?? throw new ArgumentNullException(nameof(payrollService));
        _explanationService = explanationService ?? throw new ArgumentNullException(nameof(explanationService));
        _logger = logger;
    }

    [HttpPost("calculate")]
    [ProducesResponseType(typeof(ApiResponse<PayrollCalculationResultDto>), StatusCodes.Status200OK)]
    [HrPermission("hr-payroll", "edit")]
    public async Task<ActionResult<ApiResponse<PayrollCalculationResultDto>>> CalculatePayroll(
        [FromBody] CreatePayrollRunDto dto,
        CancellationToken cancellationToken)
    {
        if (_mediator != null)
            return FromResponse(await _mediator.Send(new CalculatePayrollCommand(dto, CurrentUsername), cancellationToken));

        var user = CurrentUsername;
        var result = await _payrollService!.CreateAndCalculatePayrollRunAsync(dto, user, cancellationToken);
        return Ok(ApiResponse<PayrollCalculationResultDto>.CreateSuccess(result, "Payroll run calculated successfully."));
    }

    [HttpPost("{id:long}/approve")]
    [ProducesResponseType(typeof(ApiResponse<PayrollRun>), StatusCodes.Status200OK)]
    [HrPermission("hr-payroll", "approve")]
    public async Task<ActionResult<ApiResponse<PayrollRun>>> ApprovePayroll(
        long id,
        CancellationToken cancellationToken)
    {
        if (_mediator != null)
            return FromResponse(await _mediator.Send(new ApprovePayrollCommand(id, CurrentUsername), cancellationToken));

        var user = CurrentUsername;
        var result = await _payrollService!.ApprovePayrollRunAsync(id, user, cancellationToken);
        return Ok(ApiResponse<PayrollRun>.CreateSuccess(result, "Payroll run approved successfully."));
    }

    [HttpPost("{id:long}/correct")]
    [ProducesResponseType(typeof(ApiResponse<PayrollCalculationResultDto>), StatusCodes.Status200OK)]
    [HrPermission("hr-payroll", "reverse")]
    public async Task<ActionResult<ApiResponse<PayrollCalculationResultDto>>> CorrectPayroll(
        long id, [FromBody] PayrollCorrectionRequest request, CancellationToken cancellationToken)
    {
        if (_mediator != null)
            return FromResponse(await _mediator.Send(new CorrectPayrollCommand(id, request.Reason, CurrentUsername), cancellationToken));

        var result = await _payrollService!.CreatePayrollCorrectionAsync(id, request.Reason, CurrentUsername, cancellationToken);
        return Ok(ApiResponse<PayrollCalculationResultDto>.CreateSuccess(result, "A new payroll correction version was calculated; the prior run remains unchanged."));
    }

    [HttpPost("{id:long}/cancel")]
    [ProducesResponseType(typeof(ApiResponse<PayrollCalculationResultDto>), StatusCodes.Status200OK)]
    [HrPermission("hr-payroll", "reverse")]
    public async Task<ActionResult<ApiResponse<PayrollCalculationResultDto>>> CancelPayroll(
        long id, [FromBody] CancelPayrollRunDto dto, CancellationToken cancellationToken)
    {
        if (_mediator != null)
            return FromResponse(await _mediator.Send(new CancelPayrollCommand(id, dto, CurrentUsername), cancellationToken));

        var result = await _payrollService!.CancelPayrollRunAsync(id, dto, CurrentUsername, cancellationToken);
        return Ok(ApiResponse<PayrollCalculationResultDto>.CreateSuccess(result, "Payroll calculation cancelled; its history is retained."));
    }

    [HttpPost("{id:long}/post-to-gl")]
    [ProducesResponseType(typeof(ApiResponse<PostGlVoucherResultDto>), StatusCodes.Status409Conflict)]
    [HrPermission("hr-payroll", "post")]
    public Task<ActionResult<ApiResponse<PostGlVoucherResultDto>>> PostToGl(
        long id,
        CancellationToken cancellationToken)
    {
        return Task.FromResult<ActionResult<ApiResponse<PostGlVoucherResultDto>>>(Conflict(AccountingHandoffRequired<PostGlVoucherResultDto>()));
    }

    [HttpPost("{id:long}/reverse-gl")]
    [ProducesResponseType(typeof(ApiResponse<PayrollGlReversalResultDto>), StatusCodes.Status409Conflict)]
    [HrPermission("hr-payroll", "reverse")]
    public Task<ActionResult<ApiResponse<PayrollGlReversalResultDto>>> ReverseGl(
        long id,
        [FromBody] ReversePayrollGlDto dto,
        CancellationToken cancellationToken)
    {
        return Task.FromResult<ActionResult<ApiResponse<PayrollGlReversalResultDto>>>(Conflict(AccountingHandoffRequired<PayrollGlReversalResultDto>()));
    }

    [HttpGet("{id:long}")]
    [ProducesResponseType(typeof(ApiResponse<PayrollCalculationResultDto>), StatusCodes.Status200OK)]
    [HrPermission("hr-payroll", "view")]
    public async Task<ActionResult<ApiResponse<PayrollCalculationResultDto>>> GetPayrollRun(
        long id,
        CancellationToken cancellationToken)
    {
        if (_mediator != null)
            return FromResponse(await _mediator.Send(new GetPayrollRunByIdQuery(id), cancellationToken));

        var result = await _payrollService!.GetPayrollRunByIdAsync(id, cancellationToken);
        if (result == null) return NotFound(ApiResponse<PayrollCalculationResultDto>.CreateFailure("Payroll run not found.", statusCode: StatusCodes.Status404NotFound));
        return Ok(ApiResponse<PayrollCalculationResultDto>.CreateSuccess(result, "Payroll run retrieved successfully."));
    }

    [HttpGet]
    [ProducesResponseType(typeof(ApiResponse<PagedResultDto<PayrollRunListItemDto>>), StatusCodes.Status200OK)]
    [HrPermission("hr-payroll", "view")]
    public async Task<ActionResult<ApiResponse<PagedResultDto<PayrollRunListItemDto>>>> GetPayrollRuns(
        [FromQuery] string? period,
        [FromQuery] long? branchId,
        [FromQuery] string? status,
        [FromQuery] int pageIndex = 1,
        [FromQuery] int pageSize = 20,
        CancellationToken cancellationToken = default)
    {
        if (_mediator != null)
            return FromResponse(await _mediator.Send(new GetPayrollRunsQuery(ResolveTenantCompanyId(), period, branchId, status, pageIndex, pageSize), cancellationToken));

        if (pageIndex is < 1 or > 1_000_000 || pageSize is < 1 or > 100 || branchId is <= 0)
            return BadRequest(ApiResponse<PagedResultDto<PayrollRunListItemDto>>.CreateFailure(
                "Page, page size or branch filter is invalid.", statusCode: StatusCodes.Status400BadRequest));

        var result = await _payrollService!.GetPayrollRunsAsync(
            ResolveTenantCompanyId(), period, branchId, status, pageIndex, pageSize, cancellationToken);
        return Ok(ApiResponse<PagedResultDto<PayrollRunListItemDto>>.CreateSuccess(
            result, "Payroll runs retrieved successfully."));
    }

    [HttpGet("me/payslips")]
    [HrPermission("hr-payroll", "view", selfService: true)]
    [ProducesResponseType(typeof(ApiResponse<PagedResultDto<PayslipSummaryDto>>), StatusCodes.Status200OK)]
    public async Task<ActionResult<ApiResponse<PagedResultDto<PayslipSummaryDto>>>> GetMyPayslips(
        [FromQuery] int pageIndex = 1,
        [FromQuery] int pageSize = 20,
        CancellationToken cancellationToken = default)
    {
        if (!TryGetCurrentUserId(out var userId))
            return Unauthorized();

        if (_mediator != null)
            return FromResponse(await _mediator.Send(new GetMyPayslipsQuery(ResolveTenantCompanyId(), userId, pageIndex, pageSize), cancellationToken));

        if (pageIndex is < 1 or > 1_000_000 || pageSize is < 1 or > 100)
            return BadRequest(ApiResponse<PagedResultDto<PayslipSummaryDto>>.CreateFailure(
                "Page or page size is invalid.", statusCode: StatusCodes.Status400BadRequest));

        var result = await _payrollService!.GetMyPayslipsAsync(
            ResolveTenantCompanyId(), userId, pageIndex, pageSize, cancellationToken);
        if (result == null)
            return NotFound(ApiResponse<PagedResultDto<PayslipSummaryDto>>.CreateFailure(
                "No employee is linked to this account.", statusCode: StatusCodes.Status404NotFound));
        return Ok(ApiResponse<PagedResultDto<PayslipSummaryDto>>.CreateSuccess(
            result, "Payslips retrieved successfully."));
    }

    [HttpGet("{id:long}/lines/{employeeCode}/payslip")]
    [HrPermission("hr-payroll", "view")]
    [ProducesResponseType(typeof(ApiResponse<PayslipSummaryDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<ApiResponse<PayslipSummaryDto>>> GetEmployeePayslip(
        long id, string employeeCode, CancellationToken cancellationToken)
    {
        if (_mediator != null)
            return FromResponse(await _mediator.Send(new GetEmployeePayslipQuery(ResolveTenantCompanyId(), id, employeeCode), cancellationToken));

        var result = await _payrollService!.GetEmployeePayslipAsync(
            ResolveTenantCompanyId(), id, employeeCode, cancellationToken);
        if (result == null)
            return NotFound(ApiResponse<PayslipSummaryDto>.CreateFailure(
                "Approved payslip not found.", statusCode: StatusCodes.Status404NotFound));
        return Ok(ApiResponse<PayslipSummaryDto>.CreateSuccess(result, "Payslip retrieved successfully."));
    }

    [HttpGet("{id:long}/handoff")]
    [ProducesResponseType(typeof(ApiResponse<PayrollHandoffStatusDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<PayrollHandoffStatusDto>), StatusCodes.Status404NotFound)]
    [HrPermission("hr-payroll", "view")]
    public async Task<ActionResult<ApiResponse<PayrollHandoffStatusDto>>> GetPayrollHandoffStatus(
        long id,
        CancellationToken cancellationToken)
    {
        if (_mediator != null)
            return FromResponse(await _mediator.Send(new GetPayrollHandoffStatusQuery(id), cancellationToken));

        var status = await _payrollService!.GetPayrollHandoffStatusAsync(id, cancellationToken);
        if (status == null)
            return NotFound(ApiResponse<PayrollHandoffStatusDto>.CreateFailure("Payroll handoff not found.", statusCode: StatusCodes.Status404NotFound));
        return Ok(ApiResponse<PayrollHandoffStatusDto>.CreateSuccess(status, "Payroll handoff status retrieved successfully."));
    }

    [HttpGet("{id:long}/handoff/payments")]
    [ProducesResponseType(typeof(ApiResponse<PayrollHandoffPaymentStatusDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<PayrollHandoffPaymentStatusDto>), StatusCodes.Status404NotFound)]
    [HrPermission("hr-payroll", "view")]
    public async Task<ActionResult<ApiResponse<PayrollHandoffPaymentStatusDto>>> GetPayrollHandoffPaymentStatus(
        long id,
        CancellationToken cancellationToken)
    {
        if (_mediator != null)
            return FromResponse(await _mediator.Send(new GetPayrollHandoffPaymentStatusQuery(id), cancellationToken));

        var status = await _payrollService!.GetPayrollHandoffPaymentStatusAsync(id, cancellationToken);
        if (status == null)
            return NotFound(ApiResponse<PayrollHandoffPaymentStatusDto>.CreateFailure("Payroll handoff payment status not found.", statusCode: StatusCodes.Status404NotFound));
        return Ok(ApiResponse<PayrollHandoffPaymentStatusDto>.CreateSuccess(status, "Payroll payment status retrieved successfully."));
    }

    [HttpPost("{id:long}/handoff/withdraw")]
    [ProducesResponseType(typeof(ApiResponse<PayrollHandoffStatusDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<PayrollHandoffStatusDto>), StatusCodes.Status404NotFound)]
    [HrPermission("hr-payroll", "reverse")]
    public async Task<ActionResult<ApiResponse<PayrollHandoffStatusDto>>> WithdrawPayrollHandoff(
        long id,
        [FromBody] WithdrawHandoffRequest request,
        CancellationToken cancellationToken)
    {
        if (_mediator != null)
            return FromResponse(await _mediator.Send(new WithdrawPayrollHandoffCommand(id, request.Reason, CurrentUsername), cancellationToken));

        var result = await _payrollService!.WithdrawPayrollHandoffAsync(id, request.Reason, CurrentUsername, cancellationToken);
        if (result == null)
            return NotFound(ApiResponse<PayrollHandoffStatusDto>.CreateFailure("Payroll handoff not found.", statusCode: StatusCodes.Status404NotFound));
        return Ok(ApiResponse<PayrollHandoffStatusDto>.CreateSuccess(result, "Payroll handoff withdrawn before Accounting acceptance."));
    }

    [HttpGet("{id:long}/export-bank")]
    [HrPermission("hr-payroll", "export")]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status409Conflict)]
    public Task<IActionResult> ExportBankFile(
        long id,
        [FromQuery] string? format,
        CancellationToken cancellationToken)
    {
        return Task.FromResult<IActionResult>(Conflict(AccountingHandoffRequired<object>()));
    }

    [HttpGet("{id:long}/export-bank/preview")]
    [ProducesResponseType(typeof(ApiResponse<BankPayrollExportResultDto>), StatusCodes.Status409Conflict)]
    [HrPermission("hr-payroll", "export")]
    public Task<ActionResult<ApiResponse<BankPayrollExportResultDto>>> PreviewBankExport(
        long id,
        [FromQuery] string? format,
        CancellationToken cancellationToken)
    {
        return Task.FromResult<ActionResult<ApiResponse<BankPayrollExportResultDto>>>(Conflict(AccountingHandoffRequired<BankPayrollExportResultDto>()));
    }

    private static ApiResponse<T> AccountingHandoffRequired<T>() => ApiResponse<T>.CreateFailure(
        "Payroll journals and payments are managed by Accounting. The independent handoff workflow must be enabled first.",
        new() { "HR_ACCOUNTING_HANDOFF_REQUIRED" }, StatusCodes.Status409Conflict);

    [HttpGet("run-lines/{lineId:long}/explanation")]
    [ProducesResponseType(typeof(ApiResponse<PayrollExplanationDto>), StatusCodes.Status200OK)]
    [HrPermission("hr-payroll", "view")]
    public async Task<ActionResult<ApiResponse<PayrollExplanationDto>>> GetCalculationExplanation(
        long lineId,
        CancellationToken cancellationToken)
    {
        if (_mediator != null)
            return FromResponse(await _mediator.Send(new GetCalculationExplanationQuery(lineId), cancellationToken));

        var result = await _explanationService!.GetExplanationAsync(lineId, cancellationToken);
        if (result == null) return NotFound(ApiResponse<PayrollExplanationDto>.CreateFailure("Calculation explanation snapshot not found.", statusCode: StatusCodes.Status404NotFound));
        return Ok(ApiResponse<PayrollExplanationDto>.CreateSuccess(result, "Calculation explanation retrieved successfully."));
    }

    [HttpGet("periods")]
    [ProducesResponseType(typeof(ApiResponse<IReadOnlyList<PayrollPeriod>>), StatusCodes.Status200OK)]
    [HrPermission("hr-payroll", "view")]
    public async Task<ActionResult<ApiResponse<IReadOnlyList<PayrollPeriod>>>> GetPeriods(
        [FromQuery] long companyId,
        [FromQuery] int? year,
        CancellationToken cancellationToken)
    {
        if (_mediator != null)
            return FromResponse(await _mediator.Send(new GetPeriodsQuery(companyId, year), cancellationToken));

        var result = await _payrollService!.GetPeriodsAsync(companyId, year, cancellationToken);
        return Ok(ApiResponse<IReadOnlyList<PayrollPeriod>>.CreateSuccess(result, "Payroll periods retrieved successfully."));
    }

    [HttpPost("periods")]
    [ProducesResponseType(typeof(ApiResponse<PayrollPeriod>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<PayrollPeriod>), StatusCodes.Status400BadRequest)]
    [HrPermission("hr-payroll", "create")]
    public async Task<ActionResult<ApiResponse<PayrollPeriod>>> CreatePeriod(
        [FromBody] CreatePayrollPeriodDto request,
        CancellationToken cancellationToken)
    {
        if (request == null)
            return BadRequest(ApiResponse<PayrollPeriod>.CreateFailure("Request body cannot be null", statusCode: 400));

        if (_mediator != null)
            return FromResponse(await _mediator.Send(new CreatePeriodCommand(ResolveTenantCompanyId(), request, CurrentUsername), cancellationToken));

        var payrollType = request.PayrollType.Trim().ToUpperInvariant();
        if (request.FiscalYear < 1 || request.Month is < 1 or > 12 || request.StartDate.Date > request.EndDate.Date ||
            payrollType is not ("MONTHLY" or "BIWEEKLY" or "SPECIAL"))
            return BadRequest(ApiResponse<PayrollPeriod>.CreateFailure("Fiscal year, month, date range or payroll type is invalid", statusCode: 400));

        var period = new PayrollPeriod
        {
            CompanyId = ResolveTenantCompanyId(),
            PeriodCode = request.PeriodCode.Trim(),
            NameAr = request.NameAr.Trim(),
            NameEn = request.NameEn.Trim(),
            FiscalYear = request.FiscalYear,
            Month = request.Month,
            StartDate = request.StartDate.Date,
            EndDate = request.EndDate.Date,
            PayDate = request.PayDate.Date,
            PayrollType = payrollType,
            Status = "OPEN",
            CreationUser = CurrentUsername,
            CreationDate = DateTime.UtcNow
        };
        var result = await _payrollService!.CreatePeriodAsync(period, cancellationToken);
        return Ok(ApiResponse<PayrollPeriod>.CreateSuccess(result, "Payroll period created successfully."));
    }

    private long ResolveTenantCompanyId()
    {
        if (HttpContext.Items.TryGetValue(TenantRequestContext.HttpContextItemKey, out var value) &&
            value is TenantRequestContext tenant && tenant.CompanyId > 0)
            return tenant.CompanyId;

        throw new InvalidOperationException("The trusted tenant context is required to create a payroll period.");
    }

    [HttpPost("periods/{periodCode}/close")]
    [ProducesResponseType(typeof(ApiResponse<PayrollPeriod>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<PayrollPeriod>), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ApiResponse<PayrollPeriod>), StatusCodes.Status404NotFound)]
    [HrPermission("hr-payroll", "edit")]
    public async Task<ActionResult<ApiResponse<PayrollPeriod>>> ClosePeriod(
        string periodCode,
        [FromBody] ClosePayrollPeriodRequest request,
        CancellationToken cancellationToken)
    {
        if (request == null || string.IsNullOrWhiteSpace(request.Reason))
            return BadRequest(ApiResponse<PayrollPeriod>.CreateFailure("A reason is required to close a payroll period.", statusCode: 400));

        if (_mediator != null)
            return FromResponse(await _mediator.Send(new ClosePeriodCommand(request.CompanyId, periodCode, request.Reason, CurrentUsername), cancellationToken));

        var user = CurrentUsername;
        var result = await _payrollService!.ClosePeriodAsync(request.CompanyId, periodCode, request.Reason, user, cancellationToken);
        return Ok(ApiResponse<PayrollPeriod>.CreateSuccess(result, $"Payroll period '{periodCode}' closed successfully."));
    }

    [HttpPost("periods/{periodCode}/reopen")]
    [ProducesResponseType(typeof(ApiResponse<PayrollPeriod>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<PayrollPeriod>), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ApiResponse<PayrollPeriod>), StatusCodes.Status404NotFound)]
    [HrPermission("hr-payroll", "reverse")]
    public async Task<ActionResult<ApiResponse<PayrollPeriod>>> ReopenPeriod(
        string periodCode,
        [FromBody] ReopenPayrollPeriodRequest request,
        CancellationToken cancellationToken)
    {
        if (request == null || string.IsNullOrWhiteSpace(request.Reason))
            return BadRequest(ApiResponse<PayrollPeriod>.CreateFailure("A reason is required to reopen a payroll period.", statusCode: 400));

        if (_mediator != null)
            return FromResponse(await _mediator.Send(new ReopenPeriodCommand(request.CompanyId, periodCode, request.Reason, CurrentUsername), cancellationToken));

        var user = CurrentUsername;
        var result = await _payrollService!.ReopenPeriodAsync(request.CompanyId, periodCode, request.Reason, user, cancellationToken);
        return Ok(ApiResponse<PayrollPeriod>.CreateSuccess(result, $"Payroll period '{periodCode}' reopened successfully."));
    }

    [HttpGet("{id:long}/comparison")]
    [ProducesResponseType(typeof(ApiResponse<PayrollRunComparisonDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<PayrollRunComparisonDto>), StatusCodes.Status404NotFound)]
    [HrPermission("hr-payroll", "view")]
    public async Task<ActionResult<ApiResponse<PayrollRunComparisonDto>>> ComparePayroll(
        long id,
        [FromQuery] long? baselineRunId,
        CancellationToken cancellationToken)
    {
        if (_mediator != null)
            return FromResponse(await _mediator.Send(new ComparePayrollRunQuery(id, baselineRunId), cancellationToken));

        var result = await _payrollService!.ComparePayrollRunAsync(id, baselineRunId, cancellationToken);
        return Ok(ApiResponse<PayrollRunComparisonDto>.CreateSuccess(result, "Payroll comparison generated successfully."));
    }
}
