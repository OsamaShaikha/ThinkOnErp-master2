using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ThinkOnErp.API.Authorization;
using ThinkOnErp.Application.Common;
using ThinkOnErp.Application.DTOs.Accounting.Parties;
using ThinkOnErp.Application.Services.Accounting;

namespace ThinkOnErp.API.Controllers;

[ApiController]
[Route("api/accounting/customers")]
[TenantScoped]
[Authorize]
public class CustomersController : ControllerBase
{
    private readonly ICustomerService _customerService;
    private readonly ILogger<CustomersController> _logger;

    public CustomersController(ICustomerService customerService, ILogger<CustomersController> logger)
    {
        _customerService = customerService;
        _logger = logger;
    }

    [HttpGet]
    [ProducesResponseType(typeof(ApiResponse<IReadOnlyList<CustomerDto>>), StatusCodes.Status200OK)]
    public async Task<ActionResult<ApiResponse<IReadOnlyList<CustomerDto>>>> GetAll(
        [FromQuery] PartyFilterDto filter,
        CancellationToken cancellationToken)
    {
        var customers = await _customerService.GetAllAsync(filter, cancellationToken);
        return Ok(ApiResponse<IReadOnlyList<CustomerDto>>.CreateSuccess(
            customers,
            "تم استرجاع قائمة العملاء بنجاح",
            200));
    }

    [HttpGet("{code}")]
    [ProducesResponseType(typeof(ApiResponse<CustomerDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<CustomerDto>), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<ApiResponse<CustomerDto>>> GetByCode(
        string code,
        CancellationToken cancellationToken)
    {
        var customer = await _customerService.GetByCodeAsync(code, cancellationToken);
        return Ok(ApiResponse<CustomerDto>.CreateSuccess(
            customer,
            "تم استرجاع بيانات العميل بنجاح",
            200));
    }

    [HttpPost]
    [Authorize(Policy = "AdminOnly")]
    [ProducesResponseType(typeof(ApiResponse<CustomerDto>), StatusCodes.Status201Created)]
    public async Task<ActionResult<ApiResponse<CustomerDto>>> Create(
        [FromBody] CreateCustomerDto dto,
        CancellationToken cancellationToken)
    {
        var username = User.Identity?.Name ?? "system";
        var created = await _customerService.CreateAsync(dto, username, cancellationToken);
        return CreatedAtAction(
            nameof(GetByCode),
            new { code = created.CustomerCode },
            ApiResponse<CustomerDto>.CreateSuccess(
                created,
                "تم إنشاء العميل بنجاح",
                201));
    }

    [HttpPut("{code}")]
    [Authorize(Policy = "AdminOnly")]
    [ProducesResponseType(typeof(ApiResponse<CustomerDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<ApiResponse<CustomerDto>>> Update(
        string code,
        [FromBody] UpdateCustomerDto dto,
        CancellationToken cancellationToken)
    {
        var username = User.Identity?.Name ?? "system";
        var updated = await _customerService.UpdateAsync(code, dto, username, cancellationToken);
        return Ok(ApiResponse<CustomerDto>.CreateSuccess(
            updated,
            "تم تحديث بيانات العميل بنجاح",
            200));
    }

    [HttpPatch("{code}/status")]
    [Authorize(Policy = "AdminOnly")]
    [ProducesResponseType(typeof(ApiResponse<bool>), StatusCodes.Status200OK)]
    public async Task<ActionResult<ApiResponse<bool>>> SetStatus(
        string code,
        [FromQuery] bool isActive,
        CancellationToken cancellationToken)
    {
        var username = User.Identity?.Name ?? "system";
        var result = await _customerService.SetStatusAsync(code, isActive, username, cancellationToken);
        return Ok(ApiResponse<bool>.CreateSuccess(
            result,
            $"تم تعديل حالة العميل إلى {(isActive ? "نشط" : "غير نشط")} بنجاح",
            200));
    }
}
