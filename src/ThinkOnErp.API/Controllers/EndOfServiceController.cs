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
[Route("api/hr/eos")]
[ApiExplorerSettings(GroupName = ThinkOnErp.API.Swagger.ApiCategories.Hr)]
[TenantScoped]
[Authorize]
[Produces("application/json")]
public sealed class EndOfServiceController : ControllerBase
{
    private readonly IEndOfServiceService _eosService;

    public EndOfServiceController(IEndOfServiceService eosService)
    {
        _eosService = eosService ?? throw new ArgumentNullException(nameof(eosService));
    }

    [HttpGet("provisions/{employeeCode}")]
    [ProducesResponseType(typeof(List<EndOfServiceProvisionDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<List<EndOfServiceProvisionDto>>> GetEmployeeProvisions(string employeeCode)
    {
        var list = await _eosService.GetEmployeeProvisionsAsync(employeeCode);
        return Ok(list);
    }

    [HttpPost("provisions/run")]
    [ProducesResponseType(typeof(RunProvisionAccrualResultDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<RunProvisionAccrualResultDto>> RunMonthlyAccrual([FromQuery] string payPeriod)
    {
        var currentUser = User?.Identity?.Name ?? "API_USER";
        var result = await _eosService.RunMonthlyProvisionAccrualAsync(payPeriod, currentUser);
        return Ok(result);
    }

    [HttpGet("settlements")]
    [ProducesResponseType(typeof(List<FinalSettlementDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<List<FinalSettlementDto>>> GetAllSettlements([FromQuery] string? status = null)
    {
        var list = await _eosService.GetAllSettlementsAsync(status);
        return Ok(list);
    }

    [HttpGet("settlements/{id:long}")]
    [ProducesResponseType(typeof(FinalSettlementDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<FinalSettlementDto>> GetSettlementById(long id)
    {
        var settlement = await _eosService.GetSettlementByIdAsync(id);
        if (settlement == null)
        {
            return NotFound(new { message = $"Final settlement #{id} not found." });
        }
        return Ok(settlement);
    }

    [HttpPost("settlements/calculate")]
    [ProducesResponseType(typeof(FinalSettlementDto), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<FinalSettlementDto>> CalculateSettlement([FromBody] CalculateFinalSettlementDto dto)
    {
        var currentUser = User?.Identity?.Name ?? "API_USER";
        var created = await _eosService.CalculateFinalSettlementAsync(dto, currentUser);
        return CreatedAtAction(nameof(GetSettlementById), new { id = created.Id }, created);
    }

    [HttpPost("settlements/{id:long}/approve")]
    [ProducesResponseType(typeof(FinalSettlementDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<FinalSettlementDto>> ApproveSettlement(long id)
    {
        var currentUser = User?.Identity?.Name ?? "API_USER";
        var approved = await _eosService.ApproveFinalSettlementAsync(id, currentUser);
        return Ok(approved);
    }

    [HttpPost("settlements/{id:long}/post")]
    [ProducesResponseType(typeof(FinalSettlementDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<FinalSettlementDto>> PostSettlementToGl(long id)
    {
        var currentUser = User?.Identity?.Name ?? "API_USER";
        var posted = await _eosService.PostFinalSettlementToGlAsync(id, currentUser);
        return Ok(posted);
    }
}
