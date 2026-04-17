using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ThinkOnErp.Application.Common;
using ThinkOnErp.Application.DTOs.FiscalYear;
using ThinkOnErp.Application.Features.FiscalYears.Commands.CreateFiscalYear;
using ThinkOnErp.Application.Features.FiscalYears.Commands.UpdateFiscalYear;
using ThinkOnErp.Application.Features.FiscalYears.Commands.DeleteFiscalYear;
using ThinkOnErp.Application.Features.FiscalYears.Commands.CloseFiscalYear;
using ThinkOnErp.Application.Features.FiscalYears.Queries.GetAllFiscalYears;
using ThinkOnErp.Application.Features.FiscalYears.Queries.GetFiscalYearById;
using ThinkOnErp.Application.Features.FiscalYears.Queries.GetFiscalYearsByCompany;

namespace ThinkOnErp.API.Controllers;

/// <summary>
/// Controller for fiscal year management operations.
/// Handles CRUD operations for fiscal years with appropriate authorization.
/// </summary>
[ApiController]
[Route("api/fiscalyears")]
[Authorize]
public class FiscalYearController : ControllerBase
{
    private readonly IMediator _mediator;
    private readonly ILogger<FiscalYearController> _logger;

    /// <summary>
    /// Initializes a new instance of the FiscalYearController class.
    /// </summary>
    /// <param name="mediator">MediatR instance for sending commands and queries</param>
    /// <param name="logger">Logger for controller operations</param>
    public FiscalYearController(IMediator mediator, ILogger<FiscalYearController> logger)
    {
        _mediator = mediator ?? throw new ArgumentNullException(nameof(mediator));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <summary>
    /// Retrieves all active fiscal years from the system.
    /// Requires authentication.
    /// </summary>
    /// <returns>ApiResponse containing list of FiscalYearDto objects</returns>
    /// <response code="200">Returns the list of all active fiscal years</response>
    /// <response code="401">User is not authenticated</response>
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
                "Fiscal years retrieved successfully",
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
    /// Requires authentication.
    /// </summary>
    /// <param name="id">Unique identifier of the fiscal year</param>
    /// <returns>ApiResponse containing FiscalYearDto object</returns>
    /// <response code="200">Returns the requested fiscal year</response>
    /// <response code="404">Fiscal year not found with the specified ID</response>
    /// <response code="401">User is not authenticated</response>
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
                    "No fiscal year found with the specified identifier",
                    statusCode: 404));
            }

            _logger.LogInformation("Retrieved fiscal year with ID: {FiscalYearId}", id);

            return Ok(ApiResponse<FiscalYearDto>.CreateSuccess(
                fiscalYear,
                "Fiscal year retrieved successfully",
                200));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving fiscal year with ID: {FiscalYearId}", id);
            throw;
        }
    }

    /// <summary>
    /// Retrieves all fiscal years for a specific company.
    /// Requires authentication.
    /// </summary>
    /// <param name="companyId">Unique identifier of the company</param>
    /// <returns>ApiResponse containing list of FiscalYearDto objects</returns>
    /// <response code="200">Returns the list of fiscal years for the company</response>
    /// <response code="401">User is not authenticated</response>
    [HttpGet("company/{companyId}")]
    [ProducesResponseType(typeof(ApiResponse<List<FiscalYearDto>>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<List<FiscalYearDto>>), StatusCodes.Status401Unauthorized)]
    public async Task<ActionResult<ApiResponse<List<FiscalYearDto>>>> GetFiscalYearsByCompany(Int64 companyId)
    {
        try
        {
            _logger.LogInformation("Retrieving fiscal years for company ID: {CompanyId}", companyId);

            var query = new GetFiscalYearsByCompanyQuery { CompanyId = companyId };
            var fiscalYears = await _mediator.Send(query);

            _logger.LogInformation("Retrieved {Count} fiscal years for company ID: {CompanyId}", fiscalYears.Count, companyId);

            return Ok(ApiResponse<List<FiscalYearDto>>.CreateSuccess(
                fiscalYears,
                "Fiscal years retrieved successfully",
                200));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving fiscal years for company ID: {CompanyId}", companyId);
            throw;
        }
    }

    /// <summary>
    /// Creates a new fiscal year in the system.
    /// Requires AdminOnly authorization.
    /// </summary>
    /// <param name="command">Command containing fiscal year creation data</param>
    /// <returns>ApiResponse containing the newly created fiscal year's ID</returns>
    /// <response code="201">Fiscal year created successfully</response>
    /// <response code="400">Validation errors in the request</response>
    /// <response code="401">User is not authenticated</response>
    /// <response code="403">User does not have admin privileges</response>
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
            _logger.LogInformation("Creating new fiscal year: {FiscalYearCode}", command.FiscalYearCode);

            var fiscalYearId = await _mediator.Send(command);

            _logger.LogInformation("Fiscal year created successfully with ID: {FiscalYearId}", fiscalYearId);

            return CreatedAtAction(
                nameof(GetFiscalYearById),
                new { id = fiscalYearId },
                ApiResponse<Int64>.CreateSuccess(
                    fiscalYearId,
                    "Fiscal year created successfully",
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
    /// Requires AdminOnly authorization.
    /// </summary>
    /// <param name="id">Unique identifier of the fiscal year to update</param>
    /// <param name="command">Command containing updated fiscal year data</param>
    /// <returns>ApiResponse containing the number of rows affected</returns>
    /// <response code="200">Fiscal year updated successfully</response>
    /// <response code="400">Validation errors or ID mismatch</response>
    /// <response code="404">Fiscal year not found with the specified ID</response>
    /// <response code="401">User is not authenticated</response>
    /// <response code="403">User does not have admin privileges</response>
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
            if (id != command.FiscalYearId)
            {
                _logger.LogWarning("Fiscal year ID mismatch: URL ID {UrlId} vs Command ID {CommandId}", id, command.FiscalYearId);
                return BadRequest(ApiResponse<Int64>.CreateFailure(
                    "Fiscal year ID in URL does not match the ID in the request body",
                    statusCode: 400));
            }

            _logger.LogInformation("Updating fiscal year with ID: {FiscalYearId}", id);

            var rowsAffected = await _mediator.Send(command);

            if (rowsAffected == 0)
            {
                _logger.LogWarning("Fiscal year not found for update with ID: {FiscalYearId}", id);
                return NotFound(ApiResponse<Int64>.CreateFailure(
                    "No fiscal year found with the specified identifier",
                    statusCode: 404));
            }

            _logger.LogInformation("Fiscal year updated successfully with ID: {FiscalYearId}", id);

            return Ok(ApiResponse<Int64>.CreateSuccess(
                rowsAffected,
                "Fiscal year updated successfully",
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
    /// Requires AdminOnly authorization.
    /// </summary>
    /// <param name="id">Unique identifier of the fiscal year to delete</param>
    /// <returns>ApiResponse containing the number of rows affected</returns>
    /// <response code="200">Fiscal year deleted successfully</response>
    /// <response code="404">Fiscal year not found with the specified ID</response>
    /// <response code="401">User is not authenticated</response>
    /// <response code="403">User does not have admin privileges</response>
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
                    "No fiscal year found with the specified identifier",
                    statusCode: 404));
            }

            _logger.LogInformation("Fiscal year deleted successfully with ID: {FiscalYearId}", id);

            return Ok(ApiResponse<Int64>.CreateSuccess(
                rowsAffected,
                "Fiscal year deleted successfully",
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
    /// Requires AdminOnly authorization.
    /// </summary>
    /// <param name="id">Unique identifier of the fiscal year to close</param>
    /// <param name="dto">Optional data for closing the fiscal year</param>
    /// <returns>ApiResponse containing the number of rows affected</returns>
    /// <response code="200">Fiscal year closed successfully</response>
    /// <response code="404">Fiscal year not found with the specified ID</response>
    /// <response code="401">User is not authenticated</response>
    /// <response code="403">User does not have admin privileges</response>
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

            // Get current user from claims
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
                    "No fiscal year found with the specified identifier",
                    statusCode: 404));
            }

            _logger.LogInformation("Fiscal year closed successfully with ID: {FiscalYearId}", id);

            return Ok(ApiResponse<Int64>.CreateSuccess(
                rowsAffected,
                "Fiscal year closed successfully",
                200));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error closing fiscal year with ID: {FiscalYearId}", id);
            throw;
        }
    }
}
