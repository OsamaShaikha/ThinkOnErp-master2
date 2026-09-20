using System.Collections.Generic;
using System.Security.Claims;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using ThinkOnErp.API.Authorization;
using ThinkOnErp.API.Swagger;
using ThinkOnErp.Application.Common;
using ThinkOnErp.Application.DTOs.Inventory.ItemCategories;
using ThinkOnErp.Application.Services.Inventory;

namespace ThinkOnErp.API.Controllers.Inventory;

/// <summary>
/// إدارة شجرة تصنيفات الأصناف (Categories) الموحدة - مثل شجرة الحسابات (Full CRUD)
/// </summary>
/// <remarks>
/// واجهة عامة موحدة لإدارة تصنيفات المستودع كشجرة هرمية (Tree):
/// - التصنيف الرئيسي (Level 1): عند ترك ParentCategoryId فارغاً أو 0.
/// - التصنيف الفرعي (Level 2+): عند تحديد ParentCategoryId.
/// - الاسترجاع الافتراضي يعيد شجرة هرمية متكاملة بداخلها الأبناء (Children).
/// </remarks>
[ApiController]
[Route("api/inventory/categories")]
[ApiExplorerSettings(GroupName = ApiCategories.Inventory)]
[TenantScoped]
[Authorize]
public sealed class InvCategoriesController : ControllerBase
{
    private readonly IInvItemCategoryService _categoryService;

    public InvCategoriesController(IInvItemCategoryService categoryService)
    {
        _categoryService = categoryService;
    }

    /// <summary>
    /// استرجاع شجرة تصنيفات الأصناف كاملة (Category Tree) أو كقائمة
    /// </summary>
    /// <param name="branchId">فلترة حسب الفرع (اختياري)</param>
    /// <param name="posOnly">إظهار تصنيفات نقاط البيع فقط (اختياري)</param>
    /// <param name="asTree">استرجاع كشجرة هرمية متداخلة (الافتراضي true) أو قائمة مسطحة (false)</param>
    /// <param name="ct">إلغاء الطلب</param>
    [HttpGet]
    [ProducesResponseType(typeof(ApiResponse<List<InvCategoryTreeNodeDto>>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetCategories(
        [FromQuery] long? branchId,
        [FromQuery] bool? posOnly,
        [FromQuery] bool asTree = true,
        CancellationToken ct = default)
    {
        if (asTree)
        {
            var treeResult = await _categoryService.GetTreeAsync(branchId, posOnly, ct);
            return StatusCode(treeResult.StatusCode, treeResult);
        }

        var pagedResult = await _categoryService.GetAllPagedAsync(branchId, 1, 1000, ct);
        return StatusCode(pagedResult.StatusCode, pagedResult);
    }

    /// <summary>
    /// استرجاع تفاصيل تصنيف محدد بواسطة المعرف (ID)
    /// </summary>
    [HttpGet("{id:long}")]
    [ProducesResponseType(typeof(ApiResponse<InvItemCategoryDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<InvItemCategoryDto>), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetCategoryById([FromRoute] long id, CancellationToken ct)
    {
        var result = await _categoryService.GetByIdAsync(id, ct);
        return StatusCode(result.StatusCode, result);
    }

    /// <summary>
    /// إضافة تصنيف جديد (رئيسي إذا كان ParentCategoryId فارغاً، أو فرعي إذا تم تحديد الأب)
    /// </summary>
    [HttpPost]
    [ProducesResponseType(typeof(ApiResponse<InvItemCategoryDto>), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ApiResponse<InvItemCategoryDto>), StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> CreateCategory([FromBody] CreateInvItemCategoryDto dto, CancellationToken ct)
    {
        var username = User.FindFirstValue(ClaimTypes.Name) ?? "SYSTEM";
        var result = await _categoryService.CreateCategoryAsync(dto, username, ct);
        return StatusCode(result.StatusCode, result);
    }

    /// <summary>
    /// تعديل بيانات تصنيف أصناف أو نقله في الشجرة
    /// </summary>
    [HttpPut("{id:long}")]
    [ProducesResponseType(typeof(ApiResponse<InvItemCategoryDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<InvItemCategoryDto>), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ApiResponse<InvItemCategoryDto>), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> UpdateCategory([FromRoute] long id, [FromBody] UpdateInvItemCategoryDto dto, CancellationToken ct)
    {
        var username = User.FindFirstValue(ClaimTypes.Name) ?? "SYSTEM";
        var result = await _categoryService.UpdateCategoryAsync(id, dto, username, ct);
        return StatusCode(result.StatusCode, result);
    }

    /// <summary>
    /// حذف تصنيف أصناف (محمي: يُمنع الحذف إذا كان يتبعه تصنيفات فرعية أو أصناف)
    /// </summary>
    [HttpDelete("{id:long}")]
    [ProducesResponseType(typeof(ApiResponse<bool>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<bool>), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ApiResponse<bool>), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> DeleteCategory([FromRoute] long id, CancellationToken ct)
    {
        var result = await _categoryService.DeleteCategoryAsync(id, ct);
        return StatusCode(result.StatusCode, result);
    }
}
