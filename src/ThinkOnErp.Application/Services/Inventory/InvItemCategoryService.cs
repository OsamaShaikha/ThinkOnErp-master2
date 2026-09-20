using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using ThinkOnErp.Application.Common;
using ThinkOnErp.Application.DTOs.Inventory.ItemCategories;
using ThinkOnErp.Application.Mappings.Inventory;
using ThinkOnErp.Domain.Constants;
using ThinkOnErp.Domain.Entities.Inventory;
using ThinkOnErp.Domain.Interfaces.Inventory;

namespace ThinkOnErp.Application.Services.Inventory;

public sealed class InvItemCategoryService : IInvItemCategoryService
{
    private readonly IInvItemCategoryRepository _categoryRepository;
    private readonly ILogger<InvItemCategoryService> _logger;

    public InvItemCategoryService(IInvItemCategoryRepository categoryRepository, ILogger<InvItemCategoryService> logger)
    {
        _categoryRepository = categoryRepository;
        _logger = logger;
    }

    #region Main Categories CRUD

    public async Task<ApiResponse<InvMainCategoryDto>> CreateMainCategoryAsync(CreateMainCategoryDto dto, string username, CancellationToken ct = default)
    {
        if (await _categoryRepository.ExistsAsync(dto.CategoryCode, null, ct))
            return ApiResponse<InvMainCategoryDto>.CreateFailure($"Category code '{dto.CategoryCode}' already exists", null, 400);

        var entity = InvItemCategoryMapper.ToEntity(dto, username);
        var created = await _categoryRepository.CreateAsync(entity, ct);
        return ApiResponse<InvMainCategoryDto>.CreateSuccess(InvItemCategoryMapper.ToMainCategoryDto(created), ResponseCodes.CategoryCreated, 201);
    }

    public async Task<ApiResponse<List<InvMainCategoryDto>>> GetMainCategoriesAsync(long? branchId = null, CancellationToken ct = default)
    {
        var mainCategories = await _categoryRepository.GetMainCategoriesAsync(branchId, ct);
        var dtos = new List<InvMainCategoryDto>(mainCategories.Count);

        foreach (var mc in mainCategories)
        {
            var itemsCount = await _categoryRepository.GetItemsCountAsync(mc.Id, true, ct);
            dtos.Add(InvItemCategoryMapper.ToMainCategoryDto(mc, itemsCount));
        }

        return ApiResponse<List<InvMainCategoryDto>>.CreateSuccess(dtos, ResponseCodes.ItemCategoriesRetrieved);
    }

    public async Task<ApiResponse<InvMainCategoryDto>> GetMainCategoryByIdAsync(long id, CancellationToken ct = default)
    {
        var category = await _categoryRepository.GetByIdAsync(id, ct);
        if (category == null || category.CategoryLevel != 1)
            return ApiResponse<InvMainCategoryDto>.CreateFailure("Main item category not found", null, 404);

        var itemsCount = await _categoryRepository.GetItemsCountAsync(category.Id, true, ct);
        return ApiResponse<InvMainCategoryDto>.CreateSuccess(InvItemCategoryMapper.ToMainCategoryDto(category, itemsCount), ResponseCodes.CategoryDetailsRetrieved);
    }

    public async Task<ApiResponse<InvMainCategoryDto>> UpdateMainCategoryAsync(long id, UpdateMainCategoryDto dto, string username, CancellationToken ct = default)
    {
        var category = await _categoryRepository.GetByIdAsync(id, ct);
        if (category == null || category.CategoryLevel != 1)
            return ApiResponse<InvMainCategoryDto>.CreateFailure("Main item category not found", null, 404);

        category.CategoryNameLocal = dto.CategoryNameLocal.Trim();
        category.CategoryNameEn = dto.CategoryNameEn?.Trim();
        category.GlControlAccount = dto.GlControlAccount?.Trim();
        category.GlCogsAccount = dto.GlCogsAccount?.Trim();
        category.GlRevenueAccount = dto.GlRevenueAccount?.Trim();
        category.GlAdjustmentAccount = dto.GlAdjustmentAccount?.Trim();
        category.ImageBase64 = dto.ImageBase64;
        category.ColorCode = dto.ColorCode;
        category.ShowInPos = dto.ShowInPos;
        category.IsActive = dto.IsActive;
        category.UpdateUser = username;
        category.UpdateDate = DateTime.UtcNow;

        await _categoryRepository.UpdateAsync(category, ct);

        var itemsCount = await _categoryRepository.GetItemsCountAsync(category.Id, true, ct);
        return ApiResponse<InvMainCategoryDto>.CreateSuccess(InvItemCategoryMapper.ToMainCategoryDto(category, itemsCount), ResponseCodes.CategoryUpdated);
    }

