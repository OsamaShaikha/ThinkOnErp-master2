using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ThinkOnErp.Application.Common;
using ThinkOnErp.Application.DTOs.Company;
using ThinkOnErp.Application.Features.Companies.Commands.CreateCompany;
using ThinkOnErp.Application.Features.Companies.Commands.UpdateCompany;
using ThinkOnErp.Application.Features.Companies.Commands.DeleteCompany;
using ThinkOnErp.Application.Features.Companies.Queries.GetAllCompanies;
using ThinkOnErp.Application.Features.Companies.Queries.GetCompanyById;

namespace ThinkOnErp.API.Controllers;

/// <summary>
/// Controller for company management operations.
/// Handles CRUD operations for system companies with appropriate authorization.
/// </summary>
[ApiController]
[Route("api/companies")]
[Authorize]
public class CompanyController : ControllerBase
{
    private readonly IMediator _mediator;
    private readonly ILogger<CompanyController> _logger;

    /// <summary>
    /// Initializes a new instance of the CompanyController class.
    /// </summary>
    /// <param name="mediator">MediatR instance for sending commands and queries</param>
    /// <param name="logger">Logger for controller operations</param>
    public CompanyController(IMediator mediator, ILogger<CompanyController> logger)
    {
        _mediator = mediator ?? throw new ArgumentNullException(nameof(mediator));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <summary>
    /// Retrieves all active companies from the system.
    /// Requires authentication.
    /// </summary>
    /// <returns>ApiResponse containing list of CompanyDto objects</returns>
    /// <response code="200">Returns the list of all active companies</response>
    /// <response code="401">User is not authenticated</response>
    [HttpGet]
    [ProducesResponseType(typeof(ApiResponse<List<CompanyDto>>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<List<CompanyDto>>), StatusCodes.Status401Unauthorized)]
    public async Task<ActionResult<ApiResponse<List<CompanyDto>>>> GetAllCompanies()
    {
        try
        {
            _logger.LogInformation("Retrieving all companies");

            var query = new GetAllCompaniesQuery();
            var companies = await _mediator.Send(query);

            _logger.LogInformation("Retrieved {Count} companies", companies.Count);

            return Ok(ApiResponse<List<CompanyDto>>.CreateSuccess(
                companies,
                "Companies retrieved successfully",
                200));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving all companies");
            throw;
        }
    }

    /// <summary>
    /// Retrieves a specific company by its ID.
    /// Requires authentication.
    /// </summary>
    /// <param name="id">Unique identifier of the company</param>
    /// <returns>ApiResponse containing CompanyDto object</returns>
    /// <response code="200">Returns the requested company</response>
    /// <response code="404">Company not found with the specified ID</response>
    /// <response code="401">User is not authenticated</response>
    [HttpGet("{id}")]
    [ProducesResponseType(typeof(ApiResponse<CompanyDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<CompanyDto>), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ApiResponse<CompanyDto>), StatusCodes.Status401Unauthorized)]
    public async Task<ActionResult<ApiResponse<CompanyDto>>> GetCompanyById(Int64 id)
    {
        try
        {
            _logger.LogInformation("Retrieving company with ID: {CompanyId}", id);

            var query = new GetCompanyByIdQuery { CompanyId = id };
            var company = await _mediator.Send(query);

            if (company == null)
            {
                _logger.LogWarning("Company not found with ID: {CompanyId}", id);
                return NotFound(ApiResponse<CompanyDto>.CreateFailure(
                    "No company found with the specified identifier",
                    statusCode: 404));
            }

            _logger.LogInformation("Retrieved company with ID: {CompanyId}", id);

            return Ok(ApiResponse<CompanyDto>.CreateSuccess(
                company,
                "Company retrieved successfully",
                200));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving company with ID: {CompanyId}", id);
            throw;
        }
    }

