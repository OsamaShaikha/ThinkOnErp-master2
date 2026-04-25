using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ThinkOnErp.Application.Common;
using ThinkOnErp.Application.DTOs.Company;
using ThinkOnErp.Application.Features.Companies.Commands.CreateCompanyWithBranch;
using ThinkOnErp.Application.Features.Companies.Commands.UpdateCompany;
using ThinkOnErp.Application.Features.Companies.Commands.DeleteCompany;
using ThinkOnErp.Application.Features.Companies.Queries.GetAllCompanies;
using ThinkOnErp.Application.Features.Companies.Queries.GetCompanyById;

namespace ThinkOnErp.API.Controllers;

/// <summary>
/// Simplified controller for company management operations with Base64 logo support.
/// Provides exactly 4 endpoints: POST (create with logos), PUT (update with logo), DELETE, GET (with logos).
/// All logo operations are handled via Base64 strings in JSON requests/responses.
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
    /// Retrieves all active companies from the system with Base64 logos included in response.
    /// Requires authentication.
    /// </summary>
    /// <returns>ApiResponse containing list of CompanyDto objects with Base64 logos</returns>
    /// <response code="200">Returns the list of all active companies with logos</response>
    /// <response code="401">User is not authenticated</response>
    [HttpGet]
    [ProducesResponseType(typeof(ApiResponse<List<CompanyDto>>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<List<CompanyDto>>), StatusCodes.Status401Unauthorized)]
    public async Task<ActionResult<ApiResponse<List<CompanyDto>>>> GetAllCompanies()
    {
        try
        {
            _logger.LogInformation("Retrieving all companies with Base64 logos");

            var query = new GetAllCompaniesQuery();
            var companies = await _mediator.Send(query);

            _logger.LogInformation("Retrieved {Count} companies with logos", companies.Count);

            return Ok(ApiResponse<List<CompanyDto>>.CreateSuccess(
                companies,
                "Companies retrieved successfully with logos",
                200));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving all companies");
            throw;
        }
    }

    /// <summary>
    /// Retrieves a specific company by its ID with Base64 logo included in response.
    /// Requires authentication.
    /// </summary>
    /// <param name="id">Unique identifier of the company</param>
    /// <returns>ApiResponse containing CompanyDto object with Base64 logo</returns>
    /// <response code="200">Returns the requested company with logo</response>
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
            _logger.LogInformation("Retrieving company with ID: {CompanyId} including Base64 logo", id);

            var query = new GetCompanyByIdQuery { CompanyId = id };
            var company = await _mediator.Send(query);

            if (company == null)
            {
                _logger.LogWarning("Company not found with ID: {CompanyId}", id);
                return NotFound(ApiResponse<CompanyDto>.CreateFailure(
                    "No company found with the specified identifier",
                    statusCode: 404));
            }

            _logger.LogInformation("Retrieved company with ID: {CompanyId} with logo", id);

            return Ok(ApiResponse<CompanyDto>.CreateSuccess(
                company,
                "Company retrieved successfully with logo",
                200));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving company with ID: {CompanyId}", id);
            throw;
        }
    }

    /// <summary>
    /// Creates a new company with default branch and optional Base64 logos in a single API call.
    /// Supports both company and branch logo creation via Base64 strings in the JSON request.
    /// Requires AdminOnly authorization.
    /// </summary>
    /// <param name="dto">DTO containing company data, branch data, and optional Base64 logos</param>
    /// <returns>ApiResponse containing the newly created company and branch IDs</returns>
    /// <response code="201">Company and branch created successfully with logos</response>
    /// <response code="400">Validation errors in the request</response>
    /// <response code="401">User is not authenticated</response>
    /// <response code="403">User does not have admin privileges</response>
    [HttpPost]
    [Authorize(Policy = "AdminOnly")]
    [ProducesResponseType(typeof(ApiResponse<CreateCompanyWithBranchResult>), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ApiResponse<CreateCompanyWithBranchResult>), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ApiResponse<CreateCompanyWithBranchResult>), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ApiResponse<CreateCompanyWithBranchResult>), StatusCodes.Status403Forbidden)]
    public async Task<ActionResult<ApiResponse<CreateCompanyWithBranchResult>>> CreateCompany([FromBody] CreateCompanyDto dto)
    {
        try
        {
            _logger.LogInformation("Creating new company with default branch and Base64 logos: {CompanyCode}", dto.CompanyCode);

            // Map DTO to command
            var command = new CreateCompanyWithBranchCommand
            {
                CompanyNameAr = dto.CompanyNameAr,
                CompanyNameEn = dto.CompanyNameEn,
                CountryId = dto.CountryId,
                CurrId = dto.CurrId,
                LegalNameAr = dto.LegalNameAr,
                LegalNameEn = dto.LegalNameEn,
                CompanyCode = dto.CompanyCode,
                TaxNumber = dto.TaxNumber,
                CompanyLogoBase64 = dto.CompanyLogoBase64,
                BranchLogoBase64 = dto.BranchLogoBase64,
                
                // Branch fields (migrated from company level)
                DefaultLang = dto.BranchDefaultLang ?? "ar",
                BranchBaseCurrencyId = dto.BranchBaseCurrencyId,
                BranchRoundingRules = dto.BranchRoundingRules,
                
                // Branch contact fields
                BranchNameAr = dto.BranchNameAr ?? dto.CompanyNameAr ?? "Default Branch",
                BranchNameEn = dto.BranchNameEn ?? dto.CompanyNameEn ?? "Default Branch",
                BranchPhone = dto.BranchPhone,
                BranchMobile = dto.BranchMobile,
                BranchFax = dto.BranchFax,
                BranchEmail = dto.BranchEmail,
                
                CreationUser = User.Identity?.Name ?? "system"
            };

            var result = await _mediator.Send(command);

            _logger.LogInformation(
                "Company created successfully with ID: {CompanyId}, Default branch created with ID: {BranchId}, Logos processed",
                result.CompanyId, result.BranchId);

            return CreatedAtAction(
                nameof(GetCompanyById),
                new { id = result.CompanyId },
                ApiResponse<CreateCompanyWithBranchResult>.CreateSuccess(
                    result,
                    "Company and default branch created successfully with logos",
                    201));
        }
        catch (ArgumentException ex)
        {
            _logger.LogWarning("Validation error creating company: {ErrorMessage}", ex.Message);
            return BadRequest(ApiResponse<CreateCompanyWithBranchResult>.CreateFailure(
                ex.Message,
                statusCode: 400));
        }
        catch (InvalidOperationException ex)
        {
            _logger.LogWarning("Business rule violation creating company: {ErrorMessage}", ex.Message);
            return BadRequest(ApiResponse<CreateCompanyWithBranchResult>.CreateFailure(
                ex.Message,
                statusCode: 400));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error creating company: {CompanyCode}", dto.CompanyCode);
            throw;
        }
    }

    /// <summary>
    /// Updates an existing company with optional Base64 logo in a single API call.
    /// Supports company logo update via Base64 string in the JSON request.
    /// Requires AdminOnly authorization.
    /// </summary>
    /// <param name="id">Unique identifier of the company to update</param>
    /// <param name="dto">DTO containing updated company data and optional Base64 logo</param>
    /// <returns>ApiResponse containing the number of rows affected</returns>
    /// <response code="200">Company updated successfully with logo</response>
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
    public async Task<ActionResult<ApiResponse<Int64>>> UpdateCompany(Int64 id, [FromBody] UpdateCompanyDto dto)
    {
        try
        {
            _logger.LogInformation("Updating company with ID: {CompanyId} including Base64 logo", id);

            // Map DTO to command
            var command = new UpdateCompanyCommand
            {
                CompanyId = id,
                CompanyNameAr = dto.CompanyNameAr,
                CompanyNameEn = dto.CompanyNameEn,
                CountryId = dto.CountryId,
                CurrId = dto.CurrId,
                LegalNameAr = dto.LegalNameAr,
                LegalNameEn = dto.LegalNameEn,
                CompanyCode = dto.CompanyCode,
                TaxNumber = dto.TaxNumber,
                CompanyLogoBase64 = dto.CompanyLogoBase64,
                UpdateUser = User.Identity?.Name ?? "system"
            };

            var rowsAffected = await _mediator.Send(command);

            if (rowsAffected == 0)
            {
                _logger.LogWarning("Company not found for update with ID: {CompanyId}", id);
                return NotFound(ApiResponse<Int64>.CreateFailure(
                    "No company found with the specified identifier",
                    statusCode: 404));
            }

            _logger.LogInformation("Company updated successfully with ID: {CompanyId} including logo", id);

            return Ok(ApiResponse<Int64>.CreateSuccess(
                rowsAffected,
                "Company updated successfully with logo",
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