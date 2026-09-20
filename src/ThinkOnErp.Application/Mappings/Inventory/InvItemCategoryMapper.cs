using System;
using System.Collections.Generic;
using System.Linq;
using ThinkOnErp.Application.DTOs.Inventory.ItemCategories;
using ThinkOnErp.Domain.Entities.Inventory;

namespace ThinkOnErp.Application.Mappings.Inventory;

public static class InvItemCategoryMapper
{
    public static InvItemCategory ToEntity(CreateInvItemCategoryDto dto, string username)
    {
        var parentId = (dto.ParentCategoryId.HasValue && dto.ParentCategoryId.Value > 0) ? dto.ParentCategoryId : null;
        return new InvItemCategory
        {
            BranchId = dto.BranchId,
            ParentCategoryId = parentId,
            CategoryCode = dto.CategoryCode,
            CategoryNameLocal = dto.CategoryNameLocal.Trim(),
            CategoryNameEn = dto.CategoryNameEn?.Trim(),
            CategoryLevel = parentId.HasValue ? 2 : 1,
            GlControlAccount = dto.GlControlAccount?.Trim(),
            GlCogsAccount = dto.GlCogsAccount?.Trim(),
            GlRevenueAccount = dto.GlRevenueAccount?.Trim(),
            GlAdjustmentAccount = dto.GlAdjustmentAccount?.Trim(),
            ImageBase64 = dto.ImageBase64,
            ColorCode = dto.ColorCode,
            ShowInPos = dto.ShowInPos,
            CreationUser = username,
            CreationDate = DateTime.UtcNow,
            IsActive = true
        };
    }

    public static InvItemCategory ToEntity(CreateMainCategoryDto dto, string username)
    {
        return new InvItemCategory
        {
            BranchId = dto.BranchId,
            ParentCategoryId = null,
            CategoryCode = dto.CategoryCode,
            CategoryNameLocal = dto.CategoryNameLocal.Trim(),
            CategoryNameEn = dto.CategoryNameEn?.Trim(),
            CategoryLevel = 1,
            GlControlAccount = dto.GlControlAccount?.Trim(),
            GlCogsAccount = dto.GlCogsAccount?.Trim(),
            GlRevenueAccount = dto.GlRevenueAccount?.Trim(),
            GlAdjustmentAccount = dto.GlAdjustmentAccount?.Trim(),
            ImageBase64 = dto.ImageBase64,
            ColorCode = dto.ColorCode,
            ShowInPos = dto.ShowInPos,
            CreationUser = username,
            CreationDate = DateTime.UtcNow,
            IsActive = true
        };
    }

    public static InvItemCategory ToEntity(CreateSubCategoryDto dto, string username)
    {
        return new InvItemCategory
        {
            BranchId = dto.BranchId,
            ParentCategoryId = dto.MainCategoryId,
            CategoryCode = dto.CategoryCode,
            CategoryNameLocal = dto.CategoryNameLocal.Trim(),
            CategoryNameEn = dto.CategoryNameEn?.Trim(),
            CategoryLevel = 2,
            GlControlAccount = dto.GlControlAccount?.Trim(),
            GlCogsAccount = dto.GlCogsAccount?.Trim(),
            GlRevenueAccount = dto.GlRevenueAccount?.Trim(),
            GlAdjustmentAccount = dto.GlAdjustmentAccount?.Trim(),
            ImageBase64 = dto.ImageBase64,
            ColorCode = dto.ColorCode,
            ShowInPos = dto.ShowInPos,
            CreationUser = username,
            CreationDate = DateTime.UtcNow,
            IsActive = true
        };
    }

    public static InvItemCategoryDto ToDto(InvItemCategory entity, int itemsCount = 0)
    {
        return new InvItemCategoryDto
        {
            Id = entity.Id,
            BranchId = entity.BranchId,
            ParentCategoryId = entity.ParentCategoryId,
            ParentCategoryName = entity.ParentCategory?.CategoryNameLocal,
            IsMainCategory = !entity.ParentCategoryId.HasValue,
            IsSubCategory = entity.ParentCategoryId.HasValue,
            CategoryCode = entity.CategoryCode,
            CategoryNameLocal = entity.CategoryNameLocal,
            CategoryNameEn = entity.CategoryNameEn,
            CategoryLevel = entity.CategoryLevel,
            GlControlAccount = entity.GlControlAccount,
            GlCogsAccount = entity.GlCogsAccount,
            GlRevenueAccount = entity.GlRevenueAccount,
            GlAdjustmentAccount = entity.GlAdjustmentAccount,
            ImageBase64 = entity.ImageBase64,
            ColorCode = entity.ColorCode,
            ShowInPos = entity.ShowInPos,
            IsActive = entity.IsActive,
            ItemsCount = itemsCount,
            SubCategories = entity.SubCategories?.Select(s => ToDto(s, 0)).ToList() ?? new()
        };
    }

