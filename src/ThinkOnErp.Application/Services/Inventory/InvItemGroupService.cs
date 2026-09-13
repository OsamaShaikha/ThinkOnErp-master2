using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using ThinkOnErp.Application.Common;
using ThinkOnErp.Application.DTOs.Inventory.ItemGroups;
using ThinkOnErp.Application.Mappings.Inventory;
using ThinkOnErp.Domain.Constants;
using ThinkOnErp.Domain.Entities.Inventory;
using ThinkOnErp.Domain.Interfaces.Inventory;

namespace ThinkOnErp.Application.Services.Inventory;

public sealed class InvItemGroupService : IInvItemGroupService
{
    private readonly IInvItemGroupRepository _groupRepository;
    private readonly ILogger<InvItemGroupService> _logger;

    public InvItemGroupService(IInvItemGroupRepository groupRepository, ILogger<InvItemGroupService> logger)
    {
        _groupRepository = groupRepository;
        _logger = logger;
    }

    #region Main Groups CRUD

    public async Task<ApiResponse<InvMainGroupDto>> CreateMainGroupAsync(CreateMainGroupDto dto, string username, CancellationToken ct = default)
    {
        if (await _groupRepository.ExistsAsync(dto.GroupCode, null, ct))
            return ApiResponse<InvMainGroupDto>.CreateFailure($"Group code '{dto.GroupCode}' already exists", null, 400);

        var entity = InvItemGroupMapper.ToEntity(dto, username);
        var created = await _groupRepository.CreateAsync(entity, ct);
        return ApiResponse<InvMainGroupDto>.CreateSuccess(InvItemGroupMapper.ToMainGroupDto(created), ResponseCodes.ItemGroupCreated, 201);
    }

    public async Task<ApiResponse<List<InvMainGroupDto>>> GetMainGroupsAsync(long? branchId = null, CancellationToken ct = default)
    {
        var mainGroups = await _groupRepository.GetMainGroupsAsync(branchId, ct);
        var dtos = new List<InvMainGroupDto>(mainGroups.Count);

        foreach (var mg in mainGroups)
        {
            var itemsCount = await _groupRepository.GetItemsCountAsync(mg.Id, true, ct);
            dtos.Add(InvItemGroupMapper.ToMainGroupDto(mg, itemsCount));
        }

        return ApiResponse<List<InvMainGroupDto>>.CreateSuccess(dtos, ResponseCodes.ItemGroupsRetrieved);
    }

    public async Task<ApiResponse<InvMainGroupDto>> GetMainGroupByIdAsync(long id, CancellationToken ct = default)
    {
        var group = await _groupRepository.GetByIdAsync(id, ct);
        if (group == null || group.GroupLevel != 1)
            return ApiResponse<InvMainGroupDto>.CreateFailure("Main item group not found", null, 404);

        var itemsCount = await _groupRepository.GetItemsCountAsync(group.Id, true, ct);
        return ApiResponse<InvMainGroupDto>.CreateSuccess(InvItemGroupMapper.ToMainGroupDto(group, itemsCount), ResponseCodes.ItemGroupDetailsRetrieved);
    }

    public async Task<ApiResponse<InvMainGroupDto>> UpdateMainGroupAsync(long id, UpdateMainGroupDto dto, string username, CancellationToken ct = default)
    {
        var group = await _groupRepository.GetByIdAsync(id, ct);
        if (group == null || group.GroupLevel != 1)
            return ApiResponse<InvMainGroupDto>.CreateFailure("Main item group not found", null, 404);

        group.GroupNameLocal = dto.GroupNameLocal.Trim();
        group.GroupNameEn = dto.GroupNameEn?.Trim();
        group.GlControlAccount = dto.GlControlAccount?.Trim();
        group.GlCogsAccount = dto.GlCogsAccount?.Trim();
        group.GlRevenueAccount = dto.GlRevenueAccount?.Trim();
        group.GlAdjustmentAccount = dto.GlAdjustmentAccount?.Trim();
        group.ShowInPos = dto.ShowInPos;
        group.IsActive = dto.IsActive;
        group.UpdateUser = username;
        group.UpdateDate = DateTime.UtcNow;

        await _groupRepository.UpdateAsync(group, ct);

        var itemsCount = await _groupRepository.GetItemsCountAsync(group.Id, true, ct);
        return ApiResponse<InvMainGroupDto>.CreateSuccess(InvItemGroupMapper.ToMainGroupDto(group, itemsCount), ResponseCodes.ItemGroupUpdated);
    }

