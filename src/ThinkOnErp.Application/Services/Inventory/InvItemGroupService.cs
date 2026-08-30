using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using ThinkOnErp.Application.Common;
using ThinkOnErp.Application.DTOs.Inventory.ItemGroups;
using ThinkOnErp.Application.Mappings.Inventory;
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

    public async Task<ApiResponse<InvItemGroupDto>> CreateGroupAsync(CreateInvItemGroupDto dto, string username, CancellationToken ct = default)
    {
        if (await _groupRepository.ExistsAsync(dto.GroupCode.Trim(), null, ct))
            return ApiResponse<InvItemGroupDto>.CreateFailure($"Group code '{dto.GroupCode}' already exists", null, 400);

        var group = InvItemGroupMapper.ToEntity(dto, username);
        var created = await _groupRepository.CreateAsync(group, ct);
        return ApiResponse<InvItemGroupDto>.CreateSuccess(InvItemGroupMapper.ToDto(created), "Item group created successfully", 201);
    }

    public async Task<ApiResponse<InvItemGroupDto>> GetByIdAsync(long id, CancellationToken ct = default)
    {
        var group = await _groupRepository.GetByIdAsync(id, ct);
        if (group == null)
            return ApiResponse<InvItemGroupDto>.CreateFailure("Item group not found", null, 404);

        return ApiResponse<InvItemGroupDto>.CreateSuccess(InvItemGroupMapper.ToDto(group));
    }

    public async Task<ApiResponse<List<InvItemGroupDto>>> GetMainGroupsAsync(long? branchId = null, CancellationToken ct = default)
    {
        var groups = await _groupRepository.GetMainGroupsAsync(branchId, ct);
        return ApiResponse<List<InvItemGroupDto>>.CreateSuccess(groups.Select(InvItemGroupMapper.ToDto).ToList());
    }

    public async Task<ApiResponse<List<InvItemGroupDto>>> GetSubGroupsAsync(long mainGroupId, CancellationToken ct = default)
    {
        var groups = await _groupRepository.GetSubGroupsAsync(mainGroupId, ct);
        return ApiResponse<List<InvItemGroupDto>>.CreateSuccess(groups.Select(InvItemGroupMapper.ToDto).ToList());
    }

    public async Task<ApiResponse<(List<InvItemGroupDto> Items, int TotalCount)>> GetAllPagedAsync(long? branchId, int pageIndex, int pageSize, CancellationToken ct = default)
    {
        var (items, total) = await _groupRepository.GetAllPagedAsync(branchId, pageIndex, pageSize, ct);
        var dtos = items.Select(InvItemGroupMapper.ToDto).ToList();
        return ApiResponse<(List<InvItemGroupDto> Items, int TotalCount)>.CreateSuccess((dtos, total));
    }

    public async Task<ApiResponse<InvItemGroupDto>> UpdateGroupAsync(long id, UpdateInvItemGroupDto dto, string username, CancellationToken ct = default)
    {
        var group = await _groupRepository.GetByIdAsync(id, ct);
        if (group == null)
            return ApiResponse<InvItemGroupDto>.CreateFailure("Item group not found", null, 404);

        if (!string.IsNullOrWhiteSpace(dto.GroupNameAr)) group.GroupNameAr = dto.GroupNameAr.Trim();
        if (dto.GroupNameEn != null) group.GroupNameEn = dto.GroupNameEn.Trim();
        if (dto.ParentGroupId.HasValue) group.ParentGroupId = dto.ParentGroupId;
        if (dto.GlControlAccount != null) group.GlControlAccount = dto.GlControlAccount.Trim();
        if (dto.GlCogsAccount != null) group.GlCogsAccount = dto.GlCogsAccount.Trim();
        if (dto.GlRevenueAccount != null) group.GlRevenueAccount = dto.GlRevenueAccount.Trim();
        if (dto.GlAdjustmentAccount != null) group.GlAdjustmentAccount = dto.GlAdjustmentAccount.Trim();

        group.UpdateUser = username;
        group.UpdateDate = DateTime.UtcNow;

        await _groupRepository.UpdateAsync(group, ct);
        return ApiResponse<InvItemGroupDto>.CreateSuccess(InvItemGroupMapper.ToDto(group), "Item group updated successfully");
    }

    public async Task<ApiResponse<bool>> DeleteGroupAsync(long id, CancellationToken ct = default)
    {
        var group = await _groupRepository.GetByIdAsync(id, ct);
        if (group == null)
            return ApiResponse<bool>.CreateFailure("Item group not found", null, 404);

        await _groupRepository.DeleteAsync(id, ct);
        return ApiResponse<bool>.CreateSuccess(true, "Item group deactivated successfully");
    }
}
