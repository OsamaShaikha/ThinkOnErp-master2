using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using ThinkOnErp.API.Authorization;
using ThinkOnErp.API.Swagger;
using ThinkOnErp.Application.Services.Inventory;
using ThinkOnErp.Domain.Models;
using ThinkOnErp.Infrastructure.Data;

namespace ThinkOnErp.API.Controllers.Inventory;

[ApiController]
[Route("api/inventory")]
[ApiExplorerSettings(GroupName = ApiCategories.Inventory)]
[TenantScoped]
[Authorize]
public sealed class InventoryExcelController(IInventoryExcelService excel, OracleDbContext db) : ControllerBase
{
    private const long MaxFileBytes = 10 * 1024 * 1024;

    /// <summary>Export branch items with a category dropdown. Set template=true for an empty import template.</summary>
    [HttpGet("items/export")]
    public Task<IActionResult> ExportItems([FromQuery] long branchId, [FromQuery] bool template = false, CancellationToken ct = default) => Export(branchId, true, template, ct);

    /// <summary>Export branch categories. Set template=true for an empty import template.</summary>
    [HttpGet("categories/export")]
    public Task<IActionResult> ExportCategories([FromQuery] long branchId, [FromQuery] bool template = false, CancellationToken ct = default) => Export(branchId, false, template, ct);

    /// <summary>Import new items. Existing codes are rejected; all rows must pass validation before saving.</summary>
    [HttpPost("items/import")]
    [Consumes("multipart/form-data")]
    [RequestSizeLimit(MaxFileBytes + 65536)]
    [RequestFormLimits(MultipartBodyLengthLimit = MaxFileBytes + 65536)]
    public Task<IActionResult> ImportItems([FromQuery] long branchId, IFormFile file, CancellationToken ct) => Import(branchId, true, file, ct);

    /// <summary>Import new categories, including parent links. Existing codes are rejected.</summary>
    [HttpPost("categories/import")]
    [Consumes("multipart/form-data")]
    [RequestSizeLimit(MaxFileBytes + 65536)]
    [RequestFormLimits(MultipartBodyLengthLimit = MaxFileBytes + 65536)]
    public Task<IActionResult> ImportCategories([FromQuery] long branchId, IFormFile file, CancellationToken ct) => Import(branchId, false, file, ct);

    /// <summary>Export opening batch lines with branch item/warehouse dropdowns. Set template=true for an empty workbook, or batchId for a specific batch.</summary>
    [HttpGet("opening-balances/export")]
    public async Task<IActionResult> ExportOpeningBalances([FromQuery] long branchId, [FromServices] IOpeningBalanceExcelService openingExcel,
        [FromQuery] long? batchId = null, [FromQuery] bool template = false, CancellationToken ct = default)
    {
        if (branchId <= 0) return BadRequest(new { message = "A positive branchId is required." });
        if (!await CanAccess(branchId, ct)) return Forbid();
        if (batchId.HasValue && (batchId <= 0 || !await db.InvOpeningBatches.AnyAsync(b => b.Id == batchId && b.BranchId == branchId, ct)))
            return NotFound(new { message = "Opening batch not found in this branch." });
        return File(await openingExcel.ExportAsync(branchId, batchId, template, ct), "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", $"OpeningBalances_{branchId}{(template ? "_Template" : "")}.xlsx");
    }

    /// <summary>Validate Excel lines and create one draft opening batch. Review it and use the existing /opening-balances/{id}/post endpoint to post.</summary>
    [HttpPost("opening-balances/import")]
    [Consumes("multipart/form-data")]
    [RequestSizeLimit(MaxFileBytes + 65536)]
    [RequestFormLimits(MultipartBodyLengthLimit = MaxFileBytes + 65536)]
    public async Task<IActionResult> ImportOpeningBalances([FromQuery] long branchId, [FromQuery] long fiscalYearId, [FromQuery] DateTime batchDate,
        IFormFile file, [FromServices] IOpeningBalanceExcelService openingExcel, [FromQuery] string? description = null, CancellationToken ct = default)
    {
        if (branchId <= 0 || fiscalYearId <= 0) return BadRequest(new { message = "Positive branchId and fiscalYearId are required." });
        if (!await CanAccess(branchId, ct)) return Forbid();
        if (file == null || file.Length == 0 || file.Length > MaxFileBytes || !string.Equals(Path.GetExtension(file.FileName), ".xlsx", StringComparison.OrdinalIgnoreCase))
            return BadRequest(new { message = "Upload a non-empty .xlsx file of at most 10 MB." });
        await using var stream = file.OpenReadStream();
        var result = await openingExcel.ImportAsync(stream, branchId, fiscalYearId, batchDate, description, User.FindFirstValue(ClaimTypes.Name) ?? "SYSTEM", ct);
        return result.IsValid ? Ok(result) : BadRequest(result);
    }

    private async Task<bool> CanAccess(long branchId, CancellationToken ct)
    {
        if (branchId <= 0 || HttpContext.Items[TenantRequestContext.HttpContextItemKey] is not TenantRequestContext tenant) return false;
        if (!await db.SysBranches.AnyAsync(b => b.Id == branchId && b.CompanyId == tenant.CompanyId && b.IsActive, ct)) return false;
        if (User.FindFirst("isSuperAdmin")?.Value == "true") return true;
        if (!long.TryParse(User.FindFirst("userId")?.Value, out var userId)) return false;
        var user = await db.SysUsers.AsNoTracking().SingleOrDefaultAsync(u => u.Id == userId && u.CompanyId == tenant.CompanyId && u.IsActive, ct);
        return user != null && (user.IsAdmin || await db.SysUserBranches.AnyAsync(b => b.UserId == userId && b.BranchId == branchId, ct));
    }

    private async Task<IActionResult> Export(long branchId, bool items, bool template, CancellationToken ct)
    {
        if (branchId <= 0) return BadRequest(new { message = "A positive branchId is required." });
        if (!await CanAccess(branchId, ct)) return Forbid();
        return File(await excel.ExportAsync(branchId, items, template, ct), "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", $"{(items ? "Items" : "Categories")}_{branchId}{(template ? "_Template" : "")}.xlsx");
    }

    private async Task<IActionResult> Import(long branchId, bool items, IFormFile? file, CancellationToken ct)
    {
        if (branchId <= 0) return BadRequest(new { message = "A positive branchId is required." });
        if (!await CanAccess(branchId, ct)) return Forbid();
        if (file == null || file.Length == 0 || file.Length > MaxFileBytes || !string.Equals(Path.GetExtension(file.FileName), ".xlsx", StringComparison.OrdinalIgnoreCase))
            return BadRequest(new { message = "Upload a non-empty .xlsx file of at most 10 MB." });
        await using var stream = file.OpenReadStream();
        var result = await excel.ImportAsync(stream, branchId, items, User.FindFirstValue(ClaimTypes.Name) ?? "SYSTEM", ct);
        return result.IsValid ? Ok(result) : BadRequest(result);
    }
}