    public async Task<ApiResponse<bool>> DeleteMainCategoryAsync(long id, CancellationToken ct = default)
    {
        var category = await _categoryRepository.GetByIdAsync(id, ct);
        if (category == null || category.CategoryLevel != 1)
            return ApiResponse<bool>.CreateFailure("Main item category not found", null, 404);

        if (await _categoryRepository.HasSubCategoriesAsync(id, ct))
            return ApiResponse<bool>.CreateFailure("Cannot delete main category because it contains active sub-categories. Delete or reassign sub-categories first.", null, 400);

        if (await _categoryRepository.HasItemsAsync(id, true, ct))
            return ApiResponse<bool>.CreateFailure("Cannot delete main category because it has items assigned to it.", null, 400);

        await _categoryRepository.DeleteAsync(id, ct);
        return ApiResponse<bool>.CreateSuccess(true, ResponseCodes.CategoryDeleted);
    }

    #endregion

    #region Sub Categories CRUD

    public async Task<ApiResponse<InvSubCategoryDto>> CreateSubCategoryAsync(CreateSubCategoryDto dto, string username, CancellationToken ct = default)
    {
        var parentCategory = await _categoryRepository.GetByIdAsync(dto.MainCategoryId, ct);
        if (parentCategory == null)
            return ApiResponse<InvSubCategoryDto>.CreateFailure($"Parent category {dto.MainCategoryId} not found", null, 400);

        if (await _categoryRepository.ExistsAsync(dto.CategoryCode, null, ct))
            return ApiResponse<InvSubCategoryDto>.CreateFailure($"Category code '{dto.CategoryCode}' already exists", null, 400);

        var entity = InvItemCategoryMapper.ToEntity(dto, username);
        entity.CategoryLevel = parentCategory.CategoryLevel + 1;
        var created = await _categoryRepository.CreateAsync(entity, ct);
        created.ParentCategory = parentCategory;

        return ApiResponse<InvSubCategoryDto>.CreateSuccess(InvItemCategoryMapper.ToSubCategoryDto(created), ResponseCodes.CategoryCreated, 201);
    }

    public async Task<ApiResponse<List<InvSubCategoryDto>>> GetAllSubCategoriesAsync(long? branchId = null, CancellationToken ct = default)
    {
        var subCategories = await _categoryRepository.GetAllSubCategoriesAsync(branchId, ct);
        var dtos = new List<InvSubCategoryDto>(subCategories.Count);

        foreach (var sc in subCategories)
        {
            var itemsCount = await _categoryRepository.GetItemsCountAsync(sc.Id, false, ct);
            dtos.Add(InvItemCategoryMapper.ToSubCategoryDto(sc, itemsCount));
        }

        return ApiResponse<List<InvSubCategoryDto>>.CreateSuccess(dtos, ResponseCodes.ItemCategoriesRetrieved);
    }

    public async Task<ApiResponse<List<InvSubCategoryDto>>> GetSubCategoriesByMainCategoryIdAsync(long mainCategoryId, CancellationToken ct = default)
    {
        var subCategories = await _categoryRepository.GetSubCategoriesAsync(mainCategoryId, ct);
        var dtos = new List<InvSubCategoryDto>(subCategories.Count);

        foreach (var sc in subCategories)
        {
            var itemsCount = await _categoryRepository.GetItemsCountAsync(sc.Id, false, ct);
            dtos.Add(InvItemCategoryMapper.ToSubCategoryDto(sc, itemsCount));
        }

        return ApiResponse<List<InvSubCategoryDto>>.CreateSuccess(dtos, ResponseCodes.ItemCategoriesRetrieved);
    }

    public async Task<ApiResponse<InvSubCategoryDto>> GetSubCategoryByIdAsync(long id, CancellationToken ct = default)
    {
        var category = await _categoryRepository.GetByIdAsync(id, ct);
        if (category == null || category.CategoryLevel < 2)
            return ApiResponse<InvSubCategoryDto>.CreateFailure("Sub-category not found", null, 404);

        var itemsCount = await _categoryRepository.GetItemsCountAsync(category.Id, false, ct);
        return ApiResponse<InvSubCategoryDto>.CreateSuccess(InvItemCategoryMapper.ToSubCategoryDto(category, itemsCount), ResponseCodes.CategoryDetailsRetrieved);
    }

