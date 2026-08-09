using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ThinkOnErp.API.Authorization;
using ThinkOnErp.Application.Common;
using ThinkOnErp.Application.DTOs.Accounting;
using ThinkOnErp.Application.Services.Accounting;

namespace ThinkOnErp.API.Controllers;

/// <summary>
/// Exposes the standard account categories available in the current tenant.
/// </summary>
[ApiController]
[Route("api/accounting/account-categories")]
[TenantScoped]
[Authorize]
public sealed class AccountCategoriesController : ControllerBase
{
    private readonly IGlAccountService _service;

    public AccountCategoriesController(IGlAccountService service)
    {
        _service = service ?? throw new ArgumentNullException(nameof(service));
    }

    /// <summary>
    /// Returns the eight chart-of-accounts categories in display order.
    /// </summary>
    [HttpGet]
    [ProducesResponseType(typeof(ApiResponse<List<AccountCategoryDto>>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status403Forbidden)]
    public async Task<ActionResult<ApiResponse<List<AccountCategoryDto>>>> GetAll(
        CancellationToken cancellationToken)
    {
        var categories = (await _service.GetCategoriesAsync(cancellationToken)).ToList();

        return Ok(ApiResponse<List<AccountCategoryDto>>.CreateSuccess(
            categories,
            "Account categories retrieved successfully"));
    }
}
