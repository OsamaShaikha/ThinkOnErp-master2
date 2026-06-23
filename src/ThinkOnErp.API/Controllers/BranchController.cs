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

[ApiController]
[Route("api/branches")]
[Authorize]
public class BranchController : ControllerBase
{
    private readonly IMediator _mediator;
    private readonly ILogger<BranchController> _logger;

    public BranchController(IMediator mediator, ILogger<BranchController> logger)
    {
        _mediator = mediator ?? throw new ArgumentNullException(nameof(mediator));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    private bool IsSuperAdmin() =>
        string.Equals(User.FindFirst("isSuperAdmin")?.Value, "true", StringComparison.OrdinalIgnoreCase);

    private long? GetCurrentCompanyId()
    {
        var claim = User.FindFirst("companyId")?.Value;
        return long.TryParse(claim, out var companyId) && companyId > 0 ? companyId : null;
    }

    private bool CanAccessCompany(long? companyId)
    {
        if (IsSuperAdmin())
            return true;

        var currentCompanyId = GetCurrentCompanyId();
        return currentCompanyId.HasValue
            && companyId.HasValue
            && currentCompanyId.Value == companyId.Value;
    }

    [HttpGet]
    [ProducesResponseType(typeof(ApiResponse<List<BranchDto>>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<List<BranchDto>>), StatusCodes.Status401Unauthorized)]
    public async Task<ActionResult<ApiResponse<List<BranchDto>>>> GetAllBranches()
    {
        try
        {
            var companyId = GetCurrentCompanyId();
            var isSuperAdmin = IsSuperAdmin();

            if (!isSuperAdmin && !companyId.HasValue)
            {
                _logger.LogWarning("Branch list denied because user has no companyId claim");
                return Forbid();
            }

            _logger.LogInformation(
                isSuperAdmin
                    ? "Retrieving all branches with logos for SuperAdmin"
                    : "Retrieving branches with logos for company ID: {CompanyId}",
                companyId);

            var branches = isSuperAdmin
                ? await _mediator.Send(new GetAllBranchesQuery())
                : await _mediator.Send(new GetBranchesByCompanyIdQuery { CompanyId = companyId!.Value });

            _logger.LogInformation("Retrieved {Count} branches with logos", branches.Count);

            return Ok(ApiResponse<List<BranchDto>>.CreateSuccess(
                branches,
                "Branches retrieved successfully with logos",
                200));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving all branches");
            throw;
        }
    }

    [HttpGet("{id}")]
    [ProducesResponseType(typeof(ApiResponse<BranchDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<BranchDto>), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ApiResponse<BranchDto>), StatusCodes.Status401Unauthorized)]
    public async Task<ActionResult<ApiResponse<BranchDto>>> GetBranchById(Int64 id)
    {
        try
        {
            _logger.LogInformation("Retrieving branch with ID: {BranchId} including logo", id);

            var query = new GetBranchByIdQuery { BranchId = id };
            var branch = await _mediator.Send(query);

            if (branch == null)
            {
                _logger.LogWarning("Branch not found with ID: {BranchId}", id);
                return NotFound(ApiResponse<BranchDto>.CreateFailure(
                    "No branch found with the specified identifier",
                    statusCode: 404));
            }

            if (!CanAccessCompany(branch.CompanyId))
            {
                _logger.LogWarning(
                    "Branch access denied. User company {UserCompanyId} attempted to access branch {BranchId} company {BranchCompanyId}",
                    GetCurrentCompanyId(),
                    id,
                    branch.CompanyId);
                return Forbid();
            }

            _logger.LogInformation("Retrieved branch with ID: {BranchId} with logo", id);

            return Ok(ApiResponse<BranchDto>.CreateSuccess(
                branch,
                "Branch retrieved successfully with logo",
                200));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving branch with ID: {BranchId}", id);
            throw;
        }
    }

