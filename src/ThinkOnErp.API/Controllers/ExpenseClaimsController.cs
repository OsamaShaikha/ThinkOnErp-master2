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
[Route("api/hr/expense-claims")]
[ApiExplorerSettings(GroupName = ThinkOnErp.API.Swagger.ApiCategories.Hr)]
[TenantScoped]
[Authorize]
[Produces("application/json")]
public sealed class ExpenseClaimsController : ControllerBase
{
    private readonly IExpenseClaimService _expenseService;

    public ExpenseClaimsController(IExpenseClaimService expenseService)
    {
        _expenseService = expenseService ?? throw new ArgumentNullException(nameof(expenseService));
    }

    [HttpGet]
    [ProducesResponseType(typeof(List<ExpenseClaimDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<List<ExpenseClaimDto>>> GetAllClaims(
        [FromQuery] string? employeeCode = null,
        [FromQuery] string? status = null,
        [FromQuery] DateTime? fromDate = null,
        [FromQuery] DateTime? toDate = null)
    {
        var list = await _expenseService.GetAllClaimsAsync(employeeCode, status, fromDate, toDate);
        return Ok(list);
    }

    [HttpGet("{id:long}")]
    [ProducesResponseType(typeof(ExpenseClaimDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<ExpenseClaimDto>> GetClaimById(long id)
    {
        var claim = await _expenseService.GetClaimByIdAsync(id);
        if (claim == null)
        {
            return NotFound(new { message = $"Expense claim #{id} not found." });
        }
        return Ok(claim);
    }

    [HttpPost]
    [ProducesResponseType(typeof(ExpenseClaimDto), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<ExpenseClaimDto>> SubmitClaim([FromBody] SubmitExpenseClaimDto dto)
    {
        var currentUser = User?.Identity?.Name ?? "API_USER";
        var created = await _expenseService.SubmitClaimAsync(dto, currentUser);
        return CreatedAtAction(nameof(GetClaimById), new { id = created.Id }, created);
    }

    [HttpPost("{id:long}/approve")]
    [ProducesResponseType(typeof(ExpenseClaimDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<ExpenseClaimDto>> ApproveClaim(long id)
    {
        var currentUser = User?.Identity?.Name ?? "API_USER";
        var approved = await _expenseService.ApproveClaimAsync(id, currentUser);
        return Ok(approved);
    }

    [HttpPost("{id:long}/reject")]
    [ProducesResponseType(typeof(ExpenseClaimDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<ExpenseClaimDto>> RejectClaim(long id, [FromBody] RejectClaimRequest request)
    {
        var currentUser = User?.Identity?.Name ?? "API_USER";
        var rejected = await _expenseService.RejectClaimAsync(id, request.RejectionReason, currentUser);
        return Ok(rejected);
    }
}

public sealed class RejectClaimRequest
{
    public string RejectionReason { get; set; } = string.Empty;
}
