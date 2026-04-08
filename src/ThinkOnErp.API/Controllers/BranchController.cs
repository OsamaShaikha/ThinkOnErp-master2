using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ThinkOnErp.Application.Common;
using ThinkOnErp.Application.DTOs.Branch;
using ThinkOnErp.Application.Features.Branches.Commands.CreateBranch;
using ThinkOnErp.Application.Features.Branches.Commands.UpdateBranch;
using ThinkOnErp.Application.Features.Branches.Commands.DeleteBranch;
using ThinkOnErp.Application.Features.Branches.Queries.GetAllBranches;
using ThinkOnErp.Application.Features.Branches.Queries.GetBranchById;
using ThinkOnErp.Application.Features.Branches.Queries.GetBranchesByCompanyId;

namespace ThinkOnErp.API.Controllers;

/// <summary>
/// Controller for branch management operations.
/// Handles CRUD operations for system branches with appropriate authorization.
/// </summary>
[ApiController]
[Route("api/branches")]
[Authorize]
public class BranchController : ControllerBase
{
    private readonly IMediator _mediator;
    private readonly ILogger<BranchController> _logger;

    /// <summary>
    /// Initializes a new instance of the BranchController class.
    /// </summary>
    /// <param name="mediator">MediatR instance for sending commands and queries</param>
    /// <param name="logger">Logger for controller operations</param>
    public BranchController(IMediator mediator, ILogger<BranchController> logger)
    {
        _mediator = mediator ?? throw new ArgumentNullException(nameof(mediator));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <summary>
    /// Retrieves all active branches from the system.
    /// Requires authentication.
    /// </summary>
    /// <returns>ApiResponse containing list of BranchDto objects</returns>
    /// <response code="200">Returns the list of all active branches</response>
    /// <response code="401">User is not authenticated</response>
    [HttpGet]
    [ProducesResponseType(typeof(ApiResponse<List<BranchDto>>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<List<BranchDto>>), StatusCodes.Status401Unauthorized)]
    public async Task<ActionResult<ApiResponse<List<BranchDto>>>> GetAllBranches()
    {
        try
        {
            _logger.LogInformation("Retrieving all branches");

            var query = new GetAllBranchesQuery();
            var branches = await _mediator.Send(query);

            _logger.LogInformation("Retrieved {Count} branches", branches.Count);

            return Ok(ApiResponse<List<BranchDto>>.CreateSuccess(
                branches,
                "Branches retrieved successfully",
                200));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving all branches");
            throw;
        }
    }

    /// <summary>
    /// Retrieves a specific branch by its ID.
    /// Requires authentication.
    /// </summary>
    /// <param name="id">Unique identifier of the branch</param>
    /// <returns>ApiResponse containing BranchDto object</returns>
    /// <response code="200">Returns the requested branch</response>
    /// <response code="404">Branch not found with the specified ID</response>
    /// <response code="401">User is not authenticated</response>
    [HttpGet("{id}")]
    [ProducesResponseType(typeof(ApiResponse<BranchDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<BranchDto>), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ApiResponse<BranchDto>), StatusCodes.Status401Unauthorized)]
    public async Task<ActionResult<ApiResponse<BranchDto>>> GetBranchById(Int64 id)
    {
        try
        {
            _logger.LogInformation("Retrieving branch with ID: {BranchId}", id);

            var query = new GetBranchByIdQuery { BranchId = id };
            var branch = await _mediator.Send(query);

            if (branch == null)
            {
                _logger.LogWarning("Branch not found with ID: {BranchId}", id);
                return NotFound(ApiResponse<BranchDto>.CreateFailure(
                    "No branch found with the specified identifier",
                    statusCode: 404));
            }

            _logger.LogInformation("Retrieved branch with ID: {BranchId}", id);

            return Ok(ApiResponse<BranchDto>.CreateSuccess(
                branch,
                "Branch retrieved successfully",
                200));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving branch with ID: {BranchId}", id);
            throw;
        }
    }