    public async Task<ApiResponse<InvSubCategoryDto>> UpdateSubCategoryAsync(long id, UpdateSubCategoryDto dto, string username, CancellationToken ct = default)
    {
        var category = await _categoryRepository.GetByIdAsync(id, ct);
        if (category == null || category.CategoryLevel < 2)
            return ApiResponse<InvSubCategoryDto>.CreateFailure("Sub-category not found", null, 404);

        if (dto.MainCategoryId.HasValue && dto.MainCategoryId.Value != category.ParentCategoryId)
        {
            if (dto.MainCategoryId.Value == id)
                return ApiResponse<InvSubCategoryDto>.CreateFailure("Cannot set a category as its own parent", null, 400);

            if (await _categoryRepository.IsDescendantOfAsync(dto.MainCategoryId.Value, id, ct))
                return ApiResponse<InvSubCategoryDto>.CreateFailure("Cannot move category under one of its own descendants (circular reference)", null, 400);

            var newParent = await _categoryRepository.GetByIdAsync(dto.MainCategoryId.Value, ct);
            if (newParent == null)
                return ApiResponse<InvSubCategoryDto>.CreateFailure($"New parent category {dto.MainCategoryId.Value} not found", null, 400);

            category.ParentCategoryId = dto.MainCategoryId.Value;
            category.ParentCategory = newParent;
            category.CategoryLevel = newParent.CategoryLevel + 1;
        }

        category.CategoryNameLocal = dto.CategoryNameLocal.Trim();
        category.CategoryNameEn = dto.CategoryNameEn?.Trim();
        category.GlControlAccount = dto.GlControlAccount?.Trim();
        category.GlCogsAccount = dto.GlCogsAccount?.Trim();
        category.GlRevenueAccount = dto.GlRevenueAccount?.Trim();
        category.GlAdjustmentAccount = dto.GlAdjustmentAccount?.Trim();
        category.ImageBase64 = dto.ImageBase64;
        category.ColorCode = dto.ColorCode;
        category.ShowInPos = dto.ShowInPos;
        category.IsActive = dto.IsActive;
        category.UpdateUser = username;
        category.UpdateDate = DateTime.UtcNow;

        await _categoryRepository.UpdateAsync(category, ct);

        var itemsCount = await _categoryRepository.GetItemsCountAsync(category.Id, false, ct);
        return ApiResponse<InvSubCategoryDto>.CreateSuccess(InvItemCategoryMapper.ToSubCategoryDto(category, itemsCount), ResponseCodes.CategoryUpdated);
    }

    public async Task<ApiResponse<bool>> DeleteSubCategoryAsync(long id, CancellationToken ct = default)
    {
        var category = await _categoryRepository.GetByIdAsync(id, ct);
        if (category == null || category.CategoryLevel < 2)
            return ApiResponse<bool>.CreateFailure("Sub-category not found", null, 404);

        if (await _categoryRepository.HasSubCategoriesAsync(id, ct))
            return ApiResponse<bool>.CreateFailure("Cannot delete sub-category because it contains nested sub-categories. Delete or reassign them first.", null, 400);

        if (await _categoryRepository.HasItemsAsync(id, false, ct))
            return ApiResponse<bool>.CreateFailure("Cannot delete sub-category because it has items assigned to it.", null, 400);

        await _categoryRepository.DeleteAsync(id, ct);
        return ApiResponse<bool>.CreateSuccess(true, ResponseCodes.CategoryDeleted);
    }

    #endregion

    #region Generic & Multi-Level Tree CRUD