    public async Task<ApiResponse<bool>> DeleteMainGroupAsync(long id, CancellationToken ct = default)
    {
        var group = await _groupRepository.GetByIdAsync(id, ct);
        if (group == null || group.GroupLevel != 1)
            return ApiResponse<bool>.CreateFailure("Main item group not found", null, 404);

        if (await _groupRepository.HasSubGroupsAsync(id, ct))
            return ApiResponse<bool>.CreateFailure("Cannot delete main group because it contains active sub-groups. Delete or reassign sub-groups first.", null, 400);

        if (await _groupRepository.HasItemsAsync(id, true, ct))
            return ApiResponse<bool>.CreateFailure("Cannot delete main group because it has items assigned to it.", null, 400);

        await _groupRepository.DeleteAsync(id, ct);
        return ApiResponse<bool>.CreateSuccess(true, ResponseCodes.ItemGroupDeleted);
    }

    #endregion

    #region Sub Groups CRUD

    public async Task<ApiResponse<InvSubGroupDto>> CreateSubGroupAsync(CreateSubGroupDto dto, string username, CancellationToken ct = default)
    {
        var parentGroup = await _groupRepository.GetByIdAsync(dto.MainGroupId, ct);
        if (parentGroup == null)
            return ApiResponse<InvSubGroupDto>.CreateFailure($"Parent group {dto.MainGroupId} not found", null, 400);

        if (await _groupRepository.ExistsAsync(dto.GroupCode, null, ct))
            return ApiResponse<InvSubGroupDto>.CreateFailure($"Group code '{dto.GroupCode}' already exists", null, 400);

        var entity = InvItemGroupMapper.ToEntity(dto, username);
        entity.GroupLevel = parentGroup.GroupLevel + 1;
        var created = await _groupRepository.CreateAsync(entity, ct);
        created.ParentGroup = parentGroup;

        return ApiResponse<InvSubGroupDto>.CreateSuccess(InvItemGroupMapper.ToSubGroupDto(created), ResponseCodes.ItemGroupCreated, 201);
    }

    public async Task<ApiResponse<List<InvSubGroupDto>>> GetAllSubGroupsAsync(long? branchId = null, CancellationToken ct = default)
    {
        var subGroups = await _groupRepository.GetAllSubGroupsAsync(branchId, ct);
        var dtos = new List<InvSubGroupDto>(subGroups.Count);

        foreach (var sg in subGroups)
        {
            var itemsCount = await _groupRepository.GetItemsCountAsync(sg.Id, false, ct);
            dtos.Add(InvItemGroupMapper.ToSubGroupDto(sg, itemsCount));
        }

        return ApiResponse<List<InvSubGroupDto>>.CreateSuccess(dtos, ResponseCodes.ItemGroupsRetrieved);
    }

    public async Task<ApiResponse<List<InvSubGroupDto>>> GetSubGroupsByMainGroupIdAsync(long mainGroupId, CancellationToken ct = default)
    {
        var subGroups = await _groupRepository.GetSubGroupsAsync(mainGroupId, ct);
        var dtos = new List<InvSubGroupDto>(subGroups.Count);

        foreach (var sg in subGroups)
        {
            var itemsCount = await _groupRepository.GetItemsCountAsync(sg.Id, false, ct);
            dtos.Add(InvItemGroupMapper.ToSubGroupDto(sg, itemsCount));
        }

        return ApiResponse<List<InvSubGroupDto>>.CreateSuccess(dtos, ResponseCodes.ItemGroupsRetrieved);
    }

