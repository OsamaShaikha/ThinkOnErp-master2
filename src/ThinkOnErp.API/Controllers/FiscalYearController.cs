using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ThinkOnErp.API.Authorization;
using ThinkOnErp.Application.Common;
using ThinkOnErp.Application.DTOs.FiscalYear;
using ThinkOnErp.Application.Features.FiscalYears.Commands.CreateFiscalYear;
using ThinkOnErp.Application.Features.FiscalYears.Commands.UpdateFiscalYear;
using ThinkOnErp.Application.Features.FiscalYears.Commands.DeleteFiscalYear;
using ThinkOnErp.Application.Features.FiscalYears.Commands.CloseFiscalYear;
using ThinkOnErp.Application.Features.FiscalYears.Queries.GetAllFiscalYears;
using ThinkOnErp.Application.Features.FiscalYears.Queries.GetFiscalYearById;
using ThinkOnErp.Application.Features.FiscalYears.Queries.GetFiscalYearsByBranch;
using ThinkOnErp.Domain.Constants;

namespace ThinkOnErp.API.Controllers;

/// <summary>
/// Controller for fiscal year management operations.
/// Handles CRUD operations for fiscal years with appropriate authorization.
/// </summary>
[ApiController]
[Route("api/fiscalyears")]
[TenantScoped]
[Authorize]
public class FiscalYearController : ControllerBase
{
    private readonly IMediator _mediator;
    private readonly ILogger<FiscalYearController> _logger;

    public FiscalYearController(IMediator mediator, ILogger<FiscalYearController> logger)
    {
        _mediator = mediator ?? throw new ArgumentNullException(nameof(mediator));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <summary>
    /// Retrieves all active fiscal years from the system.
    /// </summary>
    [HttpGet]
    [ProducesResponseType(typeof(ApiResponse<List<FiscalYearDto>>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<List<FiscalYearDto>>), StatusCodes.Status401Unauthorized)]
    public async Task<ActionResult<ApiResponse<List<FiscalYearDto>>>> GetAllFiscalYears()
    {
        try
        {
            _logger.LogInformation("Retrieving all fiscal years");

            var query = new GetAllFiscalYearsQuery();
            var fiscalYears = await _mediator.Send(query);

            _logger.LogInformation("Retrieved {Count} fiscal years", fiscalYears.Count);

            return Ok(ApiResponse<List<FiscalYearDto>>.CreateSuccess(
                fiscalYears,
                ResponseCodes.FiscalYearsRetrieved,
                200));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving all fiscal years");
            throw;
        }
    }

    /// <summary>
    /// Retrieves a specific fiscal year by its ID.
    /// </summary>
    [HttpGet("{id}")]
    [ProducesResponseType(typeof(ApiResponse<FiscalYearDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<FiscalYearDto>), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ApiResponse<FiscalYearDto>), StatusCodes.Status401Unauthorized)]
    public async Task<ActionResult<ApiResponse<FiscalYearDto>>> GetFiscalYearById(Int64 id)
    {
        try
        {
            _logger.LogInformation("Retrieving fiscal year with ID: {FiscalYearId}", id);

            var query = new GetFiscalYearByIdQuery { FiscalYearId = id };
            var fiscalYear = await _mediator.Send(query);

            if (fiscalYear == null)
            {
                _logger.LogWarning("Fiscal year not found with ID: {FiscalYearId}", id);
                return NotFound(ApiResponse<FiscalYearDto>.CreateFailure(
                    ErrorCodes.FiscalYearNotFound,
                    statusCode: 404));
            }

            _logger.LogInformation("Retrieved fiscal year with ID: {FiscalYearId}", id);

            return Ok(ApiResponse<FiscalYearDto>.CreateSuccess(
                fiscalYear,
                ResponseCodes.DataRetrieved,
                200));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving fiscal year with ID: {FiscalYearId}", id);
            throw;
        }
    }

    /// <summary>
    /// Retrieves all fiscal years for a specific branch.
    /// </summary>
    [HttpGet("branch/{branchId}")]
    [ProducesResponseType(typeof(ApiResponse<List<FiscalYearDto>>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<List<FiscalYearDto>>), StatusCodes.Status401Unauthorized)]
    public async Task<ActionResult<ApiResponse<List<FiscalYearDto>>>> GetFiscalYearsByBranch(Int64 branchId)
    {
        try
        {
            _logger.LogInformation("Retrieving fiscal years for branch ID: {BranchId}", branchId);

            var query = new GetFiscalYearsByBranchQuery { BranchId = branchId };
            var fiscalYears = await _mediator.Send(query);

            _logger.LogInformation("Retrieved {Count} fiscal years for branch ID: {BranchId}", fiscalYears.Count, branchId);

            return Ok(ApiResponse<List<FiscalYearDto>>.CreateSuccess(
                fiscalYears,
                ResponseCodes.FiscalYearsRetrieved,
                200));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving fiscal years for branch ID: {BranchId}", branchId);
            throw;
        }
    }

