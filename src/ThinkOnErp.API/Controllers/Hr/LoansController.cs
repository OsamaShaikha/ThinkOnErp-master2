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

    public LoansController(
        ILoanDeductionService loanService,
        ILogger<LoansController> logger)
    {
        _loanService = loanService;
        _logger = logger;
    }

    [HttpPost]
    [ProducesResponseType(typeof(ApiResponse<EmployeeLoan>), StatusCodes.Status200OK)]
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
    public async Task<ActionResult<ApiResponse<EmployeeLoan>>> ApproveLoan(
        long id,
        CancellationToken cancellationToken)
    {
        var user = User.Identity?.Name ?? "SYSTEM";
        var result = await _loanService.ApproveLoanAsync(id, user, cancellationToken);
        return Ok(ApiResponse<EmployeeLoan>.CreateSuccess(result, "Loan approved successfully."));
    }

    [HttpGet]
    [ProducesResponseType(typeof(ApiResponse<IReadOnlyList<EmployeeLoan>>), StatusCodes.Status200OK)]
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
    public async Task<ActionResult<ApiResponse<EmployeeAdvance>>> ApproveAdvance(
        long id,
        CancellationToken cancellationToken)
    {
        var user = User.Identity?.Name ?? "SYSTEM";
        var result = await _loanService.ApproveAdvanceAsync(id, user, cancellationToken);
        return Ok(ApiResponse<EmployeeAdvance>.CreateSuccess(result, "Advance request approved successfully."));
    }

    [HttpGet("advances")]
    [ProducesResponseType(typeof(ApiResponse<IReadOnlyList<EmployeeAdvance>>), StatusCodes.Status200OK)]
    public async Task<ActionResult<ApiResponse<IReadOnlyList<EmployeeAdvance>>>> GetAdvances(
        [FromQuery] string? employeeCode,
        [FromQuery] string? targetPayPeriod,
        CancellationToken cancellationToken)
    {
        var result = await _loanService.GetAdvancesAsync(employeeCode, targetPayPeriod, cancellationToken);
        return Ok(ApiResponse<IReadOnlyList<EmployeeAdvance>>.CreateSuccess(result, "Advances retrieved successfully."));
    }
}