    public async Task<ApiResponse<InvItemCategoryDto>> CreateCategoryAsync(CreateInvItemCategoryDto dto, string username, CancellationToken ct = default)
    {
        if (await _categoryRepository.ExistsAsync(dto.CategoryCode, null, ct))
            return ApiResponse<InvItemCategoryDto>.CreateFailure($"Category code '{dto.CategoryCode}' already exists", null, 400);

        if (dto.ParentCategoryId.HasValue && dto.ParentCategoryId.Value <= 0)
            dto.ParentCategoryId = null;

        int level = 1;
        InvItemCategory? parent = null;
        if (dto.ParentCategoryId.HasValue)
        {
            parent = await _categoryRepository.GetByIdAsync(dto.ParentCategoryId.Value, ct);
            if (parent == null)
                return ApiResponse<InvItemCategoryDto>.CreateFailure($"Parent category {dto.ParentCategoryId.Value} not found", null, 400);

            level = parent.CategoryLevel + 1;
        }

        var category = InvItemCategoryMapper.ToEntity(dto, username);
        category.CategoryLevel = level;
        category.ParentCategory = parent;
        var created = await _categoryRepository.CreateAsync(category, ct);
        var resDto = InvItemCategoryMapper.ToDto(created, 0);
        resDto.ParentCategoryName = parent?.CategoryNameLocal;
        return ApiResponse<InvItemCategoryDto>.CreateSuccess(resDto, ResponseCodes.CategoryCreated, 201);
    }

    public async Task<ApiResponse<InvItemCategoryDto>> GetByIdAsync(long id, CancellationToken ct = default)
    {
        var category = await _categoryRepository.GetByIdAsync(id, ct);
        if (category == null)
            return ApiResponse<InvItemCategoryDto>.CreateFailure("Item category not found", null, 404);

        var itemsCount = await _categoryRepository.GetCategoryItemsCountAsync(category.Id, ct);
        var dto = InvItemCategoryMapper.ToDto(category, itemsCount);
        return ApiResponse<InvItemCategoryDto>.CreateSuccess(dto, ResponseCodes.CategoryDetailsRetrieved);
    }

    public async Task<ApiResponse<List<InvItemCategoryDto>>> GetSubCategoriesAsync(long mainCategoryId, CancellationToken ct = default)
    {
        var categories = await _categoryRepository.GetSubCategoriesAsync(mainCategoryId, ct);
        return ApiResponse<List<InvItemCategoryDto>>.CreateSuccess(categories.Select(c => InvItemCategoryMapper.ToDto(c, 0)).ToList(), ResponseCodes.ItemCategoriesRetrieved);
    }

    public async Task<ApiResponse<PagedResultDto<InvItemCategoryDto>>> GetAllPagedAsync(long? branchId, int pageIndex, int pageSize, CancellationToken ct = default)
    {
        var (items, total) = await _categoryRepository.GetAllPagedAsync(branchId, pageIndex, pageSize, ct);
        var dtos = items.Select(c => InvItemCategoryMapper.ToDto(c, 0)).ToList();
        var pagedResult = new PagedResultDto<InvItemCategoryDto>(dtos, total, pageIndex, pageSize);
        return ApiResponse<PagedResultDto<InvItemCategoryDto>>.CreateSuccess(pagedResult, ResponseCodes.ItemCategoriesRetrieved);
    }