    public async Task<ApiResponse<InvSubGroupDto>> GetSubGroupByIdAsync(long id, CancellationToken ct = default)
    {
        var group = await _groupRepository.GetByIdAsync(id, ct);
        if (group == null || group.GroupLevel < 2)
            return ApiResponse<InvSubGroupDto>.CreateFailure("Sub-group not found", null, 404);

        var itemsCount = await _groupRepository.GetItemsCountAsync(group.Id, false, ct);
        return ApiResponse<InvSubGroupDto>.CreateSuccess(InvItemGroupMapper.ToSubGroupDto(group, itemsCount), ResponseCodes.ItemGroupDetailsRetrieved);
    }

    public async Task<ApiResponse<InvSubGroupDto>> UpdateSubGroupAsync(long id, UpdateSubGroupDto dto, string username, CancellationToken ct = default)
    {
        var group = await _groupRepository.GetByIdAsync(id, ct);
        if (group == null || group.GroupLevel < 2)
            return ApiResponse<InvSubGroupDto>.CreateFailure("Sub-group not found", null, 404);

        if (dto.MainGroupId.HasValue && dto.MainGroupId.Value != group.ParentGroupId)
        {
            if (dto.MainGroupId.Value == id)
                return ApiResponse<InvSubGroupDto>.CreateFailure("Cannot set a group as its own parent", null, 400);

            if (await _groupRepository.IsDescendantOfAsync(dto.MainGroupId.Value, id, ct))
                return ApiResponse<InvSubGroupDto>.CreateFailure("Cannot move group under one of its own descendants (circular reference)", null, 400);

            var newParent = await _groupRepository.GetByIdAsync(dto.MainGroupId.Value, ct);
            if (newParent == null)
                return ApiResponse<InvSubGroupDto>.CreateFailure($"New parent group {dto.MainGroupId.Value} not found", null, 400);

            group.ParentGroupId = dto.MainGroupId.Value;
            group.ParentGroup = newParent;
            group.GroupLevel = newParent.GroupLevel + 1;
        }

        group.GroupNameLocal = dto.GroupNameLocal.Trim();
        group.GroupNameEn = dto.GroupNameEn?.Trim();
        group.GlControlAccount = dto.GlControlAccount?.Trim();
        group.GlCogsAccount = dto.GlCogsAccount?.Trim();
        group.GlRevenueAccount = dto.GlRevenueAccount?.Trim();
        group.GlAdjustmentAccount = dto.GlAdjustmentAccount?.Trim();
        group.ShowInPos = dto.ShowInPos;
        group.IsActive = dto.IsActive;
        group.UpdateUser = username;
        group.UpdateDate = DateTime.UtcNow;

        await _groupRepository.UpdateAsync(group, ct);

        var itemsCount = await _groupRepository.GetItemsCountAsync(group.Id, false, ct);
        return ApiResponse<InvSubGroupDto>.CreateSuccess(InvItemGroupMapper.ToSubGroupDto(group, itemsCount), ResponseCodes.ItemGroupUpdated);
    }

    public async Task<ApiResponse<bool>> DeleteSubGroupAsync(long id, CancellationToken ct = default)
    {
        var group = await _groupRepository.GetByIdAsync(id, ct);
        if (group == null || group.GroupLevel < 2)
            return ApiResponse<bool>.CreateFailure("Sub-group not found", null, 404);

        if (await _groupRepository.HasSubGroupsAsync(id, ct))
            return ApiResponse<bool>.CreateFailure("Cannot delete sub-group because it contains nested sub-groups. Delete or reassign them first.", null, 400);

        if (await _groupRepository.HasItemsAsync(id, false, ct))
            return ApiResponse<bool>.CreateFailure("Cannot delete sub-group because it has items assigned to it.", null, 400);

        await _groupRepository.DeleteAsync(id, ct);
        return ApiResponse<bool>.CreateSuccess(true, ResponseCodes.ItemGroupDeleted);
    }

    #endregion

    #region Generic & Multi-Level Tree CRUD

