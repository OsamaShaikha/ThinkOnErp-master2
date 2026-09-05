using System;
using System.Linq;
using ThinkOnErp.Application.DTOs.Inventory.ItemGroups;
using ThinkOnErp.Domain.Entities.Inventory;

namespace ThinkOnErp.Application.Mappings.Inventory;

public static class InvItemGroupMapper
{
    public static InvItemGroup ToEntity(CreateInvItemGroupDto dto, string username)
    {
        return new InvItemGroup
        {
            BranchId = dto.BranchId,
            ParentGroupId = dto.ParentGroupId,
            GroupCode = dto.GroupCode,
            GroupNameLocal = dto.GroupNameLocal.Trim(),
            GroupNameEn = dto.GroupNameEn?.Trim(),
            GroupLevel = dto.ParentGroupId.HasValue ? 2 : 1,
            GlControlAccount = dto.GlControlAccount?.Trim(),
            GlCogsAccount = dto.GlCogsAccount?.Trim(),
            GlRevenueAccount = dto.GlRevenueAccount?.Trim(),
            GlAdjustmentAccount = dto.GlAdjustmentAccount?.Trim(),
            CreationUser = username,
            CreationDate = DateTime.UtcNow,
            IsActive = true
        };
    }

    public static InvItemGroup ToEntity(CreateMainGroupDto dto, string username)
    {
        return new InvItemGroup
        {
            BranchId = dto.BranchId,
            ParentGroupId = null,
            GroupCode = dto.GroupCode,
            GroupNameLocal = dto.GroupNameLocal.Trim(),
            GroupNameEn = dto.GroupNameEn?.Trim(),
            GroupLevel = 1,
            GlControlAccount = dto.GlControlAccount?.Trim(),
            GlCogsAccount = dto.GlCogsAccount?.Trim(),
            GlRevenueAccount = dto.GlRevenueAccount?.Trim(),
            GlAdjustmentAccount = dto.GlAdjustmentAccount?.Trim(),
            CreationUser = username,
            CreationDate = DateTime.UtcNow,
            IsActive = true
        };
    }

    public static InvItemGroup ToEntity(CreateSubGroupDto dto, string username)
    {
        return new InvItemGroup
        {
            BranchId = dto.BranchId,
            ParentGroupId = dto.MainGroupId,
            GroupCode = dto.GroupCode,
            GroupNameLocal = dto.GroupNameLocal.Trim(),
            GroupNameEn = dto.GroupNameEn?.Trim(),
            GroupLevel = 2,
            GlControlAccount = dto.GlControlAccount?.Trim(),
            GlCogsAccount = dto.GlCogsAccount?.Trim(),
            GlRevenueAccount = dto.GlRevenueAccount?.Trim(),
            GlAdjustmentAccount = dto.GlAdjustmentAccount?.Trim(),
            CreationUser = username,
            CreationDate = DateTime.UtcNow,
            IsActive = true
        };
    }

    public static InvItemGroupDto ToDto(InvItemGroup entity)
    {
        return new InvItemGroupDto
        {
            Id = entity.Id,
            BranchId = entity.BranchId,
            ParentGroupId = entity.ParentGroupId,
            GroupCode = entity.GroupCode,
            GroupNameLocal = entity.GroupNameLocal,
            GroupNameEn = entity.GroupNameEn,
            GroupLevel = entity.GroupLevel,
            GlControlAccount = entity.GlControlAccount,
            GlCogsAccount = entity.GlCogsAccount,
            GlRevenueAccount = entity.GlRevenueAccount,
            GlAdjustmentAccount = entity.GlAdjustmentAccount,
            IsActive = entity.IsActive,
            SubGroups = entity.SubGroups?.Select(ToDto).ToList() ?? new()
        };
    }

    public static InvMainGroupDto ToMainGroupDto(InvItemGroup entity, int itemsCount = 0)
    {
        return new InvMainGroupDto
        {
            Id = entity.Id,
            BranchId = entity.BranchId,
            GroupCode = entity.GroupCode,
            GroupNameLocal = entity.GroupNameLocal,
            GroupNameEn = entity.GroupNameEn,
            GroupLevel = 1,
            GlControlAccount = entity.GlControlAccount,
            GlCogsAccount = entity.GlCogsAccount,
            GlRevenueAccount = entity.GlRevenueAccount,
            GlAdjustmentAccount = entity.GlAdjustmentAccount,
            IsActive = entity.IsActive,
            SubGroupsCount = entity.SubGroups?.Count ?? 0,
            ItemsCount = itemsCount,
            SubGroups = entity.SubGroups?.Select(s => ToSubGroupDto(s)).ToList() ?? new()
        };
    }

    public static InvSubGroupDto ToSubGroupDto(InvItemGroup entity, int itemsCount = 0)
    {
        return new InvSubGroupDto
        {
            Id = entity.Id,
            MainGroupId = entity.ParentGroupId ?? 0,
            MainGroupNameLocal = entity.ParentGroup?.GroupNameLocal,
            MainGroupNameEn = entity.ParentGroup?.GroupNameEn,
            BranchId = entity.BranchId,
            GroupCode = entity.GroupCode,
            GroupNameLocal = entity.GroupNameLocal,
            GroupNameEn = entity.GroupNameEn,
            GroupLevel = 2,
            GlControlAccount = entity.GlControlAccount,
            GlCogsAccount = entity.GlCogsAccount,
            GlRevenueAccount = entity.GlRevenueAccount,
            GlAdjustmentAccount = entity.GlAdjustmentAccount,
            IsActive = entity.IsActive,
            ItemsCount = itemsCount
        };
    }
}
