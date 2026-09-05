using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using ThinkOnErp.API.Authorization;
using ThinkOnErp.Application.DTOs.Hr;
using ThinkOnErp.Application.Services.Hr;

namespace ThinkOnErp.API.Controllers;

[ApiController]
[Route("api/hr/loans")]
[ApiExplorerSettings(GroupName = ThinkOnErp.API.Swagger.ApiCategories.Hr)]
[TenantScoped]
[Authorize]
[Produces("application/json")]
public sealed class LoansController : ControllerBase
{
    private readonly ILoanDeductionService _service;

    public LoansController(ILoanDeductionService service)
    {
        _service = service ?? throw new ArgumentNullException(nameof(service));
    }

    [HttpGet]
    [ProducesResponseType(typeof(List<EmployeeLoanDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<List<EmployeeLoanDto>>> GetLoans([FromQuery] string? employeeCode = null, [FromQuery] string? status = null)
    {
        var result = await _service.GetLoansAsync(employeeCode, status);
        return Ok(result);
    }

    [HttpPost]
    [ProducesResponseType(typeof(EmployeeLoanDto), StatusCodes.Status201Created)]
    public async Task<ActionResult<EmployeeLoanDto>> CreateLoan([FromBody] CreateEmployeeLoanDto dto)
    {
        var currentUser = User?.Identity?.Name ?? "API_USER";
        var created = await _service.CreateLoanAsync(dto, currentUser);
        return Created("", created);
    }

    [HttpPost("{id:long}/approve")]
    [ProducesResponseType(typeof(EmployeeLoanDto), StatusCodes.Status200OK)]
    public async Task<ActionResult<EmployeeLoanDto>> ApproveLoan(long id)
    {
        var currentUser = User?.Identity?.Name ?? "API_USER";
        var result = await _service.ApproveLoanAsync(id, currentUser);
        return Ok(result);
    }

    [HttpGet("advances")]
    [ProducesResponseType(typeof(List<EmployeeAdvanceDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<List<EmployeeAdvanceDto>>> GetAdvances(
        [FromQuery] string? employeeCode = null,
        [FromQuery] string? payPeriod = null,
        [FromQuery] string? status = null)
    {
        var result = await _service.GetAdvancesAsync(employeeCode, payPeriod, status);
        return Ok(result);
    }

    [HttpPost("advances")]
    [ProducesResponseType(typeof(EmployeeAdvanceDto), StatusCodes.Status201Created)]
    public async Task<ActionResult<EmployeeAdvanceDto>> CreateAdvance([FromBody] CreateEmployeeAdvanceDto dto)
    {
        var currentUser = User?.Identity?.Name ?? "API_USER";
        var created = await _service.CreateAdvanceAsync(dto, currentUser);
        return Created("", created);
    }

    [HttpPost("advances/{id:long}/approve")]
    [ProducesResponseType(typeof(EmployeeAdvanceDto), StatusCodes.Status200OK)]
    public async Task<ActionResult<EmployeeAdvanceDto>> ApproveAdvance(long id)
    {
        var currentUser = User?.Identity?.Name ?? "API_USER";
        var result = await _service.ApproveAdvanceAsync(id, currentUser);
        return Ok(result);
    }
}
