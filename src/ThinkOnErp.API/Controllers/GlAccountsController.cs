using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ThinkOnErp.API.Authorization;
using ThinkOnErp.Application.Common;
using ThinkOnErp.Application.DTOs.Accounting;
using ThinkOnErp.Application.Services.Accounting;
using ThinkOnErp.Domain.Constants;

namespace ThinkOnErp.API.Controllers;

/// <summary>
/// Exposes the tenant-scoped chart of accounts and account maintenance operations.
/// </summary>
[ApiController]
[Route("api/accounting/gl-accounts")]
[ApiExplorerSettings(GroupName = ThinkOnErp.API.Swagger.ApiCategories.Accounting)]
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
            ResponseCodes.CoaTreeRetrieved));
    }

    /// <summary>
    /// Returns categories (Level 1) and sub-categories (Level 2) for the current company.
    /// </summary>
    [HttpGet("categories")]
    [ProducesResponseType(typeof(ApiResponse<List<GlAccountCategoryDto>>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status403Forbidden)]
    public async Task<ActionResult<ApiResponse<List<GlAccountCategoryDto>>>> GetCategories(
        CancellationToken cancellationToken)
    {
        var categories = (await _service.GetCategoriesAsync(cancellationToken)).ToList();

        _logger.LogInformation(
            "Retrieved {CategoryCount} categories for current company",
            categories.Count);

        return Ok(ApiResponse<List<GlAccountCategoryDto>>.CreateSuccess(
            categories,
            ResponseCodes.CategoriesRetrieved));
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
            ResponseCodes.DataRetrieved));
    }

    /// <summary>
    /// Generates the next sequential child account code under a specified parent account code.
    /// </summary>
    [HttpGet("next-child-code")]
    [ProducesResponseType(typeof(ApiResponse<string>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<ApiResponse<string>>> GetNextChildCode(
        [FromQuery] string parentAccountCode,
        CancellationToken cancellationToken)
    {
        var nextCode = await _service.GetNextChildCodeAsync(parentAccountCode, cancellationToken);
        return Ok(ApiResponse<string>.CreateSuccess(nextCode, ResponseCodes.DataRetrieved));
    }

    /// <summary>
    /// Returns one GL account from the current company's chart of accounts by its code.
    /// </summary>
    [HttpGet("{accountCode}")]
    [ProducesResponseType(typeof(ApiResponse<GlAccountDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<ApiResponse<GlAccountDto>>> GetByCode(
        string accountCode,
        CancellationToken cancellationToken)
    {
        var account = await _service.GetAccountByCodeAsync(accountCode, cancellationToken);

        return Ok(ApiResponse<GlAccountDto>.CreateSuccess(
            account,
            ResponseCodes.DataRetrieved));
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
                ErrorCodes.FieldRequired));
        }

        var account = await _service.CreateAccountAsync(request, cancellationToken);

        _logger.LogInformation(
            "Created GL account with code {AccountCode}",
            account.AccountCode);

        return StatusCode(
            StatusCodes.Status201Created,
            ApiResponse<GlAccountDto>.CreateSuccess(
                account,
                ResponseCodes.AccountCreated,
                StatusCodes.Status201Created));
    }

    /// <summary>
    /// Updates the editable attributes of one GL account.
    /// </summary>
    [HttpPut("{accountCode}")]
    [Authorize(Policy = "TenantAdminOnly")]
    [ProducesResponseType(typeof(ApiResponse<GlAccountDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<ApiResponse<GlAccountDto>>> Update(
        string accountCode,
        [FromBody] UpdateGlAccountDto? request,
        CancellationToken cancellationToken)
    {
        if (request is null)
        {
            return BadRequest(ApiResponse<GlAccountDto>.CreateFailure(
                ErrorCodes.FieldRequired));
        }

        var account = await _service.UpdateAccountAsync(accountCode, request, cancellationToken);

        _logger.LogInformation(
            "Updated GL account with code {AccountCode}",
            account.AccountCode);

        return Ok(ApiResponse<GlAccountDto>.CreateSuccess(
            account,
            ResponseCodes.AccountUpdated));
    }

    /// <summary>
    /// Activates or deactivates an account in the current company's chart.
    /// </summary>
    [HttpPatch("{accountCode}/status")]
    [Authorize(Policy = "TenantAdminOnly")]
    [ProducesResponseType(typeof(ApiResponse<GlAccountStatusDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<GlAccountStatusDto>), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<ApiResponse<GlAccountStatusDto>>> UpdateStatus(
        string accountCode,
        [FromBody] UpdateGlAccountStatusDto? request,
        CancellationToken cancellationToken)
    {
        if (request?.IsActive is null)
        {
            return BadRequest(ApiResponse<GlAccountStatusDto>.CreateFailure(
                ErrorCodes.FieldRequired));
        }

        await _service.UpdateAccountStatusAsync(
            accountCode,
            request.IsActive.Value,
            cancellationToken);

        var result = new GlAccountStatusDto
        {
            AccountCode = accountCode,
            IsActive = request.IsActive.Value
        };

        _logger.LogInformation(
            "Updated GL account {AccountCode} active status to {IsActive}",
            accountCode,
            result.IsActive);

        return Ok(ApiResponse<GlAccountStatusDto>.CreateSuccess(
            result,
            ResponseCodes.StatusUpdated));
    }

    /// <summary>
    /// Permanently deletes a non-root GL account that has no child accounts
    /// and is not referenced by database-enforced accounting data.
    /// </summary>
    [HttpDelete("{accountCode}")]
    [Authorize(Policy = "TenantAdminOnly")]
    [ProducesResponseType(typeof(ApiResponse<GlAccountDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status409Conflict)]
    public async Task<ActionResult<ApiResponse<GlAccountDto>>> Delete(
        string accountCode,
        CancellationToken cancellationToken)
    {
        var account = await _service.DeleteAccountAsync(accountCode, cancellationToken);

        _logger.LogInformation(
            "Deleted GL account with code {AccountCode}",
            account.AccountCode);

        return Ok(ApiResponse<GlAccountDto>.CreateSuccess(
            account,
            ResponseCodes.AccountDeleted));
    }
}

