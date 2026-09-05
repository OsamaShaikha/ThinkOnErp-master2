using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using ThinkOnErp.API.Authorization;
using ThinkOnErp.Application.DTOs.Hr;
using ThinkOnErp.Application.Services.Hr;
using ThinkOnErp.Domain.Entities.Hr;
using ThinkOnErp.Domain.Interfaces.Hr;

namespace ThinkOnErp.API.Controllers;

[ApiController]
[Route("api/hr/payroll")]
[ApiExplorerSettings(GroupName = ThinkOnErp.API.Swagger.ApiCategories.Hr)]
[TenantScoped]
[Authorize]
[Produces("application/json")]
public sealed class PayrollController : ControllerBase
{
    private readonly IPayrollService _payrollService;
    private readonly IPayrollExplanationService _explanationService;
    private readonly IPayrollPeriodRepository _periodRepo;

    public PayrollController(
        IPayrollService payrollService,
        IPayrollExplanationService explanationService,
        IPayrollPeriodRepository periodRepo)
    {
        _payrollService = payrollService ?? throw new ArgumentNullException(nameof(payrollService));
        _explanationService = explanationService ?? throw new ArgumentNullException(nameof(explanationService));
        _periodRepo = periodRepo ?? throw new ArgumentNullException(nameof(periodRepo));
    }