    /// <summary>
    /// Creates a new fiscal year in the system.
    /// </summary>
    [HttpPost]
    [Authorize(Policy = "AdminOnly")]
    [ProducesResponseType(typeof(ApiResponse<Int64>), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ApiResponse<Int64>), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ApiResponse<Int64>), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ApiResponse<Int64>), StatusCodes.Status403Forbidden)]
    public async Task<ActionResult<ApiResponse<Int64>>> CreateFiscalYear([FromBody] CreateFiscalYearCommand command)
    {
        try
        {
            command.CreationUser = User.Identity?.Name ?? "system";
            _logger.LogInformation("Creating new fiscal year: {FiscalYearCode}", command.FiscalYearCode);

            var fiscalYearId = await _mediator.Send(command);

            _logger.LogInformation("Fiscal year created successfully with ID: {FiscalYearId}", fiscalYearId);

            return CreatedAtAction(
                nameof(GetFiscalYearById),
                new { id = fiscalYearId },
                ApiResponse<Int64>.CreateSuccess(
                    fiscalYearId,
                    ResponseCodes.FiscalYearCreated,
                    201));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error creating fiscal year: {FiscalYearCode}", command.FiscalYearCode);
            throw;
        }
    }

    /// <summary>
    /// Updates an existing fiscal year in the system.
    /// </summary>
    [HttpPut("{id}")]
    [Authorize(Policy = "AdminOnly")]
    [ProducesResponseType(typeof(ApiResponse<Int64>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<Int64>), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ApiResponse<Int64>), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ApiResponse<Int64>), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ApiResponse<Int64>), StatusCodes.Status403Forbidden)]
    public async Task<ActionResult<ApiResponse<Int64>>> UpdateFiscalYear(Int64 id, [FromBody] UpdateFiscalYearCommand command)
    {
        try
        {
            command.FiscalYearId = id;
            command.UpdateUser = User.Identity?.Name ?? "system";

            _logger.LogInformation("Updating fiscal year with ID: {FiscalYearId}", id);

            var rowsAffected = await _mediator.Send(command);

            if (rowsAffected == 0)
            {
                _logger.LogWarning("Fiscal year not found for update with ID: {FiscalYearId}", id);
                return NotFound(ApiResponse<Int64>.CreateFailure(
                    ErrorCodes.FiscalYearNotFound,
                    statusCode: 404));
            }

            _logger.LogInformation("Fiscal year updated successfully with ID: {FiscalYearId}", id);

            return Ok(ApiResponse<Int64>.CreateSuccess(
                rowsAffected,
                ResponseCodes.FiscalYearUpdated,
                200));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error updating fiscal year with ID: {FiscalYearId}", id);
            throw;
        }
    }

    /// <summary>
    /// Deletes (soft delete) a fiscal year from the system.
    /// </summary>
    [HttpDelete("{id}")]
    [Authorize(Policy = "AdminOnly")]
    [ProducesResponseType(typeof(ApiResponse<Int64>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<Int64>), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ApiResponse<Int64>), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ApiResponse<Int64>), StatusCodes.Status403Forbidden)]
    public async Task<ActionResult<ApiResponse<Int64>>> DeleteFiscalYear(Int64 id)
    {
        try
        {
            _logger.LogInformation("Deleting fiscal year with ID: {FiscalYearId}", id);

            var command = new DeleteFiscalYearCommand { FiscalYearId = id };
            var rowsAffected = await _mediator.Send(command);

            if (rowsAffected == 0)
            {
                _logger.LogWarning("Fiscal year not found for deletion with ID: {FiscalYearId}", id);
                return NotFound(ApiResponse<Int64>.CreateFailure(
                    ErrorCodes.FiscalYearNotFound,
                    statusCode: 404));
            }

            _logger.LogInformation("Fiscal year deleted successfully with ID: {FiscalYearId}", id);

            return Ok(ApiResponse<Int64>.CreateSuccess(
                rowsAffected,
                ResponseCodes.FiscalYearDeleted,
                200));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error deleting fiscal year with ID: {FiscalYearId}", id);
            throw;
        }
    }

    /// <summary>
    /// Closes a fiscal year, preventing further modifications.
    /// </summary>
    [HttpPost("{id}/close")]
    [Authorize(Policy = "AdminOnly")]
    [ProducesResponseType(typeof(ApiResponse<Int64>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<Int64>), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ApiResponse<Int64>), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ApiResponse<Int64>), StatusCodes.Status403Forbidden)]
    public async Task<ActionResult<ApiResponse<Int64>>> CloseFiscalYear(Int64 id, [FromBody] CloseFiscalYearDto? dto)
    {
        try
        {
            _logger.LogInformation("Closing fiscal year with ID: {FiscalYearId}", id);

            var currentUser = User.Identity?.Name ?? "system";

            var command = new CloseFiscalYearCommand 
            { 
                FiscalYearId = id,
                UpdateUser = currentUser
            };
            
            var rowsAffected = await _mediator.Send(command);

            if (rowsAffected == 0)
            {
                _logger.LogWarning("Fiscal year not found for closing with ID: {FiscalYearId}", id);
                return NotFound(ApiResponse<Int64>.CreateFailure(
                    ErrorCodes.FiscalYearNotFound,
                    statusCode: 404));
            }

            _logger.LogInformation("Fiscal year closed successfully with ID: {FiscalYearId}", id);

            return Ok(ApiResponse<Int64>.CreateSuccess(
                rowsAffected,
                ResponseCodes.FiscalYearClosed,
                200));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error closing fiscal year with ID: {FiscalYearId}", id);
            throw;
        }
    }
}
