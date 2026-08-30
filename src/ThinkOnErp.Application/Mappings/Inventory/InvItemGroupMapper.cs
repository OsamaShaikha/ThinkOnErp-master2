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
            GroupNameAr = dto.GroupNameAr,
            GroupNameEn = dto.GroupNameEn,
            GroupLevel = dto.ParentGroupId.HasValue ? 2 : 1,
            GlControlAccount = dto.GlControlAccount,
            GlCogsAccount = dto.GlCogsAccount,
            GlRevenueAccount = dto.GlRevenueAccount,
            GlAdjustmentAccount = dto.GlAdjustmentAccount,
            CreationUser = username,
            CreationDate = System.DateTime.UtcNow,
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
            GroupNameAr = entity.GroupNameAr,
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
}
