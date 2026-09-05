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