    [HttpGet("periods")]
    [ProducesResponseType(typeof(List<PayrollPeriodDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<List<PayrollPeriodDto>>> GetPeriods([FromQuery] long companyId = 1, [FromQuery] int? fiscalYear = null)
    {
        var periods = await _periodRepo.GetAllAsync(companyId, fiscalYear);
        var dtos = periods.Select(p => new PayrollPeriodDto
        {
            Id = p.Id,
            CompanyId = p.CompanyId,
            PeriodCode = p.PeriodCode,
            NameEn = p.NameEn,
            NameAr = p.NameAr,
            FiscalYear = p.FiscalYear,
            Month = p.Month,
            StartDate = p.StartDate,
            EndDate = p.EndDate,
            PayDate = p.PayDate,
            PayrollType = p.PayrollType,
            Status = p.Status
        }).ToList();

        return Ok(dtos);
    }

    [HttpPost("periods")]
    [ProducesResponseType(typeof(PayrollPeriodDto), StatusCodes.Status201Created)]
    public async Task<ActionResult<PayrollPeriodDto>> CreatePeriod([FromBody] CreatePayrollPeriodDto dto)
    {
        var currentUser = User?.Identity?.Name ?? "API_USER";
        var exists = await _periodRepo.ExistsAsync(dto.CompanyId, dto.FiscalYear, dto.Month, dto.PayrollType);
        if (exists)
        {
            return Conflict(new { message = $"Payroll period for {dto.FiscalYear}-{dto.Month:D2} ({dto.PayrollType}) already exists." });
        }

        var period = new PayrollPeriod
        {
            CompanyId = dto.CompanyId,
            PeriodCode = dto.PeriodCode.Trim(),
            NameEn = dto.NameEn.Trim(),
            NameAr = dto.NameAr.Trim(),
            FiscalYear = dto.FiscalYear,
            Month = dto.Month,
            StartDate = dto.StartDate.Date,
            EndDate = dto.EndDate.Date,
            PayDate = dto.PayDate.Date,
            PayrollType = dto.PayrollType.ToUpperInvariant(),
            Status = "OPEN",
            CreationUser = currentUser,
            CreationDate = DateTime.UtcNow
        };

        await _periodRepo.AddAsync(period);
        await _periodRepo.SaveChangesAsync();

        return Created("", new PayrollPeriodDto
        {
            Id = period.Id,
            CompanyId = period.CompanyId,
            PeriodCode = period.PeriodCode,
            NameEn = period.NameEn,
            NameAr = period.NameAr,
            FiscalYear = period.FiscalYear,
            Month = period.Month,
            StartDate = period.StartDate,
            EndDate = period.EndDate,
            PayDate = period.PayDate,
            PayrollType = period.PayrollType,
            Status = period.Status
        });
    }

    [HttpGet("runs")]
    [ProducesResponseType(typeof(List<PayrollRunDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<List<PayrollRunDto>>> GetAllRuns(
        [FromQuery] string? payPeriod = null,
        [FromQuery] long? branchId = null,
        [FromQuery] string? status = null)
    {
        var runs = await _payrollService.GetAllRunsAsync(payPeriod, branchId, status);
        return Ok(runs);
    }

    [HttpGet("runs/{id:long}")]
    [ProducesResponseType(typeof(PayrollRunDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<PayrollRunDto>> GetRunById(long id)
    {
        var run = await _payrollService.GetRunByIdAsync(id);
        if (run == null)
        {
            return NotFound(new { message = $"Payroll run #{id} not found." });
        }
        return Ok(run);
    }

    [HttpPost("runs")]
    [ProducesResponseType(typeof(PayrollRunDto), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<PayrollRunDto>> CreateRun([FromBody] CreatePayrollRunDto dto)
    {
        var currentUser = User?.Identity?.Name ?? "API_USER";
        var created = await _payrollService.CreateRunAsync(dto, currentUser);
        return CreatedAtAction(nameof(GetRunById), new { id = created.Id }, created);
    }

    [HttpPost("runs/{id:long}/calculate")]
    [ProducesResponseType(typeof(PayrollRunDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<PayrollRunDto>> CalculateRun(long id)
    {
        var currentUser = User?.Identity?.Name ?? "API_USER";
        var result = await _payrollService.CalculateRunAsync(id, currentUser);
        return Ok(result);
    }

    [HttpPost("runs/{id:long}/validate")]
    [ProducesResponseType(typeof(PayrollValidationResultDto), StatusCodes.Status200OK)]
    public async Task<ActionResult<PayrollValidationResultDto>> ValidateRun(long id)
    {
        var result = await _payrollService.ValidateRunAsync(id);
        return Ok(result);
    }

    [HttpPost("runs/{id:long}/submit-for-approval")]
    [ProducesResponseType(typeof(PayrollRunDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<PayrollRunDto>> SubmitForApproval(long id)
    {
        var currentUser = User?.Identity?.Name ?? "API_USER";
        var result = await _payrollService.SubmitForApprovalAsync(id, currentUser);
        return Ok(result);
    }

    [HttpPost("runs/{id:long}/approve")]
    [ProducesResponseType(typeof(PayrollRunDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<PayrollRunDto>> ApproveRun(long id)
    {
        var currentUser = User?.Identity?.Name ?? "API_USER";
        var result = await _payrollService.ApproveRunAsync(id, currentUser);
        return Ok(result);
    }

    [HttpPost("runs/{id:long}/post")]
    [ProducesResponseType(typeof(PayrollRunDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<PayrollRunDto>> PostRunToGl(long id)
    {
        var currentUser = User?.Identity?.Name ?? "API_USER";
        var result = await _payrollService.PostRunToGlAsync(id, currentUser);
        return Ok(result);
    }

    [HttpPost("runs/{id:long}/lock")]
    [ProducesResponseType(typeof(PayrollRunDto), StatusCodes.Status200OK)]
    public async Task<ActionResult<PayrollRunDto>> LockRun(long id)
    {
        var currentUser = User?.Identity?.Name ?? "API_USER";
        var result = await _payrollService.LockRunAsync(id, currentUser);
        return Ok(result);
    }

    [HttpPost("runs/{id:long}/reverse")]
    [ProducesResponseType(typeof(PayrollRunDto), StatusCodes.Status200OK)]
    public async Task<ActionResult<PayrollRunDto>> ReverseRun(long id, [FromQuery] string reason)
    {
        var currentUser = User?.Identity?.Name ?? "API_USER";
        var result = await _payrollService.ReverseRunAsync(id, reason, currentUser);
        return Ok(result);
    }

    [HttpPost("runs/{id:long}/disburse")]
    [ProducesResponseType(typeof(PayrollRunDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<PayrollRunDto>> DisbursePayroll(long id)
    {
        var currentUser = User?.Identity?.Name ?? "API_USER";
        var result = await _payrollService.DisbursePayrollAsync(id, currentUser);
        return Ok(result);
    }

    [HttpGet("explain/{employeeCode}/{payPeriod}")]
    [ProducesResponseType(typeof(PayrollExplanationDto), StatusCodes.Status200OK)]
    public async Task<ActionResult<PayrollExplanationDto>> ExplainPayroll(string employeeCode, string payPeriod)
    {
        var result = await _explanationService.GetPayrollExplanationAsync(employeeCode, payPeriod);
        return Ok(result);
    }

    [HttpGet("payslips/{employeeCode}")]
    [ProducesResponseType(typeof(List<PayslipDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<List<PayslipDto>>> GetEmployeePayslips(
        string employeeCode,
        [FromQuery] string? payPeriod = null)
    {
        var result = await _payrollService.GetEmployeePayslipsAsync(employeeCode, payPeriod);
        return Ok(result);
    }

    [HttpGet("payslips/{employeeCode}/{payPeriod}")]
    [ProducesResponseType(typeof(PayslipDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<PayslipDto>> GetEmployeePayslipForPeriod(string employeeCode, string payPeriod)
    {
        var result = await _payrollService.GetEmployeePayslipForPeriodAsync(employeeCode, payPeriod);
        if (result == null)
        {
            return NotFound(new { message = $"Payslip for employee {employeeCode} in period {payPeriod} not found." });
        }
        return Ok(result);
    }
}
