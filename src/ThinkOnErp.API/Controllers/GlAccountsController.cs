using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ThinkOnErp.API.Authorization;
using ThinkOnErp.Application.Common;
using ThinkOnErp.Application.DTOs.Accounting;
using ThinkOnErp.Application.Services.Accounting;

namespace ThinkOnErp.API.Controllers;

/// <summary>
/// Exposes the tenant-scoped chart of accounts and account maintenance operations.
/// </summary>
[ApiController]
[Route("api/accounting/gl-accounts")]
[TenantScoped]
[Authorize]
public sealed class GlAccountsController : ControllerBase
{
    private readonly IGlAccountService _service;
    private readonly ILogger<GlAccountsController> _logger;

    public GlAccountsController(
        IGlAccountService service,
        ILogger<GlAccountsController> logger)
    {
        _service = service ?? throw new ArgumentNullException(nameof(service));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <summary>
    /// Returns the complete chart of accounts as a sorted hierarchy for the current company.
    /// </summary>
    [HttpGet("tree")]
    [ProducesResponseType(typeof(ApiResponse<List<GlAccountTreeDto>>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status403Forbidden)]
    public async Task<ActionResult<ApiResponse<List<GlAccountTreeDto>>>> GetTree(
        CancellationToken cancellationToken)
    {
        var tree = (await _service.GetTreeAsync(cancellationToken)).ToList();

        _logger.LogInformation(
            "Retrieved chart of accounts with {RootCount} root accounts",
            tree.Count);

        return Ok(ApiResponse<List<GlAccountTreeDto>>.CreateSuccess(
            tree,
            "Chart of accounts retrieved successfully"));
    }

    /// <summary>
    /// Returns active detail accounts that may receive postings for the selected branch.
    /// </summary>
    [HttpGet("postable")]
    [ProducesResponseType(typeof(ApiResponse<List<GlAccountDto>>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status403Forbidden)]
    public async Task<ActionResult<ApiResponse<List<GlAccountDto>>>> GetPostable(
        [FromQuery] long branchId,
        CancellationToken cancellationToken)
    {
        var accounts = (await _service.GetPostableAccountsAsync(branchId, cancellationToken))
            .ToList();

        _logger.LogInformation(
            "Retrieved {AccountCount} postable accounts for branch {BranchId}",
            accounts.Count,
            branchId);

        return Ok(ApiResponse<List<GlAccountDto>>.CreateSuccess(
            accounts,
            "Postable accounts retrieved successfully"));
    }

    /// <summary>
    /// Returns one GL account from the current company's chart of accounts.
    /// </summary>
    /// <param name="id">Tenant-local GL account identifier.</param>
    /// <param name="cancellationToken">Request cancellation token.</param>
    [HttpGet("{id:long}")]
    [ProducesResponseType(typeof(ApiResponse<GlAccountDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<ApiResponse<GlAccountDto>>> GetById(
        long id,
        CancellationToken cancellationToken)
    {
        var account = await _service.GetAccountAsync(id, cancellationToken);

        return Ok(ApiResponse<GlAccountDto>.CreateSuccess(
            account,
            "GL account retrieved successfully"));
    }

    /// <summary>
    /// Creates one account in the current company's chart of accounts.
    /// </summary>
    [HttpPost]
    [Authorize(Policy = "TenantAdminOnly")]
    [ProducesResponseType(typeof(ApiResponse<GlAccountDto>), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ApiResponse<GlAccountDto>), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status403Forbidden)]
    public async Task<ActionResult<ApiResponse<GlAccountDto>>> Create(
        [FromBody] CreateGlAccountDto? request,
        CancellationToken cancellationToken)
    {
        if (request is null)
        {
            return BadRequest(ApiResponse<GlAccountDto>.CreateFailure(
                "Account data is required."));
        }

        var account = await _service.CreateAccountAsync(request, cancellationToken);

        _logger.LogInformation(
            "Created GL account {AccountId} with code {AccountCode}",
            account.Id,
            account.AccountCode);

        return StatusCode(
            StatusCodes.Status201Created,
            ApiResponse<GlAccountDto>.CreateSuccess(
                account,
                "GL account created successfully",
                StatusCodes.Status201Created));
    }

    /// <summary>
    /// Updates the editable attributes of one GL account.
    /// </summary>
    /// <remarks>
    /// Account code, parent, category, level, account type, and normal balance
    /// are structural and cannot be changed after creation. Use the status
    /// endpoint to activate or deactivate the account.
    /// </remarks>
    [HttpPut("{id:long}")]
    [Authorize(Policy = "TenantAdminOnly")]
    [ProducesResponseType(typeof(ApiResponse<GlAccountDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<ApiResponse<GlAccountDto>>> Update(
        long id,
        [FromBody] UpdateGlAccountDto? request,
        CancellationToken cancellationToken)
    {
        if (request is null)
        {
            return BadRequest(ApiResponse<GlAccountDto>.CreateFailure(
                "Account update data is required."));
        }

        var account = await _service.UpdateAccountAsync(id, request, cancellationToken);

        _logger.LogInformation(
            "Updated GL account {AccountId} with code {AccountCode}",
            account.Id,
            account.AccountCode);

        return Ok(ApiResponse<GlAccountDto>.CreateSuccess(
            account,
            "GL account updated successfully"));
    }

    /// <summary>
    /// Activates or deactivates an account in the current company's chart.
    /// </summary>
    [HttpPatch("{id:long}/status")]
    [Authorize(Policy = "TenantAdminOnly")]
    [ProducesResponseType(typeof(ApiResponse<GlAccountStatusDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<GlAccountStatusDto>), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<ApiResponse<GlAccountStatusDto>>> UpdateStatus(
        long id,
        [FromBody] UpdateGlAccountStatusDto? request,
        CancellationToken cancellationToken)
    {
        if (request?.IsActive is null)
        {
            return BadRequest(ApiResponse<GlAccountStatusDto>.CreateFailure(
                "The isActive value is required."));
        }

        await _service.UpdateAccountStatusAsync(
            id,
            request.IsActive.Value,
            cancellationToken);

        var result = new GlAccountStatusDto
        {
            AccountId = id,
            IsActive = request.IsActive.Value
        };

        _logger.LogInformation(
            "Updated GL account {AccountId} active status to {IsActive}",
            id,
            result.IsActive);

        return Ok(ApiResponse<GlAccountStatusDto>.CreateSuccess(
            result,
            "GL account status updated successfully"));
    }

    /// <summary>
    /// Permanently deletes a non-root GL account that has no child accounts
    /// and is not referenced by database-enforced accounting data.
    /// </summary>
    [HttpDelete("{id:long}")]
    [Authorize(Policy = "TenantAdminOnly")]
    [ProducesResponseType(typeof(ApiResponse<GlAccountDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status409Conflict)]
    public async Task<ActionResult<ApiResponse<GlAccountDto>>> Delete(
        long id,
        CancellationToken cancellationToken)
    {
        var account = await _service.DeleteAccountAsync(id, cancellationToken);

        _logger.LogInformation(
            "Deleted GL account {AccountId} with code {AccountCode}",
            account.Id,
            account.AccountCode);

        return Ok(ApiResponse<GlAccountDto>.CreateSuccess(
            account,
            "GL account deleted successfully"));
    }
}