    public async Task<ApiResponse<InvItemCategoryDto>> UpdateCategoryAsync(long id, UpdateInvItemCategoryDto dto, string username, CancellationToken ct = default)
    {
        var category = await _categoryRepository.GetByIdAsync(id, ct);
        if (category == null)
            return ApiResponse<InvItemCategoryDto>.CreateFailure("Item category not found", null, 404);

        if (dto.ParentCategoryId.HasValue && dto.ParentCategoryId.Value <= 0)
            dto.ParentCategoryId = null;

        if (dto.ParentCategoryId.HasValue && dto.ParentCategoryId.Value != category.ParentCategoryId)
        {
            if (dto.ParentCategoryId.Value == id)
                return ApiResponse<InvItemCategoryDto>.CreateFailure("Cannot set a category as its own parent", null, 400);

            if (await _categoryRepository.IsDescendantOfAsync(dto.ParentCategoryId.Value, id, ct))
                return ApiResponse<InvItemCategoryDto>.CreateFailure("Cannot move category under one of its own descendants (circular reference)", null, 400);

            var newParent = await _categoryRepository.GetByIdAsync(dto.ParentCategoryId.Value, ct);
            if (newParent == null)
                return ApiResponse<InvItemCategoryDto>.CreateFailure($"Parent category {dto.ParentCategoryId.Value} not found", null, 400);

            category.ParentCategoryId = dto.ParentCategoryId.Value;
            category.ParentCategory = newParent;
            category.CategoryLevel = newParent.CategoryLevel + 1;
        }
        else if (dto.ParentCategoryId == null && category.ParentCategoryId != null)
        {
            category.ParentCategoryId = null;
            category.ParentCategory = null;
            category.CategoryLevel = 1;
        }

        if (!string.IsNullOrWhiteSpace(dto.CategoryNameLocal)) category.CategoryNameLocal = dto.CategoryNameLocal.Trim();
        if (dto.CategoryNameEn != null) category.CategoryNameEn = dto.CategoryNameEn.Trim();
        if (dto.GlControlAccount != null) category.GlControlAccount = dto.GlControlAccount.Trim();
        if (dto.GlCogsAccount != null) category.GlCogsAccount = dto.GlCogsAccount.Trim();
        if (dto.GlRevenueAccount != null) category.GlRevenueAccount = dto.GlRevenueAccount.Trim();
        if (dto.GlAdjustmentAccount != null) category.GlAdjustmentAccount = dto.GlAdjustmentAccount.Trim();
        if (dto.ImageBase64 != null) category.ImageBase64 = dto.ImageBase64;
        if (dto.ColorCode.HasValue) category.ColorCode = dto.ColorCode.Value;
        if (dto.ShowInPos.HasValue) category.ShowInPos = dto.ShowInPos.Value;
        if (dto.IsActive.HasValue) category.IsActive = dto.IsActive.Value;

        category.UpdateUser = username;
        category.UpdateDate = DateTime.UtcNow;

        await _categoryRepository.UpdateAsync(category, ct);

        var itemsCount = await _categoryRepository.GetCategoryItemsCountAsync(category.Id, ct);
        return ApiResponse<InvItemCategoryDto>.CreateSuccess(InvItemCategoryMapper.ToDto(category, itemsCount), ResponseCodes.CategoryUpdated);
    }

    public async Task<ApiResponse<bool>> DeleteCategoryAsync(long id, CancellationToken ct = default)
    {
        var category = await _categoryRepository.GetByIdAsync(id, ct);
        if (category == null)
            return ApiResponse<bool>.CreateFailure("Item category not found", null, 404);

        if (await _categoryRepository.HasSubCategoriesAsync(id, ct))
            return ApiResponse<bool>.CreateFailure("Cannot delete category because it contains sub-categories. Delete or reassign sub-categories first.", null, 400);

        if (await _categoryRepository.HasItemsAsync(id, true, ct) || await _categoryRepository.HasItemsAsync(id, false, ct))
            return ApiResponse<bool>.CreateFailure("Cannot delete category because it has items assigned to it.", null, 400);

        await _categoryRepository.DeleteAsync(id, ct);
        return ApiResponse<bool>.CreateSuccess(true, ResponseCodes.CategoryDeleted);
    }

    #endregion

    #region Tree Hierarchy & POS

    public async Task<ApiResponse<List<InvCategoryTreeNodeDto>>> GetTreeAsync(long? branchId = null, bool? posOnly = null, CancellationToken ct = default)
    {
        var allCategories = await _categoryRepository.GetAllActiveCategoriesAsync(branchId, posOnly, ct);
        var itemCounts = new Dictionary<long, int>();
        foreach (var c in allCategories)
        {
            itemCounts[c.Id] = await _categoryRepository.GetCategoryItemsCountAsync(c.Id, ct);
        }

        var tree = InvItemCategoryMapper.BuildTree(allCategories, itemCounts);
        return ApiResponse<List<InvCategoryTreeNodeDto>>.CreateSuccess(tree, ResponseCodes.ItemCategoriesRetrieved);
    }

    public async Task<ApiResponse<List<InvCategoryTreeNodeDto>>> GetPosCategoriesAsync(long? branchId = null, CancellationToken ct = default)
    {
        return await GetTreeAsync(branchId, true, ct);
    }

    public async Task<ApiResponse<List<InvCategoryTreeNodeDto>>> GetChildrenAsync(long parentCategoryId, CancellationToken ct = default)
    {
        var children = await _categoryRepository.GetChildrenAsync(parentCategoryId, ct);
        var dtos = new List<InvCategoryTreeNodeDto>(children.Count);
        foreach (var c in children)
        {
            var count = await _categoryRepository.GetCategoryItemsCountAsync(c.Id, ct);
            dtos.Add(InvItemCategoryMapper.ToTreeNodeDto(c, count));
        }
        return ApiResponse<List<InvCategoryTreeNodeDto>>.CreateSuccess(dtos, ResponseCodes.ItemCategoriesRetrieved);
    }

    #endregion
}
