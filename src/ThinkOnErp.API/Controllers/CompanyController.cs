using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ThinkOnErp.Application.Common;
using ThinkOnErp.Application.DTOs.Company;
using ThinkOnErp.Application.Features.Companies.Commands.CreateCompany;
using ThinkOnErp.Application.Features.Companies.Commands.UpdateCompany;
using ThinkOnErp.Application.Features.Companies.Commands.DeleteCompany;
using ThinkOnErp.Application.Features.Companies.Commands.UpdateCompanyLogo;
using ThinkOnErp.Application.Features.Companies.Queries.GetAllCompanies;
using ThinkOnErp.Application.Features.Companies.Queries.GetCompanyById;
using ThinkOnErp.Application.Features.Companies.Queries.GetCompanyLogo;

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

    /// <summary>
    /// Retrieves the logo for a specific company.
    /// Requires authentication.
    /// </summary>
    /// <param name="id">Unique identifier of the company</param>
    /// <returns>Company logo as byte array</returns>
    /// <response code="200">Returns the company logo</response>
    /// <response code="404">Company not found or no logo available</response>
    /// <response code="401">User is not authenticated</response>
    [HttpGet("{id}/logo")]
    [ProducesResponseType(typeof(FileContentResult), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<byte[]>), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ApiResponse<byte[]>), StatusCodes.Status401Unauthorized)]
    public async Task<ActionResult> GetCompanyLogo(Int64 id)
    {
        try
        {
            _logger.LogInformation("Retrieving logo for company with ID: {CompanyId}", id);

            var query = new GetCompanyLogoQuery { CompanyId = id };
            var logo = await _mediator.Send(query);

            if (logo == null || logo.Length == 0)
            {
                _logger.LogWarning("No logo found for company with ID: {CompanyId}", id);
                return NotFound(ApiResponse<byte[]>.CreateFailure(
                    "No logo found for the specified company",
                    statusCode: 404));
            }

            _logger.LogInformation("Retrieved logo for company with ID: {CompanyId}, Size: {LogoSize} bytes", id, logo.Length);

            return File(logo, "image/jpeg", $"company_{id}_logo.jpg");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving logo for company with ID: {CompanyId}", id);
            throw;
        }
    }

    /// <summary>
    /// Updates the logo for a specific company.
    /// Requires AdminOnly authorization.
    /// </summary>
    /// <param name="id">Unique identifier of the company</param>
    /// <param name="logoFile">Logo image file (max 5MB)</param>
    /// <returns>ApiResponse containing the number of rows affected</returns>
    /// <response code="200">Logo updated successfully</response>
    /// <response code="400">Invalid file or validation errors</response>
    /// <response code="404">Company not found</response>
    /// <response code="401">User is not authenticated</response>
    /// <response code="403">User does not have admin privileges</response>
    [HttpPut("{id}/logo")]
    [Authorize(Policy = "AdminOnly")]
    [ProducesResponseType(typeof(ApiResponse<Int64>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<Int64>), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ApiResponse<Int64>), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ApiResponse<Int64>), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ApiResponse<Int64>), StatusCodes.Status403Forbidden)]
    public async Task<ActionResult<ApiResponse<Int64>>> UpdateCompanyLogo(Int64 id, IFormFile logoFile)
    {
        try
        {
            if (logoFile == null || logoFile.Length == 0)
            {
                _logger.LogWarning("No logo file provided for company ID: {CompanyId}", id);
                return BadRequest(ApiResponse<Int64>.CreateFailure(
                    "Logo file is required",
                    statusCode: 400));
            }

            // Validate file size (5MB limit)
            const int maxFileSize = 5 * 1024 * 1024; // 5MB
            if (logoFile.Length > maxFileSize)
            {
                _logger.LogWarning("Logo file too large for company ID: {CompanyId}, Size: {FileSize} bytes", id, logoFile.Length);
                return BadRequest(ApiResponse<Int64>.CreateFailure(
                    "Logo file size cannot exceed 5MB",
                    statusCode: 400));
            }

            // Validate file type
            var allowedTypes = new[] { "image/jpeg", "image/jpg", "image/png", "image/gif" };
            if (!allowedTypes.Contains(logoFile.ContentType.ToLower()))
            {
                _logger.LogWarning("Invalid logo file type for company ID: {CompanyId}, Type: {ContentType}", id, logoFile.ContentType);
                return BadRequest(ApiResponse<Int64>.CreateFailure(
                    "Logo file must be a valid image (JPEG, PNG, or GIF)",
                    statusCode: 400));
            }

            _logger.LogInformation("Updating logo for company with ID: {CompanyId}, File size: {FileSize} bytes", id, logoFile.Length);

            // Convert file to byte array
            byte[] logoBytes;
            using (var memoryStream = new MemoryStream())
            {
                await logoFile.CopyToAsync(memoryStream);
                logoBytes = memoryStream.ToArray();
            }

            var command = new UpdateCompanyLogoCommand
            {
                CompanyId = id,
                Logo = logoBytes,
                UpdateUser = User.Identity?.Name ?? "System"
            };

            var rowsAffected = await _mediator.Send(command);

            if (rowsAffected == 0)
            {
                _logger.LogWarning("Company not found for logo update with ID: {CompanyId}", id);
                return NotFound(ApiResponse<Int64>.CreateFailure(
                    "No company found with the specified identifier",
                    statusCode: 404));
            }

            _logger.LogInformation("Logo updated successfully for company with ID: {CompanyId}", id);

            return Ok(ApiResponse<Int64>.CreateSuccess(
                rowsAffected,
                "Company logo updated successfully",
                200));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error updating logo for company with ID: {CompanyId}", id);
            throw;
        }
    }

    /// <summary>
    /// Deletes the logo for a specific company.
    /// Requires AdminOnly authorization.
    /// </summary>
    /// <param name="id">Unique identifier of the company</param>
    /// <returns>ApiResponse containing the number of rows affected</returns>
    /// <response code="200">Logo deleted successfully</response>
    /// <response code="404">Company not found</response>
    /// <response code="401">User is not authenticated</response>
    /// <response code="403">User does not have admin privileges</response>
    [HttpDelete("{id}/logo")]
    [Authorize(Policy = "AdminOnly")]
    [ProducesResponseType(typeof(ApiResponse<Int64>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<Int64>), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ApiResponse<Int64>), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ApiResponse<Int64>), StatusCodes.Status403Forbidden)]
    public async Task<ActionResult<ApiResponse<Int64>>> DeleteCompanyLogo(Int64 id)
    {
        try
        {
            _logger.LogInformation("Deleting logo for company with ID: {CompanyId}", id);

            var command = new UpdateCompanyLogoCommand
            {
                CompanyId = id,
                Logo = Array.Empty<byte>(), // Empty array to delete logo
                UpdateUser = User.Identity?.Name ?? "System"
            };

            var rowsAffected = await _mediator.Send(command);

            if (rowsAffected == 0)
            {
                _logger.LogWarning("Company not found for logo deletion with ID: {CompanyId}", id);
                return NotFound(ApiResponse<Int64>.CreateFailure(
                    "No company found with the specified identifier",
                    statusCode: 404));
            }

            _logger.LogInformation("Logo deleted successfully for company with ID: {CompanyId}", id);

            return Ok(ApiResponse<Int64>.CreateSuccess(
                rowsAffected,
                "Company logo deleted successfully",
                200));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error deleting logo for company with ID: {CompanyId}", id);
            throw;
        }
    }
}
