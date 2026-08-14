using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ThinkOnErp.API.Authorization;
using ThinkOnErp.Application.Common;
using ThinkOnErp.Application.DTOs.Accounting;
using ThinkOnErp.Application.Services.Accounting;

namespace ThinkOnErp.API.Controllers;

/// <summary>
/// Exposes Level 1 Categories and Level 2 Sub-Categories from the Chart of Accounts.
/// </summary>
[ApiController]
[Route("api/accounting/account-categories")]
[ApiExplorerSettings(GroupName = ThinkOnErp.API.Swagger.ApiCategories.Accounting)]
[TenantScoped]
[Authorize]
public sealed class AccountCategoriesController : ControllerBase
{
    private readonly IGlAccountService _service;
    private readonly ILogger<AccountCategoriesController> _logger;

    public AccountCategoriesController(
        IGlAccountService service,
        ILogger<AccountCategoriesController> logger)
    {
        _service = service ?? throw new ArgumentNullException(nameof(service));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <summary>
    /// Returns categories (Level 1) and sub-categories (Level 2) for the current company.
    /// </summary>
    [HttpGet]
    [ProducesResponseType(typeof(ApiResponse<List<GlAccountCategoryDto>>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status403Forbidden)]
    public async Task<ActionResult<ApiResponse<List<GlAccountCategoryDto>>>> GetCategories(
        CancellationToken cancellationToken)
    {
        var categories = (await _service.GetCategoriesAsync(cancellationToken)).ToList();

        _logger.LogInformation(
            "Retrieved {CategoryCount} account categories (Level 1 & 2)",
            categories.Count);

        return Ok(ApiResponse<List<GlAccountCategoryDto>>.CreateSuccess(
            categories,
            "Account categories and sub-categories retrieved successfully"));
    }
}
