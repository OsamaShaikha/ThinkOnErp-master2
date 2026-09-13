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
using ThinkOnErp.Application.DTOs.Inventory.ItemGroups;
using ThinkOnErp.Application.Services.Inventory;

namespace ThinkOnErp.API.Controllers.Inventory;

/// <summary>
/// إدارة المجموعات الرئيسية والمجموعات الفرعية للأصناف (Main Groups &amp; Sub-Groups Full CRUD API)
/// </summary>
[ApiController]
[Route("api/inventory/groups")]
[ApiExplorerSettings(GroupName = ApiCategories.Inventory)]
[TenantScoped]
[Authorize]
public sealed class InvItemGroupsController : ControllerBase
{
    private readonly IInvItemGroupService _groupService;

    public InvItemGroupsController(IInvItemGroupService groupService)
    {
        _groupService = groupService;
    }

    #region Main Groups CRUD (المجموعات الرئيسية)

    /// <summary>
    /// إضافة مجموعة رئيسية جديدة للأصناف (Main Group Creation)
    /// </summary>
    [HttpPost("main")]
    [ProducesResponseType(typeof(ApiResponse<InvMainGroupDto>), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ApiResponse<InvMainGroupDto>), StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> CreateMainGroup([FromBody] CreateMainGroupDto dto, CancellationToken ct)
    {
        var username = User.FindFirstValue(ClaimTypes.Name) ?? "SYSTEM";
        var result = await _groupService.CreateMainGroupAsync(dto, username, ct);
        return StatusCode(result.StatusCode, result);
    }

    /// <summary>
    /// استرجاع كافة المجموعات الرئيسية مع عدد المجموعات الفرعية وعدد الأصناف التابعة
    /// </summary>
    [HttpGet("main")]
    [ProducesResponseType(typeof(ApiResponse<List<InvMainGroupDto>>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetMainGroups([FromQuery] long? branchId, CancellationToken ct)
    {
        var result = await _groupService.GetMainGroupsAsync(branchId, ct);
        return StatusCode(result.StatusCode, result);
    }

    /// <summary>
    /// استرجاع تفاصيل مجموعة رئيسية محددة بواسطة المعرف
    /// </summary>
    [HttpGet("main/{id:long}")]
    [ProducesResponseType(typeof(ApiResponse<InvMainGroupDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<InvMainGroupDto>), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetMainGroupById([FromRoute] long id, CancellationToken ct)
    {
        var result = await _groupService.GetMainGroupByIdAsync(id, ct);
        return StatusCode(result.StatusCode, result);
    }

    /// <summary>
    /// تعديل بيانات مجموعة رئيسية
    /// </summary>
    [HttpPut("main/{id:long}")]
    [ProducesResponseType(typeof(ApiResponse<InvMainGroupDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<InvMainGroupDto>), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> UpdateMainGroup([FromRoute] long id, [FromBody] UpdateMainGroupDto dto, CancellationToken ct)
    {
        var username = User.FindFirstValue(ClaimTypes.Name) ?? "SYSTEM";
        var result = await _groupService.UpdateMainGroupAsync(id, dto, username, ct);
        return StatusCode(result.StatusCode, result);
    }

    /// <summary>
    /// حذف مجموعة رئيسية (محمي ضد الحذف في حال وجود مجموعات فرعية أو أصناف مرتبطة)
    /// </summary>
    [HttpDelete("main/{id:long}")]
    [ProducesResponseType(typeof(ApiResponse<bool>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<bool>), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ApiResponse<bool>), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> DeleteMainGroup([FromRoute] long id, CancellationToken ct)
    {
        var result = await _groupService.DeleteMainGroupAsync(id, ct);
        return StatusCode(result.StatusCode, result);
    }

    #endregion

    #region Sub Groups CRUD (المجموعات الفرعية)

    /// <summary>
    /// إضافة مجموعة فرعية جديدة وربطها بالمجموعة الرئيسية (Sub-Group Creation)
    /// </summary>
    [HttpPost("sub")]
    [ProducesResponseType(typeof(ApiResponse<InvSubGroupDto>), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ApiResponse<InvSubGroupDto>), StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> CreateSubGroup([FromBody] CreateSubGroupDto dto, CancellationToken ct)
    {
        var username = User.FindFirstValue(ClaimTypes.Name) ?? "SYSTEM";
        var result = await _groupService.CreateSubGroupAsync(dto, username, ct);
        return StatusCode(result.StatusCode, result);
    }

    /// <summary>
    /// استرجاع كافة المجموعات الفرعية مع اسم المجموعة الرئيسية وعدد الأصناف
    /// </summary>
    [HttpGet("sub")]
    [ProducesResponseType(typeof(ApiResponse<List<InvSubGroupDto>>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetAllSubGroups([FromQuery] long? branchId, CancellationToken ct)
    {
        var result = await _groupService.GetAllSubGroupsAsync(branchId, ct);
        return StatusCode(result.StatusCode, result);
    }

    /// <summary>
    /// استرجاع كافة المجموعات الفرعية التابعة لمجموعة رئيسية محددة
    /// </summary>
    [HttpGet("main/{mainGroupId:long}/sub")]
    [ProducesResponseType(typeof(ApiResponse<List<InvSubGroupDto>>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetSubGroupsByMainGroup([FromRoute] long mainGroupId, CancellationToken ct)
    {
        var result = await _groupService.GetSubGroupsByMainGroupIdAsync(mainGroupId, ct);
        return StatusCode(result.StatusCode, result);
    }

    /// <summary>
    /// استرجاع تفاصيل مجموعة فرعية محددة بواسطة المعرف
    /// </summary>
    [HttpGet("sub/{id:long}")]
    [ProducesResponseType(typeof(ApiResponse<InvSubGroupDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<InvSubGroupDto>), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetSubGroupById([FromRoute] long id, CancellationToken ct)
    {
        var result = await _groupService.GetSubGroupByIdAsync(id, ct);
        return StatusCode(result.StatusCode, result);
    }

    /// <summary>
    /// تعديل بيانات مجموعة فرعية (أو نقلها لمجموعة رئيسية أخرى)
    /// </summary>
    [HttpPut("sub/{id:long}")]
    [ProducesResponseType(typeof(ApiResponse<InvSubGroupDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<InvSubGroupDto>), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> UpdateSubGroup([FromRoute] long id, [FromBody] UpdateSubGroupDto dto, CancellationToken ct)
    {
        var username = User.FindFirstValue(ClaimTypes.Name) ?? "SYSTEM";
        var result = await _groupService.UpdateSubGroupAsync(id, dto, username, ct);
        return StatusCode(result.StatusCode, result);
    }

    /// <summary>
    /// حذف مجموعة فرعية (محمي ضد الحذف في حال وجود أصناف مرتبطة بها)
    /// </summary>
    [HttpDelete("sub/{id:long}")]
    [ProducesResponseType(typeof(ApiResponse<bool>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<bool>), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ApiResponse<bool>), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> DeleteSubGroup([FromRoute] long id, CancellationToken ct)
    {
        var result = await _groupService.DeleteSubGroupAsync(id, ct);
        return StatusCode(result.StatusCode, result);
    }

    #endregion

    #region Multi-Level Tree & POS Hierarchy

    /// <summary>
    /// إضافة مجموعة أصناف جديدة على أي مستوى في الشجرة (Unified Group Creation)
    /// </summary>
    /// <remarks>
    /// إذا تم ترك ParentGroupId فارغاً، تُنشأ المجموعة كمجموعة رئيسية (Level 1).
    /// إذا تم تمرير ParentGroupId، تُنشأ كمجموعة فرعية تابعة للأب المحدد (Level = Parent.Level + 1).
    /// </remarks>
    [HttpPost]
    [ProducesResponseType(typeof(ApiResponse<InvItemGroupDto>), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ApiResponse<InvItemGroupDto>), StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> CreateGroup([FromBody] CreateInvItemGroupDto dto, CancellationToken ct)
    {
        var username = User.FindFirstValue(ClaimTypes.Name) ?? "SYSTEM";
        var result = await _groupService.CreateGroupAsync(dto, username, ct);
        return StatusCode(result.StatusCode, result);
    }

    /// <summary>
    /// استرجاع شجرة مجموعات الأصناف الكاملة المتداخلة (Full Hierarchical Tree View)
    /// </summary>
    /// <param name="branchId">فلترة حسب الفرع (اختياري)</param>
    /// <param name="posOnly">تحديد ما إذا كان المطلوب فقط المجموعات المعلمة بنقاط البيع (ShowInPos = true)</param>
    /// <param name="ct">Cancellation token</param>
    /// <returns>شجرة متداخلة تحتوي على كل مجموعة وبداخلها قائمة Children الخاصة بها</returns>
    [HttpGet("tree")]
    [ProducesResponseType(typeof(ApiResponse<List<InvGroupTreeNodeDto>>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetTree(
        [FromQuery] long? branchId,
        [FromQuery] bool? posOnly,
        CancellationToken ct)
    {
        var result = await _groupService.GetTreeAsync(branchId, posOnly, ct);
        return StatusCode(result.StatusCode, result);
    }

    /// <summary>
    /// استرجاع مجموعات نقاط البيع فقط (POS Groups Hierarchy)
    /// </summary>
    /// <remarks>
    /// نقطة نهاية مخصصة ومحسنة لأنظمة الكاشير ونقاط البيع لاسترجاع المجموعات المسموح بعرضها في الـ POS فقط.
    /// </remarks>
    [HttpGet("pos")]
    [ProducesResponseType(typeof(ApiResponse<List<InvGroupTreeNodeDto>>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetPosGroups([FromQuery] long? branchId, CancellationToken ct)
    {
        var result = await _groupService.GetPosGroupsAsync(branchId, ct);
        return StatusCode(result.StatusCode, result);
    }

    /// <summary>
    /// استرجاع تفاصيل مجموعة محددة بواسطة المعرف
    /// </summary>
    [HttpGet("{id:long}")]
    [ProducesResponseType(typeof(ApiResponse<InvItemGroupDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<InvItemGroupDto>), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetGroupById([FromRoute] long id, CancellationToken ct)
    {
        var result = await _groupService.GetByIdAsync(id, ct);
        return StatusCode(result.StatusCode, result);
    }

    /// <summary>
    /// استرجاع المجموعات الفرعية المباشرة لمجموعة محددة (Direct Children)
    /// </summary>
    [HttpGet("{id:long}/children")]
    [ProducesResponseType(typeof(ApiResponse<List<InvGroupTreeNodeDto>>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetChildren([FromRoute] long id, CancellationToken ct)
    {
        var result = await _groupService.GetChildrenAsync(id, ct);
        return StatusCode(result.StatusCode, result);
    }

    /// <summary>
    /// تعديل بيانات مجموعة أو نقلها لأب آخر في الشجرة (مع حماية العلاقات الدائرية)
    /// </summary>
    [HttpPut("{id:long}")]
    [ProducesResponseType(typeof(ApiResponse<InvItemGroupDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<InvItemGroupDto>), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ApiResponse<InvItemGroupDto>), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> UpdateGroup([FromRoute] long id, [FromBody] UpdateInvItemGroupDto dto, CancellationToken ct)
    {
        var username = User.FindFirstValue(ClaimTypes.Name) ?? "SYSTEM";
        var result = await _groupService.UpdateGroupAsync(id, dto, username, ct);
        return StatusCode(result.StatusCode, result);
    }

    /// <summary>
    /// حذف مجموعة أصناف (محمي ضد الحذف إذا كان يتبعها مجموعات فرعية أو أصناف)
    /// </summary>
    [HttpDelete("{id:long}")]
    [ProducesResponseType(typeof(ApiResponse<bool>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<bool>), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ApiResponse<bool>), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> DeleteGroup([FromRoute] long id, CancellationToken ct)
    {
        var result = await _groupService.DeleteGroupAsync(id, ct);
        return StatusCode(result.StatusCode, result);
    }

    #endregion

    #region Hierarchy & Paginated View

    /// <summary>
    /// استرجاع كافة المجموعات مع تقسيم الصفحات (Paginated Groups)
    /// </summary>
    [HttpGet]
    [ProducesResponseType(typeof(ApiResponse<PagedResultDto<InvItemGroupDto>>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetAllGroups(
        [FromQuery] long? branchId,
        [FromQuery] int pageIndex = 1,
        [FromQuery] int pageSize = 50,
        CancellationToken ct = default)
    {
        var result = await _groupService.GetAllPagedAsync(branchId, pageIndex, pageSize, ct);
        return StatusCode(result.StatusCode, result);
    }

    #endregion
}
