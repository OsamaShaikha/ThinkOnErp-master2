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
using ThinkOnErp.Application.DTOs.Pos;
using ThinkOnErp.Application.Services.Pos;

namespace ThinkOnErp.API.Controllers.Pos;

/// <summary>
/// Point of Sale Modifiers API: Manages modifier groups (e.g., sizes, toppings, cooking preferences), options with price adjustments, item associations, and validation rules (BRD Section 4.5).
/// </summary>
[ApiController]
[Route("api/pos/modifiers")]
[ApiExplorerSettings(GroupName = ApiCategories.Pos)]
[TenantScoped]
[Authorize]
public class PosModifiersController : ControllerBase
{
    private readonly IPosModifierService _modifierService;

    public PosModifiersController(IPosModifierService modifierService)
    {
        _modifierService = modifierService;
    }

    /// <summary>
    /// Retrieves all modifier groups for a branch with optional active filter.
    /// </summary>
    [HttpGet("groups")]
    [ProducesResponseType(typeof(ApiResponse<IReadOnlyList<PosModifierGroupDto>>), StatusCodes.Status200OK)]
    public async Task<ActionResult<ApiResponse<IReadOnlyList<PosModifierGroupDto>>>> GetGroups(
        [FromQuery] long branchId,
        [FromQuery] bool? isActive,
        CancellationToken ct)
    {
        var result = await _modifierService.GetGroupsByBranchAsync(branchId, isActive, ct);
        return Ok(result);
    }

    /// <summary>
    /// Retrieves a specific modifier group by ID with its options and linked items.
    /// </summary>
    [HttpGet("groups/{id:long}")]
    [ProducesResponseType(typeof(ApiResponse<PosModifierGroupDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<PosModifierGroupDto>), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<ApiResponse<PosModifierGroupDto>>> GetGroupById(
        long id,
        CancellationToken ct)
    {
        var result = await _modifierService.GetGroupByIdAsync(id, ct);
        return result.Success ? Ok(result) : NotFound(result);
    }

    /// <summary>
    /// Creates a new modifier group with optional options and linked items.
    /// </summary>
    [HttpPost("groups")]
    [ProducesResponseType(typeof(ApiResponse<PosModifierGroupDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<PosModifierGroupDto>), StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<ApiResponse<PosModifierGroupDto>>> CreateGroup(
        [FromBody] CreatePosModifierGroupDto dto,
        CancellationToken ct)
    {
        var username = User.FindFirstValue(ClaimTypes.Name) ?? "SYSTEM";
        var result = await _modifierService.CreateGroupAsync(dto, username, ct);
        return result.Success ? Ok(result) : (result.StatusCode == 404 ? NotFound(result) : BadRequest(result));
    }

    /// <summary>
    /// Updates an existing modifier group's metadata and validation rules.
    /// </summary>
    [HttpPut("groups/{id:long}")]
    [ProducesResponseType(typeof(ApiResponse<PosModifierGroupDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<PosModifierGroupDto>), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ApiResponse<PosModifierGroupDto>), StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<ApiResponse<PosModifierGroupDto>>> UpdateGroup(
        long id,
        [FromBody] UpdatePosModifierGroupDto dto,
        CancellationToken ct)
    {
        var username = User.FindFirstValue(ClaimTypes.Name) ?? "SYSTEM";
        var result = await _modifierService.UpdateGroupAsync(id, dto, username, ct);
        return result.Success ? Ok(result) : (result.StatusCode == 404 ? NotFound(result) : BadRequest(result));
    }

    /// <summary>
    /// Soft-deactivates a modifier group.
    /// </summary>
    [HttpDelete("groups/{id:long}")]
    [ProducesResponseType(typeof(ApiResponse<bool>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<bool>), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<ApiResponse<bool>>> DeleteGroup(
        long id,
        CancellationToken ct)
    {
        var result = await _modifierService.DeleteGroupAsync(id, ct);
        return result.Success ? Ok(result) : NotFound(result);
    }

    /// <summary>
    /// Adds or updates an individual modifier option within a group.
    /// </summary>
    [HttpPost("groups/{id:long}/options")]
    [ProducesResponseType(typeof(ApiResponse<PosModifierOptionDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<PosModifierOptionDto>), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ApiResponse<PosModifierOptionDto>), StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<ApiResponse<PosModifierOptionDto>>> UpsertOption(
        long id,
        [FromBody] UpsertPosModifierOptionDto dto,
        CancellationToken ct)
    {
        var result = await _modifierService.UpsertOptionAsync(id, dto, ct);
        return result.Success ? Ok(result) : (result.StatusCode == 404 ? NotFound(result) : BadRequest(result));
    }

    /// <summary>
    /// Deletes a modifier option from a group.
    /// </summary>
    [HttpDelete("groups/{id:long}/options/{optionId:long}")]
    [ProducesResponseType(typeof(ApiResponse<bool>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<bool>), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<ApiResponse<bool>>> DeleteOption(
        long id,
        long optionId,
        CancellationToken ct)
    {
        var result = await _modifierService.DeleteOptionAsync(id, optionId, ct);
        return result.Success ? Ok(result) : NotFound(result);
    }

    /// <summary>
    /// Retrieves all modifier groups linked to a specific item.
    /// </summary>
    [HttpGet("items/{itemId:long}")]
    [ProducesResponseType(typeof(ApiResponse<ItemModifierGroupsDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<ItemModifierGroupsDto>), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<ApiResponse<ItemModifierGroupsDto>>> GetGroupsByItem(
        long itemId,
        CancellationToken ct)
    {
        var result = await _modifierService.GetGroupsByItemIdAsync(itemId, ct);
        return result.Success ? Ok(result) : NotFound(result);
    }

    /// <summary>
    /// Assigns multiple modifier groups to an item.
    /// </summary>
    [HttpPost("items/{itemId:long}/assign")]
    [ProducesResponseType(typeof(ApiResponse<bool>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<bool>), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<ApiResponse<bool>>> AssignGroupsToItem(
        long itemId,
        [FromBody] AssignItemModifierGroupsDto dto,
        CancellationToken ct)
    {
        var result = await _modifierService.AssignGroupsToItemAsync(itemId, dto?.GroupIds ?? new List<long>(), ct);
        return result.Success ? Ok(result) : (result.StatusCode == 404 ? NotFound(result) : BadRequest(result));
    }

    /// <summary>
    /// Removes a modifier group association from an item.
    /// </summary>
    [HttpDelete("items/{itemId:long}/groups/{groupId:long}")]
    [ProducesResponseType(typeof(ApiResponse<bool>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<bool>), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<ApiResponse<bool>>> RemoveGroupFromItem(
        long itemId,
        long groupId,
        CancellationToken ct)
    {
        var result = await _modifierService.RemoveGroupFromItemAsync(itemId, groupId, ct);
        return result.Success ? Ok(result) : NotFound(result);
    }
}
