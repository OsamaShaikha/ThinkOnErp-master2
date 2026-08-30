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
using ThinkOnErp.Domain.Constants;

namespace ThinkOnErp.API.Controllers.Inventory;

/// <summary>
/// Item Groups and Categories API: Manages two-tier hierarchical classification (Main Groups and Sub-Groups) with default General Ledger account mappings.
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

    /// <summary>
    /// Creates a new main item group or secondary sub-group.
    /// </summary>
    /// <param name="dto">Group creation payload.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>The created item group.</returns>
    [HttpPost]
    [ProducesResponseType(typeof(ApiResponse<InvItemGroupDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<InvItemGroupDto>), StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<ApiResponse<InvItemGroupDto>>> CreateGroup(
        [FromBody] CreateInvItemGroupDto dto,
        CancellationToken ct)
    {
        if (!ModelState.IsValid)
            return BadRequest(ApiResponse<InvItemGroupDto>.CreateFailure("Validation failed", null));

        var username = User.FindFirstValue(ClaimTypes.Name) ?? "SYSTEM";
        var result = await _groupService.CreateGroupAsync(dto, username, ct);
        if (result.Success)
            result.Message = ResponseCodes.ItemGroupCreated;

        return result.Success ? Ok(result) : BadRequest(result);
    }

    /// <summary>
    /// Retrieves all item groups with pagination.
    /// </summary>
    /// <param name="branchId">Optional branch filter.</param>
    /// <param name="pageIndex">Page index (default 1).</param>
    /// <param name="pageSize">Page size (default 20).</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>Paginated item groups.</returns>
    [HttpGet]
    [ProducesResponseType(typeof(ApiResponse<(List<InvItemGroupDto> Items, int TotalCount)>), StatusCodes.Status200OK)]
    public async Task<ActionResult<ApiResponse<(List<InvItemGroupDto> Items, int TotalCount)>>> GetAllGroups(
        [FromQuery] long? branchId,
        [FromQuery] int pageIndex = 1,
        [FromQuery] int pageSize = 20,
        CancellationToken ct = default)
    {
        var result = await _groupService.GetAllPagedAsync(branchId, pageIndex, pageSize, ct);
        if (result.Success)
            result.Message = ResponseCodes.ItemGroupsRetrieved;

        return Ok(result);
    }

    /// <summary>
    /// Retrieves all active top-level main item groups.
    /// </summary>
    /// <param name="branchId">Optional branch filter.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>List of main item groups.</returns>
    [HttpGet("main")]
    [ProducesResponseType(typeof(ApiResponse<List<InvItemGroupDto>>), StatusCodes.Status200OK)]
    public async Task<ActionResult<ApiResponse<List<InvItemGroupDto>>>> GetMainGroups(
        [FromQuery] long? branchId,
        CancellationToken ct)
    {
        var result = await _groupService.GetMainGroupsAsync(branchId, ct);
        if (result.Success)
            result.Message = ResponseCodes.ItemGroupsRetrieved;

        return Ok(result);
    }

    /// <summary>
    /// Retrieves all sub-groups nested under a specified main group.
    /// </summary>
    /// <param name="mainGroupId">The identifier of the parent main group.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>List of sub-groups.</returns>
    [HttpGet("{mainGroupId:long}/sub")]
    [ProducesResponseType(typeof(ApiResponse<List<InvItemGroupDto>>), StatusCodes.Status200OK)]
    public async Task<ActionResult<ApiResponse<List<InvItemGroupDto>>>> GetSubGroups(
        long mainGroupId,
        CancellationToken ct)
    {
        var result = await _groupService.GetSubGroupsAsync(mainGroupId, ct);
        if (result.Success)
            result.Message = ResponseCodes.ItemGroupsRetrieved;

        return Ok(result);
    }

    /// <summary>
    /// Retrieves details of a specific item group by its ID.
    /// </summary>
    /// <param name="id">Item group identifier.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>The matching item group.</returns>
    [HttpGet("{id:long}")]
    [ProducesResponseType(typeof(ApiResponse<InvItemGroupDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<InvItemGroupDto>), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<ApiResponse<InvItemGroupDto>>> GetGroupById(
        long id,
        CancellationToken ct)
    {
        var result = await _groupService.GetByIdAsync(id, ct);
        if (result.Success)
            result.Message = ResponseCodes.ItemGroupDetailsRetrieved;

        return result.Success ? Ok(result) : NotFound(result);
    }

    /// <summary>
    /// Updates an existing item group.
    /// </summary>
    /// <param name="id">Item group identifier.</param>
    /// <param name="dto">Update payload.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>The updated item group.</returns>
    [HttpPut("{id:long}")]
    [ProducesResponseType(typeof(ApiResponse<InvItemGroupDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<InvItemGroupDto>), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ApiResponse<InvItemGroupDto>), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<ApiResponse<InvItemGroupDto>>> UpdateGroup(
        long id,
        [FromBody] UpdateInvItemGroupDto dto,
        CancellationToken ct)
    {
        var username = User.FindFirstValue(ClaimTypes.Name) ?? "SYSTEM";
        var result = await _groupService.UpdateGroupAsync(id, dto, username, ct);
        if (result.Success)
            result.Message = ResponseCodes.ItemGroupUpdated;

        return result.Success ? Ok(result) : BadRequest(result);
    }

    /// <summary>
    /// Deactivates or deletes an item group.
    /// </summary>
    /// <param name="id">Item group identifier.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>Deletion confirmation.</returns>
    [HttpDelete("{id:long}")]
    [ProducesResponseType(typeof(ApiResponse<bool>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<bool>), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<ApiResponse<bool>>> DeleteGroup(
        long id,
        CancellationToken ct)
    {
        var result = await _groupService.DeleteGroupAsync(id, ct);
        if (result.Success)
            result.Message = ResponseCodes.ItemGroupDeleted;

        return result.Success ? Ok(result) : NotFound(result);
    }
}