    public static InvMainCategoryDto ToMainCategoryDto(InvItemCategory entity, int itemsCount = 0)
    {
        return new InvMainCategoryDto
        {
            Id = entity.Id,
            BranchId = entity.BranchId,
            CategoryCode = entity.CategoryCode,
            CategoryNameLocal = entity.CategoryNameLocal,
            CategoryNameEn = entity.CategoryNameEn,
            CategoryLevel = 1,
            GlControlAccount = entity.GlControlAccount,
            GlCogsAccount = entity.GlCogsAccount,
            GlRevenueAccount = entity.GlRevenueAccount,
            GlAdjustmentAccount = entity.GlAdjustmentAccount,
            ImageBase64 = entity.ImageBase64,
            ColorCode = entity.ColorCode,
            ShowInPos = entity.ShowInPos,
            IsActive = entity.IsActive,
            SubCategoriesCount = entity.SubCategories?.Count ?? 0,
            ItemsCount = itemsCount,
            SubCategories = entity.SubCategories?.Select(s => ToSubCategoryDto(s)).ToList() ?? new()
        };
    }

    public static InvSubCategoryDto ToSubCategoryDto(InvItemCategory entity, int itemsCount = 0)
    {
        return new InvSubCategoryDto
        {
            Id = entity.Id,
            MainCategoryId = entity.ParentCategoryId ?? 0,
            MainCategoryNameLocal = entity.ParentCategory?.CategoryNameLocal,
            MainCategoryNameEn = entity.ParentCategory?.CategoryNameEn,
            BranchId = entity.BranchId,
            CategoryCode = entity.CategoryCode,
            CategoryNameLocal = entity.CategoryNameLocal,
            CategoryNameEn = entity.CategoryNameEn,
            CategoryLevel = entity.CategoryLevel,
            GlControlAccount = entity.GlControlAccount,
            GlCogsAccount = entity.GlCogsAccount,
            GlRevenueAccount = entity.GlRevenueAccount,
            GlAdjustmentAccount = entity.GlAdjustmentAccount,
            ImageBase64 = entity.ImageBase64,
            ColorCode = entity.ColorCode,
            ShowInPos = entity.ShowInPos,
            IsActive = entity.IsActive,
            ItemsCount = itemsCount
        };
    }

    public static InvCategoryTreeNodeDto ToTreeNodeDto(InvItemCategory entity, int itemsCount = 0)
    {
        return new InvCategoryTreeNodeDto
        {
            Id = entity.Id,
            BranchId = entity.BranchId,
            ParentCategoryId = entity.ParentCategoryId,
            ParentCategoryName = entity.ParentCategory?.CategoryNameLocal,
            IsMainCategory = !entity.ParentCategoryId.HasValue,
            IsSubCategory = entity.ParentCategoryId.HasValue,
            CategoryCode = entity.CategoryCode,
            CategoryNameLocal = entity.CategoryNameLocal,
            CategoryNameEn = entity.CategoryNameEn,
            CategoryLevel = entity.CategoryLevel,
            GlControlAccount = entity.GlControlAccount,
            GlCogsAccount = entity.GlCogsAccount,
            GlRevenueAccount = entity.GlRevenueAccount,
            GlAdjustmentAccount = entity.GlAdjustmentAccount,
            ImageBase64 = entity.ImageBase64,
            ColorCode = entity.ColorCode,
            ShowInPos = entity.ShowInPos,
            IsActive = entity.IsActive,
            ItemsCount = itemsCount,
            Children = entity.SubCategories?.Select(s => ToTreeNodeDto(s)).ToList() ?? new()
        };
    }

    public static List<InvCategoryTreeNodeDto> BuildTree(List<InvItemCategory> allCategories, Dictionary<long, int>? itemCounts = null)
    {
        var nodeMap = allCategories.ToDictionary(
            c => c.Id,
            c => new InvCategoryTreeNodeDto
            {
                Id = c.Id,
                BranchId = c.BranchId,
                ParentCategoryId = c.ParentCategoryId,
                IsMainCategory = !c.ParentCategoryId.HasValue,
                IsSubCategory = c.ParentCategoryId.HasValue,
                CategoryCode = c.CategoryCode,
                CategoryNameLocal = c.CategoryNameLocal,
                CategoryNameEn = c.CategoryNameEn,
                CategoryLevel = c.CategoryLevel,
                GlControlAccount = c.GlControlAccount,
                GlCogsAccount = c.GlCogsAccount,
                GlRevenueAccount = c.GlRevenueAccount,
                GlAdjustmentAccount = c.GlAdjustmentAccount,
                ImageBase64 = c.ImageBase64,
                ColorCode = c.ColorCode,
                ShowInPos = c.ShowInPos,
                IsActive = c.IsActive,
                ItemsCount = itemCounts != null && itemCounts.TryGetValue(c.Id, out var count) ? count : 0,
                Children = new List<InvCategoryTreeNodeDto>()
            });

        var roots = new List<InvCategoryTreeNodeDto>();

        foreach (var c in allCategories)
        {
            var node = nodeMap[c.Id];
            if (c.ParentCategoryId.HasValue && nodeMap.TryGetValue(c.ParentCategoryId.Value, out var parentNode))
            {
                node.ParentCategoryName = parentNode.CategoryNameLocal;
                parentNode.Children.Add(node);
            }
            else
            {
                roots.Add(node);
            }
        }

        return roots;
    }
}
