using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ThinkOnErp.API.Authorization;
using ThinkOnErp.Application.Common;
using ThinkOnErp.Application.DTOs.Accounting.Parties;
using ThinkOnErp.Application.Services.Accounting;

namespace ThinkOnErp.API.Controllers;

[ApiController]
[Route("api/accounting/vendors")]
[TenantScoped]
[Authorize]
public class VendorsController : ControllerBase
{
    private readonly IVendorService _vendorService;
    private readonly ILogger<VendorsController> _logger;

    public VendorsController(IVendorService vendorService, ILogger<VendorsController> logger)
    {
        _vendorService = vendorService;
        _logger = logger;
    }

    [HttpGet]
    [ProducesResponseType(typeof(ApiResponse<IReadOnlyList<VendorDto>>), StatusCodes.Status200OK)]
    public async Task<ActionResult<ApiResponse<IReadOnlyList<VendorDto>>>> GetAll(
        [FromQuery] PartyFilterDto filter,
        CancellationToken cancellationToken)
    {
        var vendors = await _vendorService.GetAllAsync(filter, cancellationToken);
        return Ok(ApiResponse<IReadOnlyList<VendorDto>>.CreateSuccess(
            vendors,
            "تم استرجاع قائمة الموردين بنجاح",
            200));
    }

    [HttpGet("{code}")]
    [ProducesResponseType(typeof(ApiResponse<VendorDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<VendorDto>), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<ApiResponse<VendorDto>>> GetByCode(
        string code,
        CancellationToken cancellationToken)
    {
        var vendor = await _vendorService.GetByCodeAsync(code, cancellationToken);
        return Ok(ApiResponse<VendorDto>.CreateSuccess(
            vendor,
            "تم استرجاع بيانات المورد بنجاح",
            200));
    }

    [HttpPost]
    [Authorize(Policy = "AdminOnly")]
    [ProducesResponseType(typeof(ApiResponse<VendorDto>), StatusCodes.Status201Created)]
    public async Task<ActionResult<ApiResponse<VendorDto>>> Create(
        [FromBody] CreateVendorDto dto,
        CancellationToken cancellationToken)
    {
        var username = User.Identity?.Name ?? "system";
        var created = await _vendorService.CreateAsync(dto, username, cancellationToken);
        return CreatedAtAction(
            nameof(GetByCode),
            new { code = created.VendorCode },
            ApiResponse<VendorDto>.CreateSuccess(
                created,
                "تم إنشاء المورد بنجاح",
                201));
    }

    [HttpPut("{code}")]
    [Authorize(Policy = "AdminOnly")]
    [ProducesResponseType(typeof(ApiResponse<VendorDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<ApiResponse<VendorDto>>> Update(
        string code,
        [FromBody] UpdateVendorDto dto,
        CancellationToken cancellationToken)
    {
        var username = User.Identity?.Name ?? "system";
        var updated = await _vendorService.UpdateAsync(code, dto, username, cancellationToken);
        return Ok(ApiResponse<VendorDto>.CreateSuccess(
            updated,
            "تم تحديث بيانات المورد بنجاح",
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
        var result = await _vendorService.SetStatusAsync(code, isActive, username, cancellationToken);
        return Ok(ApiResponse<bool>.CreateSuccess(
            result,
            $"تم تعديل حالة المورد إلى {(isActive ? "نشط" : "غير نشط")} بنجاح",
            200));
    }
}