    [HttpPost]
    [Authorize(Policy = "AdminOnly")]
    [ProducesResponseType(typeof(ApiResponse<Int64>), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ApiResponse<Int64>), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ApiResponse<Int64>), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ApiResponse<Int64>), StatusCodes.Status403Forbidden)]
    public async Task<ActionResult<ApiResponse<Int64>>> CreateBranch(
        [FromForm] CreateBranchDto dto,
        IFormFile? branchLogo)
    {
        try
        {
            _logger.LogInformation("Creating new branch with logo file: {BranchDesc}", dto.BranchNameEn);

            if (!CanAccessCompany(dto.CompanyId))
            {
                _logger.LogWarning(
                    "Branch create denied. User company {UserCompanyId} attempted to create branch for company {TargetCompanyId}",
                    GetCurrentCompanyId(),
                    dto.CompanyId);
                return Forbid();
            }

            var command = new CreateBranchCommand
            {
                CompanyId = dto.CompanyId,
                BranchNameAr = dto.BranchNameAr,
                BranchNameEn = dto.BranchNameEn,
                Phone = dto.Phone,
                Mobile = dto.Mobile,
                Fax = dto.Fax,
                Email = dto.Email,
                TaxNumber = dto.TaxNumber,
                IsHeadBranch = dto.IsHeadBranch,
                DefaultLang = dto.DefaultLang,
                BaseCurrencyId = dto.BaseCurrencyId,
                RoundingRules = dto.RoundingRules,
                BranchLogo = branchLogo != null ? await ReadFileBytesAsync(branchLogo) : null,
                Systems = dto.Systems,
                CreationUser = User.Identity?.Name ?? "system"
            };

            var branchId = await _mediator.Send(command);

            _logger.LogInformation("Branch created successfully with ID: {BranchId} including logo", branchId);

            return CreatedAtAction(
                nameof(GetBranchById),
                new { id = branchId },
                ApiResponse<Int64>.CreateSuccess(
                    branchId,
                    "Branch created successfully with logo",
                    201));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error creating branch: {BranchDesc}", dto.BranchNameEn);
            throw;
        }
    }

    [HttpPut("{id}")]
    [Authorize(Policy = "AdminOnly")]
    [ProducesResponseType(typeof(ApiResponse<Int64>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<Int64>), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ApiResponse<Int64>), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ApiResponse<Int64>), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ApiResponse<Int64>), StatusCodes.Status403Forbidden)]
    public async Task<ActionResult<ApiResponse<Int64>>> UpdateBranch(
        Int64 id,
        [FromForm] UpdateBranchDto dto,
        IFormFile? branchLogo)
    {
        try
        {
            _logger.LogInformation("Updating branch with ID: {BranchId} including logo file", id);

            var existingBranch = await _mediator.Send(new GetBranchByIdQuery { BranchId = id });
            if (existingBranch == null)
            {
                _logger.LogWarning("Branch not found for update with ID: {BranchId}", id);
                return NotFound(ApiResponse<Int64>.CreateFailure(
                    "No branch found with the specified identifier",
                    statusCode: 404));
            }

            if (!CanAccessCompany(existingBranch.CompanyId))
            {
                _logger.LogWarning(
                    "Branch update denied. User company {UserCompanyId} attempted to update branch {BranchId} owned by company {BranchCompanyId}",
                    GetCurrentCompanyId(),
                    id,
                    existingBranch.CompanyId);
                return Forbid();
            }

            if (!CanAccessCompany(dto.CompanyId))
            {
                _logger.LogWarning(
                    "Branch update denied. User company {UserCompanyId} attempted to update branch {BranchId} for company {TargetCompanyId}",
                    GetCurrentCompanyId(),
                    id,
                    dto.CompanyId);
                return Forbid();
            }

            var command = new UpdateBranchCommand
            {
                BranchId = id,
                CompanyId = dto.CompanyId,
                BranchNameAr = dto.BranchNameAr,
                BranchNameEn = dto.BranchNameEn,
                Phone = dto.Phone,
                Mobile = dto.Mobile,
                Fax = dto.Fax,
                Email = dto.Email,
                TaxNumber = dto.TaxNumber,
                IsHeadBranch = dto.IsHeadBranch,
                DefaultLang = dto.DefaultLang,
                BaseCurrencyId = dto.BaseCurrencyId,
                RoundingRules = dto.RoundingRules,
                BranchLogo = branchLogo != null ? await ReadFileBytesAsync(branchLogo) : null,
                UpdateUser = User.Identity?.Name ?? "system"
            };

            var rowsAffected = await _mediator.Send(command);

            if (rowsAffected == 0)
            {
                _logger.LogWarning("Branch not found for update with ID: {BranchId}", id);
                return NotFound(ApiResponse<Int64>.CreateFailure(
                    "No branch found with the specified identifier",
                    statusCode: 404));
            }

            _logger.LogInformation("Branch updated successfully with ID: {BranchId} including logo", id);

            return Ok(ApiResponse<Int64>.CreateSuccess(
                rowsAffected,
                "Branch updated successfully with logo",
                200));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error updating branch with ID: {BranchId}", id);
            throw;
        }
    }

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

            var existingBranch = await _mediator.Send(new GetBranchByIdQuery { BranchId = id });
            if (existingBranch == null)
            {
                _logger.LogWarning("Branch not found for deletion with ID: {BranchId}", id);
                return NotFound(ApiResponse<Int64>.CreateFailure(
                    "No branch found with the specified identifier",
                    statusCode: 404));
            }

            if (!CanAccessCompany(existingBranch.CompanyId))
            {
                _logger.LogWarning(
                    "Branch delete denied. User company {UserCompanyId} attempted to delete branch {BranchId} owned by company {BranchCompanyId}",
                    GetCurrentCompanyId(),
                    id,
                    existingBranch.CompanyId);
                return Forbid();
            }

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

    [HttpGet("company/{companyId}")]
    [ProducesResponseType(typeof(ApiResponse<List<BranchDto>>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<List<BranchDto>>), StatusCodes.Status401Unauthorized)]
    public async Task<ActionResult<ApiResponse<List<BranchDto>>>> GetBranchesByCompanyId(Int64 companyId)
    {
        try
        {
            _logger.LogInformation("Retrieving branches for company ID: {CompanyId} with logos", companyId);

            if (!CanAccessCompany(companyId))
            {
                _logger.LogWarning(
                    "Company branch list denied. User company {UserCompanyId} attempted to list company {TargetCompanyId}",
                    GetCurrentCompanyId(),
                    companyId);
                return Forbid();
            }

            var query = new GetBranchesByCompanyIdQuery { CompanyId = companyId };
            var branches = await _mediator.Send(query);

            _logger.LogInformation("Retrieved {Count} branches for company ID: {CompanyId} with logos", branches.Count, companyId);

            return Ok(ApiResponse<List<BranchDto>>.CreateSuccess(
                branches,
                "Branches retrieved successfully with logos",
                200));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving branches for company ID: {CompanyId}", companyId);
            throw;
        }
    }

    private static async Task<byte[]> ReadFileBytesAsync(IFormFile file)
    {
        using var ms = new MemoryStream();
        await file.CopyToAsync(ms);
        return ms.ToArray();
    }
}
