using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;
using ThinkOnErp.API.Authorization;
using ThinkOnErp.Application.Common;
using ThinkOnErp.Application.DTOs.Accounting.OpeningBalances;
using ThinkOnErp.Application.Services.Accounting;
using ThinkOnErp.Domain.Constants;

namespace ThinkOnErp.API.Controllers;

/// <summary>
/// Manages Opening Balance entries for a company branch.
/// A new company entering the system enters their account starting balances here.
/// Once confirmed, the system auto-generates a posted OB journal voucher (TypeCode=302).
/// </summary>
[ApiController]
[Route("api/accounting/opening-balances")]
[ApiExplorerSettings(GroupName = ThinkOnErp.API.Swagger.ApiCategories.Accounting)]
[TenantScoped]
[Authorize]
public sealed class OpeningBalancesController : ControllerBase
{
    private readonly IOpeningBalanceService _service;
    private readonly ILogger<OpeningBalancesController> _logger;

    public OpeningBalancesController(
        IOpeningBalanceService service,
        ILogger<OpeningBalancesController> logger)
    {
        _service = service ?? throw new ArgumentNullException(nameof(service));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <summary>
    /// Retrieves the Opening Balance for a specific branch and fiscal year.
    /// </summary>
    [HttpGet]
    [ProducesResponseType(typeof(ApiResponse<OpeningBalanceHeaderDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<ApiResponse<OpeningBalanceHeaderDto>>> GetByFiscalYear(
        [FromQuery] long branchId,
        [FromQuery] long fiscalYearId,
        CancellationToken cancellationToken)
    {
        var result = await _service.GetByFiscalYearAsync(branchId, fiscalYearId, cancellationToken);
        if (result == null)
            return NotFound(ApiResponse<object>.CreateFailure(
                ErrorCodes.OpeningBalanceNotFound,
                statusCode: 404));

        return Ok(ApiResponse<OpeningBalanceHeaderDto>.CreateSuccess(
            result, ResponseCodes.OpeningBalanceRetrieved));
    }

    /// <summary>
    /// Returns all Opening Balance records for a given branch (summary list).
    /// </summary>
    [HttpGet("branch/{branchId:long}")]
    [ProducesResponseType(typeof(ApiResponse<IReadOnlyList<OpeningBalanceHeaderDto>>), StatusCodes.Status200OK)]
    public async Task<ActionResult<ApiResponse<IReadOnlyList<OpeningBalanceHeaderDto>>>> GetAllByBranch(
        long branchId,
        CancellationToken cancellationToken)
    {
        var results = await _service.GetAllByBranchAsync(branchId, cancellationToken);
        return Ok(ApiResponse<IReadOnlyList<OpeningBalanceHeaderDto>>.CreateSuccess(
            results, ResponseCodes.OpeningBalanceRetrieved));
    }

    /// <summary>
    /// Retrieves a single Opening Balance entry by ID.
    /// </summary>
    [HttpGet("{id:long}")]
    [ProducesResponseType(typeof(ApiResponse<OpeningBalanceHeaderDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<ApiResponse<OpeningBalanceHeaderDto>>> GetById(
        long id,
        CancellationToken cancellationToken)
    {
        var result = await _service.GetByIdAsync(id, cancellationToken);
        if (result == null)
            return NotFound(ApiResponse<object>.CreateFailure(
                ErrorCodes.OpeningBalanceNotFound, statusCode: 404));

        return Ok(ApiResponse<OpeningBalanceHeaderDto>.CreateSuccess(
            result, ResponseCodes.OpeningBalanceRetrieved));
    }

    /// <summary>
    /// Creates a new Opening Balance entry in Draft status for a branch + fiscal year.
    /// Only ONE Opening Balance is allowed per branch per fiscal year.
    /// </summary>
    [HttpPost]
    [ProducesResponseType(typeof(ApiResponse<OpeningBalanceHeaderDto>), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<ApiResponse<OpeningBalanceHeaderDto>>> Create(
        [FromBody] CreateOpeningBalanceDto dto,
        CancellationToken cancellationToken)
    {
        var username = User.FindFirstValue(ClaimTypes.Name) ?? User.Identity?.Name ?? "API_USER";
        var result = await _service.CreateAsync(dto, username, cancellationToken);

        return CreatedAtAction(
            nameof(GetById),
            new { id = result.Id },
            ApiResponse<OpeningBalanceHeaderDto>.CreateSuccess(
                result, ResponseCodes.OpeningBalanceCreated, 201));
    }

    /// <summary>
    /// Adds a new account line to a Draft Opening Balance.
    /// </summary>
    [HttpPost("{id:long}/lines")]
    [ProducesResponseType(typeof(ApiResponse<OpeningBalanceHeaderDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<ApiResponse<OpeningBalanceHeaderDto>>> AddLine(
        long id,
        [FromBody] OpeningBalanceLineDto line,
        CancellationToken cancellationToken)
    {
        var username = User.FindFirstValue(ClaimTypes.Name) ?? User.Identity?.Name ?? "API_USER";
        var result = await _service.AddLineAsync(id, line, username, cancellationToken);
        return Ok(ApiResponse<OpeningBalanceHeaderDto>.CreateSuccess(
            result, ResponseCodes.RecordCreated));
    }

    /// <summary>
    /// Updates an existing account line in a Draft Opening Balance.
    /// </summary>
    [HttpPut("{id:long}/lines/{lineId:long}")]
    [ProducesResponseType(typeof(ApiResponse<OpeningBalanceHeaderDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<ApiResponse<OpeningBalanceHeaderDto>>> UpdateLine(
        long id,
        long lineId,
        [FromBody] OpeningBalanceLineDto line,
        CancellationToken cancellationToken)
    {
        var username = User.FindFirstValue(ClaimTypes.Name) ?? User.Identity?.Name ?? "API_USER";
        var result = await _service.UpdateLineAsync(id, lineId, line, username, cancellationToken);
        return Ok(ApiResponse<OpeningBalanceHeaderDto>.CreateSuccess(
            result, ResponseCodes.RecordUpdated));
    }

    /// <summary>
    /// Removes an account line from a Draft Opening Balance.
    /// </summary>
    [HttpDelete("{id:long}/lines/{lineId:long}")]
    [ProducesResponseType(typeof(ApiResponse<OpeningBalanceHeaderDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<ApiResponse<OpeningBalanceHeaderDto>>> DeleteLine(
        long id,
        long lineId,
        CancellationToken cancellationToken)
    {
        var username = User.FindFirstValue(ClaimTypes.Name) ?? User.Identity?.Name ?? "API_USER";
        var result = await _service.DeleteLineAsync(id, lineId, username, cancellationToken);
        return Ok(ApiResponse<OpeningBalanceHeaderDto>.CreateSuccess(
            result, ResponseCodes.RecordDeleted));
    }

    /// <summary>
    /// Confirms the Opening Balance — locks it permanently and auto-generates
    /// a posted OB journal voucher (TypeCode=302) in GL_VOUCHER_HEADER/DETAIL.
    /// This action CANNOT be undone.
    /// </summary>
    [HttpPost("{id:long}/confirm")]
    [ProducesResponseType(typeof(ApiResponse<OpeningBalanceHeaderDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<ApiResponse<OpeningBalanceHeaderDto>>> Confirm(
        long id,
        CancellationToken cancellationToken)
    {
        var username = User.FindFirstValue(ClaimTypes.Name) ?? User.Identity?.Name ?? "API_USER";
        var result = await _service.ConfirmAsync(id, username, cancellationToken);
        return Ok(ApiResponse<OpeningBalanceHeaderDto>.CreateSuccess(
            result, ResponseCodes.OpeningBalanceConfirmed));
    }
}