    public async Task<ApiResponse<InvItemGroupDto>> CreateGroupAsync(CreateInvItemGroupDto dto, string username, CancellationToken ct = default)
    {
        if (await _groupRepository.ExistsAsync(dto.GroupCode, null, ct))
            return ApiResponse<InvItemGroupDto>.CreateFailure($"Group code '{dto.GroupCode}' already exists", null, 400);

        int level = 1;
        if (dto.ParentGroupId.HasValue)
        {
            var parent = await _groupRepository.GetByIdAsync(dto.ParentGroupId.Value, ct);
            if (parent == null)
                return ApiResponse<InvItemGroupDto>.CreateFailure($"Parent group {dto.ParentGroupId.Value} not found", null, 400);

            level = parent.GroupLevel + 1;
        }

        var group = InvItemGroupMapper.ToEntity(dto, username);
        group.GroupLevel = level;
        var created = await _groupRepository.CreateAsync(group, ct);
        return ApiResponse<InvItemGroupDto>.CreateSuccess(InvItemGroupMapper.ToDto(created), ResponseCodes.ItemGroupCreated, 201);
    }

    public async Task<ApiResponse<InvItemGroupDto>> GetByIdAsync(long id, CancellationToken ct = default)
    {
        var group = await _groupRepository.GetByIdAsync(id, ct);
        if (group == null)
            return ApiResponse<InvItemGroupDto>.CreateFailure("Item group not found", null, 404);

        return ApiResponse<InvItemGroupDto>.CreateSuccess(InvItemGroupMapper.ToDto(group), ResponseCodes.ItemGroupDetailsRetrieved);
    }

    public async Task<ApiResponse<List<InvItemGroupDto>>> GetSubGroupsAsync(long mainGroupId, CancellationToken ct = default)
    {
        var groups = await _groupRepository.GetSubGroupsAsync(mainGroupId, ct);
        return ApiResponse<List<InvItemGroupDto>>.CreateSuccess(groups.Select(InvItemGroupMapper.ToDto).ToList(), ResponseCodes.ItemGroupsRetrieved);
    }

    public async Task<ApiResponse<PagedResultDto<InvItemGroupDto>>> GetAllPagedAsync(long? branchId, int pageIndex, int pageSize, CancellationToken ct = default)
    {
        var (items, total) = await _groupRepository.GetAllPagedAsync(branchId, pageIndex, pageSize, ct);
        var dtos = items.Select(InvItemGroupMapper.ToDto).ToList();
        var pagedResult = new PagedResultDto<InvItemGroupDto>(dtos, total, pageIndex, pageSize);
        return ApiResponse<PagedResultDto<InvItemGroupDto>>.CreateSuccess(pagedResult, ResponseCodes.ItemGroupsRetrieved);
    }

