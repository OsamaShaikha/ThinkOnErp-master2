using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using ThinkOnErp.API.Authorization;
using ThinkOnErp.Application.Common;
using ThinkOnErp.Application.DTOs.Hr;
using ThinkOnErp.Application.Services.Hr;

namespace ThinkOnErp.API.Controllers;

[ApiController]
[Route("api/hr")]
[ApiExplorerSettings(GroupName = ThinkOnErp.API.Swagger.ApiCategories.Hr)]
[TenantScoped]
[Authorize]
[Produces("application/json")]
public sealed class CompensationController : ControllerBase
{
    private readonly ICompensationService _compensationService;

    public CompensationController(ICompensationService compensationService)
    {
        _compensationService = compensationService ?? throw new ArgumentNullException(nameof(compensationService));
    }

    [HttpGet("salary-components")]
    [ProducesResponseType(typeof(List<SalaryComponentDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<List<SalaryComponentDto>>> GetAllSalaryComponents([FromQuery] bool activeOnly = true)
    {
        var list = await _compensationService.GetAllComponentsAsync(activeOnly);
        return Ok(list);
    }

    [HttpGet("salary-components/{code}")]
    [ProducesResponseType(typeof(SalaryComponentDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<SalaryComponentDto>> GetComponentByCode(string code)
    {
        var comp = await _compensationService.GetComponentByCodeAsync(code);
        if (comp == null)
        {
            return NotFound(new { message = $"Salary component '{code}' not found." });
        }
        return Ok(comp);
    }

    [HttpPost("salary-components")]
    [ProducesResponseType(typeof(SalaryComponentDto), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<SalaryComponentDto>> CreateSalaryComponent([FromBody] CreateSalaryComponentDto dto)
    {
        var currentUser = User?.Identity?.Name ?? "API_USER";
        var created = await _compensationService.CreateComponentAsync(dto, currentUser);
        return CreatedAtAction(nameof(GetComponentByCode), new { code = created.ComponentCode }, created);
    }

    [HttpGet("employees/{employeeCode}/salary-structure")]
    [ProducesResponseType(typeof(EmployeeSalaryStructureDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<EmployeeSalaryStructureDto>> GetActiveSalaryStructure(
        string employeeCode,
        [FromQuery] DateTime? effectiveDate = null)
    {
        var structure = await _compensationService.GetActiveStructureAsync(employeeCode, effectiveDate);
        if (structure == null)
        {
            return NotFound(new { message = $"Active salary structure for employee '{employeeCode}' not found." });
        }
        return Ok(structure);
    }

    [HttpGet("employees/{employeeCode}/salary-structure/history")]
    [ProducesResponseType(typeof(List<EmployeeSalaryStructureDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<List<EmployeeSalaryStructureDto>>> GetSalaryStructureHistory(string employeeCode)
    {
        var list = await _compensationService.GetStructureHistoryAsync(employeeCode);
        return Ok(list);
    }

    [HttpPost("employees/{employeeCode}/salary-structure")]
    [ProducesResponseType(typeof(EmployeeSalaryStructureDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<EmployeeSalaryStructureDto>> SetSalaryStructure(
        string employeeCode,
        [FromBody] SetEmployeeSalaryStructureDto dto)
    {
        var currentUser = User?.Identity?.Name ?? "API_USER";
        var result = await _compensationService.SetStructureAsync(employeeCode, dto, currentUser);
        return Ok(result);
    }

    [HttpGet("employees/{employeeCode}/salary-revisions")]
    [ProducesResponseType(typeof(List<SalaryRevisionDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<List<SalaryRevisionDto>>> GetSalaryRevisions(string employeeCode)
    {
        var list = await _compensationService.GetSalaryRevisionsAsync(employeeCode);
        return Ok(list);
    }

    [HttpPost("employees/{employeeCode}/salary-revisions")]
    [ProducesResponseType(typeof(SalaryRevisionDto), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<SalaryRevisionDto>> CreateSalaryRevision(
        string employeeCode,
        [FromBody] CreateSalaryRevisionDto dto)
    {
        var currentUser = User?.Identity?.Name ?? "API_USER";
        var created = await _compensationService.CreateRevisionAsync(employeeCode, dto, currentUser);
        return Created(string.Empty, created);
    }

    [HttpGet("employees/{employeeCode}/contracts")]
    [ProducesResponseType(typeof(List<EmploymentContractDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<List<EmploymentContractDto>>> GetContracts(string employeeCode)
    {
        var list = await _compensationService.GetContractsAsync(employeeCode);
        return Ok(list);
    }

    [HttpGet("employees/{employeeCode}/contracts/active")]
    [ProducesResponseType(typeof(EmploymentContractDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<EmploymentContractDto>> GetActiveContract(string employeeCode)
    {
        var contract = await _compensationService.GetActiveContractAsync(employeeCode);
        if (contract == null)
        {
            return NotFound(new { message = $"Active employment contract for employee '{employeeCode}' not found." });
        }
        return Ok(contract);
    }

    [HttpPost("employees/{employeeCode}/contracts")]
    [ProducesResponseType(typeof(EmploymentContractDto), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<EmploymentContractDto>> CreateContract(
        string employeeCode,
        [FromBody] CreateEmploymentContractDto dto)
    {
        var currentUser = User?.Identity?.Name ?? "API_USER";
        var created = await _compensationService.CreateContractAsync(employeeCode, dto, currentUser);
        return Created(string.Empty, created);
    }
}