    /// <summary>
    /// Creates a new branch in the system.
    /// Requires AdminOnly authorization.
    /// </summary>
    /// <param name="command">Command containing branch creation data</param>
    /// <returns>ApiResponse containing the newly created branch's ID</returns>
    /// <response code="201">Branch created successfully</response>
    /// <response code="400">Validation errors in the request</response>
    /// <response code="401">User is not authenticated</response>
    /// <response code="403">User does not have admin privileges</response>
    [HttpPost]
    [Authorize(Policy = "AdminOnly")]
    [ProducesResponseType(typeof(ApiResponse<Int64>), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ApiResponse<Int64>), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ApiResponse<Int64>), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ApiResponse<Int64>), StatusCodes.Status403Forbidden)]
    public async Task<ActionResult<ApiResponse<Int64>>> CreateBranch([FromBody] CreateBranchCommand command)
    {
        try
        {
            _logger.LogInformation("Creating new branch: {BranchDesc}", command.BranchNameEn);

            var branchId = await _mediator.Send(command);

            _logger.LogInformation("Branch created successfully with ID: {BranchId}", branchId);

            return CreatedAtAction(
                nameof(GetBranchById),
                new { id = branchId },
                ApiResponse<Int64>.CreateSuccess(
                    branchId,
                    "Branch created successfully",
                    201));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error creating branch: {BranchDesc}", command.BranchNameEn);
            throw;
        }
    }

    /// <summary>
    /// Updates an existing branch in the system.
    /// Requires AdminOnly authorization.
    /// </summary>
    /// <param name="id">Unique identifier of the branch to update</param>
    /// <param name="command">Command containing updated branch data</param>
    /// <returns>ApiResponse containing the number of rows affected</returns>
    /// <response code="200">Branch updated successfully</response>
    /// <response code="400">Validation errors or ID mismatch</response>
    /// <response code="404">Branch not found with the specified ID</response>
    /// <response code="401">User is not authenticated</response>
    /// <response code="403">User does not have admin privileges</response>
    [HttpPut("{id}")]
    [Authorize(Policy = "AdminOnly")]
    [ProducesResponseType(typeof(ApiResponse<Int64>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<Int64>), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ApiResponse<Int64>), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ApiResponse<Int64>), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ApiResponse<Int64>), StatusCodes.Status403Forbidden)]
    public async Task<ActionResult<ApiResponse<Int64>>> UpdateBranch(Int64 id, [FromBody] UpdateBranchCommand command)
    {
        try
        {
            if (id != command.BranchId)
            {
                _logger.LogWarning("Branch ID mismatch: URL ID {UrlId} vs Command ID {CommandId}", id, command.BranchId);
                return BadRequest(ApiResponse<Int64>.CreateFailure(
                    "Branch ID in URL does not match the ID in the request body",
                    statusCode: 400));
            }

            _logger.LogInformation("Updating branch with ID: {BranchId}", id);

            var rowsAffected = await _mediator.Send(command);

            if (rowsAffected == 0)
            {
                _logger.LogWarning("Branch not found for update with ID: {BranchId}", id);
                return NotFound(ApiResponse<Int64>.CreateFailure(
                    "No branch found with the specified identifier",
                    statusCode: 404));
            }

            _logger.LogInformation("Branch updated successfully with ID: {BranchId}", id);

            return Ok(ApiResponse<Int64>.CreateSuccess(
                rowsAffected,
                "Branch updated successfully",
                200));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error updating branch with ID: {BranchId}", id);
            throw;
        }
    }

    /// <summary>
    /// Deletes (soft delete) a branch from the system.
    /// Requires AdminOnly authorization.
    /// </summary>
    /// <param name="id">Unique identifier of the branch to delete</param>
    /// <returns>ApiResponse containing the number of rows affected</returns>
    /// <response code="200">Branch deleted successfully</response>
    /// <response code="404">Branch not found with the specified ID</response>
    /// <response code="401">User is not authenticated</response>
    /// <response code="403">User does not have admin privileges</response>
    [HttpDelete("{id}")]
    [Authorize(Policy = "AdminOnly")]
    [ProducesResponseType(typeof(ApiResponse<Int64>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<Int64>), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ApiResponse<Int64>), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ApiResponse<Int64>), StatusCodes.Status403Forbidden)]
    public async Task<ActionResult<ApiResponse<Int64>>> DeleteBranch(Int64 id)
    {
        try
        {
            _logger.LogInformation("Deleting branch with ID: {BranchId}", id);

            var command = new DeleteBranchCommand { BranchId = id };
            var rowsAffected = await _mediator.Send(command);

            if (rowsAffected == 0)
            {
                _logger.LogWarning("Branch not found for deletion with ID: {BranchId}", id);
                return NotFound(ApiResponse<Int64>.CreateFailure(
                    "No branch found with the specified identifier",
                    statusCode: 404));
            }

            _logger.LogInformation("Branch deleted successfully with ID: {BranchId}", id);

            return Ok(ApiResponse<Int64>.CreateSuccess(
                rowsAffected,
                "Branch deleted successfully",
                200));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error deleting branch with ID: {BranchId}", id);
            throw;
        }
    }

    /// <summary>
    /// Retrieves all active branches for a specific company.
    /// Requires authentication.
    /// </summary>
    /// <param name="companyId">Unique identifier of the company</param>
    /// <returns>ApiResponse containing list of BranchDto objects for the specified company</returns>
    /// <response code="200">Returns the list of branches for the company</response>
    /// <response code="401">User is not authenticated</response>
    [HttpGet("company/{companyId}")]
    [ProducesResponseType(typeof(ApiResponse<List<BranchDto>>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<List<BranchDto>>), StatusCodes.Status401Unauthorized)]
    public async Task<ActionResult<ApiResponse<List<BranchDto>>>> GetBranchesByCompanyId(Int64 companyId)
    {
        try
        {
            _logger.LogInformation("Retrieving branches for company ID: {CompanyId}", companyId);

            var query = new GetBranchesByCompanyIdQuery { CompanyId = companyId };
            var branches = await _mediator.Send(query);

            _logger.LogInformation("Retrieved {Count} branches for company ID: {CompanyId}", branches.Count, companyId);

            return Ok(ApiResponse<List<BranchDto>>.CreateSuccess(
                branches,
                "Branches retrieved successfully",
                200));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving branches for company ID: {CompanyId}", companyId);
            throw;
        }
    }
}