    public async Task<ApiResponse<InvItemGroupDto>> UpdateGroupAsync(long id, UpdateInvItemGroupDto dto, string username, CancellationToken ct = default)
    {
        var group = await _groupRepository.GetByIdAsync(id, ct);
        if (group == null)
            return ApiResponse<InvItemGroupDto>.CreateFailure("Item group not found", null, 404);

        if (dto.ParentGroupId.HasValue && dto.ParentGroupId.Value != group.ParentGroupId)
        {
            if (dto.ParentGroupId.Value == id)
                return ApiResponse<InvItemGroupDto>.CreateFailure("Cannot set a group as its own parent", null, 400);

            if (await _groupRepository.IsDescendantOfAsync(dto.ParentGroupId.Value, id, ct))
                return ApiResponse<InvItemGroupDto>.CreateFailure("Cannot move group under one of its own descendants (circular reference)", null, 400);

            var newParent = await _groupRepository.GetByIdAsync(dto.ParentGroupId.Value, ct);
            if (newParent == null)
                return ApiResponse<InvItemGroupDto>.CreateFailure($"Parent group {dto.ParentGroupId.Value} not found", null, 400);

            group.ParentGroupId = dto.ParentGroupId.Value;
            group.GroupLevel = newParent.GroupLevel + 1;
        }
        else if (dto.ParentGroupId == null && group.ParentGroupId != null)
        {
            group.ParentGroupId = null;
            group.GroupLevel = 1;
        }

        if (!string.IsNullOrWhiteSpace(dto.GroupNameLocal)) group.GroupNameLocal = dto.GroupNameLocal.Trim();
        if (dto.GroupNameEn != null) group.GroupNameEn = dto.GroupNameEn.Trim();
        if (dto.GlControlAccount != null) group.GlControlAccount = dto.GlControlAccount.Trim();
        if (dto.GlCogsAccount != null) group.GlCogsAccount = dto.GlCogsAccount.Trim();
        if (dto.GlRevenueAccount != null) group.GlRevenueAccount = dto.GlRevenueAccount.Trim();
        if (dto.GlAdjustmentAccount != null) group.GlAdjustmentAccount = dto.GlAdjustmentAccount.Trim();
        if (dto.ShowInPos.HasValue) group.ShowInPos = dto.ShowInPos.Value;
        if (dto.IsActive.HasValue) group.IsActive = dto.IsActive.Value;

        group.UpdateUser = username;
        group.UpdateDate = DateTime.UtcNow;

        await _groupRepository.UpdateAsync(group, ct);
        return ApiResponse<InvItemGroupDto>.CreateSuccess(InvItemGroupMapper.ToDto(group), ResponseCodes.ItemGroupUpdated);
    }

    public async Task<ApiResponse<bool>> DeleteGroupAsync(long id, CancellationToken ct = default)
    {
        var group = await _groupRepository.GetByIdAsync(id, ct);
        if (group == null)
            return ApiResponse<bool>.CreateFailure("Item group not found", null, 404);

        if (await _groupRepository.HasSubGroupsAsync(id, ct))
            return ApiResponse<bool>.CreateFailure("Cannot delete group because it contains sub-groups. Delete or reassign sub-groups first.", null, 400);

        if (await _groupRepository.HasItemsAsync(id, true, ct) || await _groupRepository.HasItemsAsync(id, false, ct))
            return ApiResponse<bool>.CreateFailure("Cannot delete group because it has items assigned to it.", null, 400);

        await _groupRepository.DeleteAsync(id, ct);
        return ApiResponse<bool>.CreateSuccess(true, ResponseCodes.ItemGroupDeleted);
    }

    #endregion

    #region Tree Hierarchy & POS

    public async Task<ApiResponse<List<InvGroupTreeNodeDto>>> GetTreeAsync(long? branchId = null, bool? posOnly = null, CancellationToken ct = default)
    {
        var allGroups = await _groupRepository.GetAllActiveGroupsAsync(branchId, posOnly, ct);
        var itemCounts = new Dictionary<long, int>();
        foreach (var g in allGroups)
        {
            itemCounts[g.Id] = await _groupRepository.GetGroupItemsCountAsync(g.Id, ct);
        }

        var tree = InvItemGroupMapper.BuildTree(allGroups, itemCounts);
        return ApiResponse<List<InvGroupTreeNodeDto>>.CreateSuccess(tree, ResponseCodes.ItemGroupsRetrieved);
    }

    public async Task<ApiResponse<List<InvGroupTreeNodeDto>>> GetPosGroupsAsync(long? branchId = null, CancellationToken ct = default)
    {
        return await GetTreeAsync(branchId, true, ct);
    }

    public async Task<ApiResponse<List<InvGroupTreeNodeDto>>> GetChildrenAsync(long parentGroupId, CancellationToken ct = default)
    {
        var children = await _groupRepository.GetChildrenAsync(parentGroupId, ct);
        var dtos = new List<InvGroupTreeNodeDto>(children.Count);
        foreach (var c in children)
        {
            var count = await _groupRepository.GetGroupItemsCountAsync(c.Id, ct);
            dtos.Add(InvItemGroupMapper.ToTreeNodeDto(c, count));
        }
        return ApiResponse<List<InvGroupTreeNodeDto>>.CreateSuccess(dtos, ResponseCodes.ItemGroupsRetrieved);
    }

    #endregion
}
