using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ThinkOnErp.API.Authorization;
using ThinkOnErp.Application.Common;
using ThinkOnErp.Application.DTOs.Branch;
using ThinkOnErp.Application.Features.Branches.Commands.CreateBranch;
using ThinkOnErp.Application.Features.Branches.Commands.UpdateBranch;
using ThinkOnErp.Application.Features.Branches.Commands.DeleteBranch;
using ThinkOnErp.Application.Features.Branches.Commands.SetBranchStatus;
using ThinkOnErp.Application.Features.Branches.Queries.GetAllBranches;
using ThinkOnErp.Application.Features.Branches.Queries.GetBranchById;
using ThinkOnErp.Application.Features.Branches.Queries.GetBranchesByCompanyId;
using ThinkOnErp.Domain.Constants;
using ThinkOnErp.Domain.Interfaces;
using ThinkOnErp.Infrastructure.Services;

namespace ThinkOnErp.API.Controllers;

[ApiController]
[Route("api/branches")]
[TenantScoped]
[Authorize]
public class BranchController : ControllerBase
{
    private readonly IMediator _mediator;
    private readonly ILogger<BranchController> _logger;
    private readonly ISuperAdminRepository _superAdminRepository;
    private readonly PasswordHashingService _passwordHashingService;

    public BranchController(
        IMediator mediator, 
        ILogger<BranchController> logger,
        ISuperAdminRepository superAdminRepository,
        PasswordHashingService passwordHashingService)
    {
        _mediator = mediator ?? throw new ArgumentNullException(nameof(mediator));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _superAdminRepository = superAdminRepository ?? throw new ArgumentNullException(nameof(superAdminRepository));
        _passwordHashingService = passwordHashingService ?? throw new ArgumentNullException(nameof(passwordHashingService));
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
                ResponseCodes.DataRetrieved,
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
                    ErrorCodes.EntityNotFound,
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
                ResponseCodes.DataRetrieved,
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
                UsersLimit = dto.UsersLimit,
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
                    ResponseCodes.BranchCreated,
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

            if (!await VerifySuperAdminPinAsync())
            {
                return StatusCode(StatusCodes.Status403Forbidden, ApiResponse<Int64>.CreateFailure(
                    ErrorCodes.UnauthorizedAction, 
                    statusCode: 403));
            }

            var existingBranch = await _mediator.Send(new GetBranchByIdQuery { BranchId = id });
            if (existingBranch == null)
            {
                _logger.LogWarning("Branch not found for update with ID: {BranchId}", id);
                return NotFound(ApiResponse<Int64>.CreateFailure(
                    ErrorCodes.EntityNotFound,
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
                UsersLimit = dto.UsersLimit,
                BranchLogo = branchLogo != null ? await ReadFileBytesAsync(branchLogo) : null,
                UpdateUser = User.Identity?.Name ?? "system"
            };

            var rowsAffected = await _mediator.Send(command);

            if (rowsAffected == 0)
            {
                _logger.LogWarning("Branch not found for update with ID: {BranchId}", id);
                return NotFound(ApiResponse<Int64>.CreateFailure(
                    ErrorCodes.EntityNotFound,
                    statusCode: 404));
            }

            _logger.LogInformation("Branch updated successfully with ID: {BranchId} including logo", id);

            return Ok(ApiResponse<Int64>.CreateSuccess(
                rowsAffected,
                ResponseCodes.BranchUpdated,
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

            if (!await VerifySuperAdminPinAsync())
            {
                return StatusCode(StatusCodes.Status403Forbidden, ApiResponse<Int64>.CreateFailure(
                    ErrorCodes.UnauthorizedAction, 
                    statusCode: 403));
            }

            var existingBranch = await _mediator.Send(new GetBranchByIdQuery { BranchId = id });
            if (existingBranch == null)
            {
                _logger.LogWarning("Branch not found for deletion with ID: {BranchId}", id);
                return NotFound(ApiResponse<Int64>.CreateFailure(
                    ErrorCodes.EntityNotFound,
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
                    ErrorCodes.EntityNotFound,
                    statusCode: 404));
            }

            _logger.LogInformation("Branch deleted successfully with ID: {BranchId}", id);

            return Ok(ApiResponse<Int64>.CreateSuccess(
                rowsAffected,
                ResponseCodes.RecordDeleted,
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
                ResponseCodes.DataRetrieved,
                200));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving branches for company ID: {CompanyId}", companyId);
            throw;
        }
    }

    [HttpPatch("{id}/status")]
    [ProducesResponseType(typeof(ApiResponse<bool>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<bool>), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<ApiResponse<bool>>> SetBranchStatus(Int64 id, [FromBody] SetBranchStatusDto dto)
    {
        try
        {
            _logger.LogInformation("Setting status for branch ID {BranchId} to IsActive={IsActive}", id, dto.IsActive);
            var userName = User.Identity?.Name ?? "system";
            var command = new SetBranchStatusCommand
            {
                BranchId = id,
                IsActive = dto.IsActive,
                UpdateUser = userName
            };
            var success = await _mediator.Send(command);
            if (!success)
            {
                return NotFound(ApiResponse<bool>.CreateFailure(ErrorCodes.EntityNotFound, statusCode: 404));
            }
            return Ok(ApiResponse<bool>.CreateSuccess(true, ResponseCodes.StatusUpdated, 200));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error setting status for branch ID {BranchId}", id);
            throw;
        }
    }

    [HttpPatch("{id}/activate")]
    [ProducesResponseType(typeof(ApiResponse<bool>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<bool>), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<ApiResponse<bool>>> ActivateBranch(Int64 id) =>
        await SetBranchStatus(id, new SetBranchStatusDto { IsActive = true });

    [HttpPatch("{id}/deactivate")]
    [ProducesResponseType(typeof(ApiResponse<bool>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<bool>), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<ApiResponse<bool>>> DeactivateBranch(Int64 id) =>
        await SetBranchStatus(id, new SetBranchStatusDto { IsActive = false });

    private static async Task<byte[]> ReadFileBytesAsync(IFormFile file)
    {
        using var ms = new MemoryStream();
        await file.CopyToAsync(ms);
        return ms.ToArray();
    }

    private async Task<bool> VerifySuperAdminPinAsync()
    {
        var superAdminIdClaim = User.FindFirst("userId")?.Value;
        if (!long.TryParse(superAdminIdClaim, out var superAdminId))
        {
            return false;
        }

        var pinCode = Request.Headers["X-SuperAdmin-PIN"].ToString();
        if (string.IsNullOrEmpty(pinCode))
        {
            return false;
        }

        var superAdmin = await _superAdminRepository.GetByIdAsync(superAdminId);
        if (superAdmin == null)
        {
            return false;
        }

        if (string.IsNullOrEmpty(superAdmin.PinHash))
        {
            return pinCode == "1234";
        }

        return _passwordHashingService.VerifyPassword(pinCode, superAdmin.PinHash);
    }
}