    /// <summary>
    /// Creates a new company in the system.
    /// Requires AdminOnly authorization.
    /// </summary>
    /// <param name="command">Command containing company creation data</param>
    /// <returns>ApiResponse containing the newly created company's ID</returns>
    /// <response code="201">Company created successfully</response>
    /// <response code="400">Validation errors in the request</response>
    /// <response code="401">User is not authenticated</response>
    /// <response code="403">User does not have admin privileges</response>
    [HttpPost]
    [Authorize(Policy = "AdminOnly")]
    [ProducesResponseType(typeof(ApiResponse<Int64>), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ApiResponse<Int64>), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ApiResponse<Int64>), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ApiResponse<Int64>), StatusCodes.Status403Forbidden)]
    public async Task<ActionResult<ApiResponse<Int64>>> CreateCompany([FromBody] CreateCompanyCommand command)
    {
        try
        {
            _logger.LogInformation("Creating new company: {CompanyDesc}", command.CompanyNameEn);

            var companyId = await _mediator.Send(command);

            _logger.LogInformation("Company created successfully with ID: {CompanyId}", companyId);

            return CreatedAtAction(
                nameof(GetCompanyById),
                new { id = companyId },
                ApiResponse<Int64>.CreateSuccess(
                    companyId,
                    "Company created successfully",
                    201));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error creating company: {CompanyDesc}", command.CompanyNameEn);
            throw;
        }
    }

    /// <summary>
    /// Updates an existing company in the system.
    /// Requires AdminOnly authorization.
    /// </summary>
    /// <param name="id">Unique identifier of the company to update</param>
    /// <param name="command">Command containing updated company data</param>
    /// <returns>ApiResponse containing the number of rows affected</returns>
    /// <response code="200">Company updated successfully</response>
    /// <response code="400">Validation errors or ID mismatch</response>
    /// <response code="404">Company not found with the specified ID</response>
    /// <response code="401">User is not authenticated</response>
    /// <response code="403">User does not have admin privileges</response>
    [HttpPut("{id}")]
    [Authorize(Policy = "AdminOnly")]
    [ProducesResponseType(typeof(ApiResponse<Int64>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<Int64>), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ApiResponse<Int64>), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ApiResponse<Int64>), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ApiResponse<Int64>), StatusCodes.Status403Forbidden)]
    public async Task<ActionResult<ApiResponse<Int64>>> UpdateCompany(Int64 id, [FromBody] UpdateCompanyCommand command)
    {
        try
        {
            if (id != command.CompanyId)
            {
                _logger.LogWarning("Company ID mismatch: URL ID {UrlId} vs Command ID {CommandId}", id, command.CompanyId);
                return BadRequest(ApiResponse<Int64>.CreateFailure(
                    "Company ID in URL does not match the ID in the request body",
                    statusCode: 400));
            }

            _logger.LogInformation("Updating company with ID: {CompanyId}", id);

            var rowsAffected = await _mediator.Send(command);

            if (rowsAffected == 0)
            {
                _logger.LogWarning("Company not found for update with ID: {CompanyId}", id);
                return NotFound(ApiResponse<Int64>.CreateFailure(
                    "No company found with the specified identifier",
                    statusCode: 404));
            }

            _logger.LogInformation("Company updated successfully with ID: {CompanyId}", id);

            return Ok(ApiResponse<Int64>.CreateSuccess(
                rowsAffected,
                "Company updated successfully",
                200));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error updating company with ID: {CompanyId}", id);
            throw;
        }
    }

    /// <summary>
    /// Deletes (soft delete) a company from the system.
    /// Requires AdminOnly authorization.
    /// </summary>
    /// <param name="id">Unique identifier of the company to delete</param>
    /// <returns>ApiResponse containing the number of rows affected</returns>
    /// <response code="200">Company deleted successfully</response>
    /// <response code="404">Company not found with the specified ID</response>
    /// <response code="401">User is not authenticated</response>
    /// <response code="403">User does not have admin privileges</response>
    [HttpDelete("{id}")]
    [Authorize(Policy = "AdminOnly")]
    [ProducesResponseType(typeof(ApiResponse<Int64>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<Int64>), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ApiResponse<Int64>), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ApiResponse<Int64>), StatusCodes.Status403Forbidden)]
    public async Task<ActionResult<ApiResponse<Int64>>> DeleteCompany(Int64 id)
    {
        try
        {
            _logger.LogInformation("Deleting company with ID: {CompanyId}", id);

            var command = new DeleteCompanyCommand { CompanyId = id };
            var rowsAffected = await _mediator.Send(command);

            if (rowsAffected == 0)
            {
                _logger.LogWarning("Company not found for deletion with ID: {CompanyId}", id);
                return NotFound(ApiResponse<Int64>.CreateFailure(
                    "No company found with the specified identifier",
                    statusCode: 404));
            }

            _logger.LogInformation("Company deleted successfully with ID: {CompanyId}", id);

            return Ok(ApiResponse<Int64>.CreateSuccess(
                rowsAffected,
                "Company deleted successfully",
                200));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error deleting company with ID: {CompanyId}", id);
            throw;
        }
    }
}
