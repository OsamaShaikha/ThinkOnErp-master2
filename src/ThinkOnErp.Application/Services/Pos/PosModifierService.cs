using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using ThinkOnErp.Application.Common;
using ThinkOnErp.Application.DTOs.Pos;
using ThinkOnErp.Domain.Entities.Pos;
using ThinkOnErp.Domain.Entities.Pos.Enums;
using ThinkOnErp.Domain.Interfaces;
using ThinkOnErp.Domain.Interfaces.Inventory;
using ThinkOnErp.Domain.Interfaces.Pos;

namespace ThinkOnErp.Application.Services.Pos;

public class PosModifierService : IPosModifierService
{
    private readonly IPosModifierRepository _modifierRepository;
    private readonly IInvItemRepository _itemRepository;
    private readonly IBranchRepository _branchRepository;

    public PosModifierService(
        IPosModifierRepository modifierRepository,
        IInvItemRepository itemRepository,
        IBranchRepository branchRepository)
    {
        _modifierRepository = modifierRepository;
        _itemRepository = itemRepository;
        _branchRepository = branchRepository;
    }

    public async Task<ApiResponse<IReadOnlyList<PosModifierGroupDto>>> GetGroupsByBranchAsync(long branchId, bool? isActive = null, CancellationToken ct = default)
    {
        var groups = await _modifierRepository.GetGroupsByBranchAsync(branchId, isActive, ct);
        var dtos = groups.Select(MapToGroupDto).ToList();
        return ApiResponse<IReadOnlyList<PosModifierGroupDto>>.CreateSuccess(dtos);
    }

    public async Task<ApiResponse<PosModifierGroupDto>> GetGroupByIdAsync(long id, CancellationToken ct = default)
    {
        var group = await _modifierRepository.GetGroupByIdAsync(id, ct);
        if (group == null)
            return ApiResponse<PosModifierGroupDto>.CreateFailure("Modifier group not found", null, 404);

        return ApiResponse<PosModifierGroupDto>.CreateSuccess(MapToGroupDto(group));
    }

    public async Task<ApiResponse<PosModifierGroupDto>> CreateGroupAsync(CreatePosModifierGroupDto dto, string username, CancellationToken ct = default)
    {
        if (dto.BranchId <= 0)
            return ApiResponse<PosModifierGroupDto>.CreateFailure("Valid BranchId is required", null, 400);

        var branch = await _branchRepository.GetByIdAsync(dto.BranchId);
        if (branch == null)
            return ApiResponse<PosModifierGroupDto>.CreateFailure($"Branch {dto.BranchId} not found", null, 404);

        if (string.IsNullOrWhiteSpace(dto.GroupCode))
            return ApiResponse<PosModifierGroupDto>.CreateFailure("GroupCode is required", null, 400);

        if (string.IsNullOrWhiteSpace(dto.GroupNameLocal))
            return ApiResponse<PosModifierGroupDto>.CreateFailure("GroupNameLocal is required", null, 400);

        var (isValid, errorMsg) = ValidateSelectionConfig(dto.SelectionType, dto.MinSelections, dto.MaxSelections);
        if (!isValid)
            return ApiResponse<PosModifierGroupDto>.CreateFailure(errorMsg!, null, 400);

        var normalizedCode = dto.GroupCode.Trim().ToUpperInvariant();
        var existing = await _modifierRepository.GetGroupByCodeAsync(dto.BranchId, normalizedCode, ct);
        if (existing != null)
            return ApiResponse<PosModifierGroupDto>.CreateFailure($"Modifier group code '{dto.GroupCode}' already exists for branch {dto.BranchId}", null, 400);

        var group = new PosModifierGroup
        {
            BranchId = dto.BranchId,
            GroupCode = normalizedCode,
            GroupNameLocal = dto.GroupNameLocal.Trim(),
            GroupNameEn = dto.GroupNameEn?.Trim(),
            IsRequired = dto.IsRequired,
            SelectionType = dto.SelectionType,
            MinSelections = dto.MinSelections,
            MaxSelections = dto.MaxSelections,
            SortOrder = dto.SortOrder,
            IsActive = true,
            CreationUser = username,
            CreationDate = DateTime.UtcNow
        };

        if (dto.Options != null && dto.Options.Count > 0)
        {
            foreach (var opt in dto.Options)
            {
                if (opt == null || string.IsNullOrWhiteSpace(opt.OptionNameLocal))
                    return ApiResponse<PosModifierGroupDto>.CreateFailure("OptionNameLocal is required for all options", null, 400);

                if (opt.RelatedItemId.HasValue && opt.RelatedItemId.Value > 0)
                {
                    var relatedItem = await _itemRepository.GetByIdAsync(opt.RelatedItemId.Value, ct);
                    if (relatedItem == null)
                        return ApiResponse<PosModifierGroupDto>.CreateFailure($"Related item {opt.RelatedItemId.Value} not found", null, 404);
                }

                group.Options.Add(new PosModifierOption
                {
                    OptionNameLocal = opt.OptionNameLocal.Trim(),
                    OptionNameEn = opt.OptionNameEn?.Trim(),
                    PriceAdjustment = opt.PriceAdjustment,
                    RelatedItemId = opt.RelatedItemId,
                    IsDefault = opt.IsDefault,
                    SortOrder = opt.SortOrder,
                    IsActive = opt.IsActive
                });
            }
        }

        if (dto.LinkedItemIds != null && dto.LinkedItemIds.Count > 0)
        {
            var distinctItemIds = dto.LinkedItemIds.Distinct().ToList();
            foreach (var itemId in distinctItemIds)
            {
                var linkedItem = await _itemRepository.GetByIdAsync(itemId, ct);
                if (linkedItem == null)
                    return ApiResponse<PosModifierGroupDto>.CreateFailure($"Linked item {itemId} not found", null, 404);
            }

            int sort = 1;
            foreach (var itemId in distinctItemIds)
            {
                group.ItemLinks.Add(new PosItemModifierGroup
                {
                    ItemId = itemId,
                    SortOrder = sort++
                });
            }
        }

        await _modifierRepository.AddGroupAsync(group, ct);

        try
        {
            await _modifierRepository.SaveChangesAsync(ct);
        }
        catch (Exception ex) when (ex.ToString().Contains("ORA-00001") || ex.ToString().Contains("UQ_POS_MOD_GRP_BR_CD"))
        {
            return ApiResponse<PosModifierGroupDto>.CreateFailure($"Modifier group code '{dto.GroupCode}' already exists for branch {dto.BranchId}", null, 400);
        }

        return await GetGroupByIdAsync(group.Id, ct);
    }

    public async Task<ApiResponse<PosModifierGroupDto>> UpdateGroupAsync(long id, UpdatePosModifierGroupDto dto, string username, CancellationToken ct = default)
    {
        var group = await _modifierRepository.GetGroupByIdAsync(id, ct);
        if (group == null)
            return ApiResponse<PosModifierGroupDto>.CreateFailure("Modifier group not found", null, 404);

        if (string.IsNullOrWhiteSpace(dto.GroupNameLocal))
            return ApiResponse<PosModifierGroupDto>.CreateFailure("GroupNameLocal is required", null, 400);

        var effectiveSelectionType = dto.SelectionType != 0 ? dto.SelectionType : group.SelectionType;
        var (isValid, errorMsg) = ValidateSelectionConfig(effectiveSelectionType, dto.MinSelections, dto.MaxSelections);
        if (!isValid)
            return ApiResponse<PosModifierGroupDto>.CreateFailure(errorMsg!, null, 400);

        group.GroupNameLocal = dto.GroupNameLocal.Trim();
        group.GroupNameEn = dto.GroupNameEn?.Trim();
        group.IsRequired = dto.IsRequired;
        group.SelectionType = effectiveSelectionType;
        group.MinSelections = dto.MinSelections;
        group.MaxSelections = dto.MaxSelections;
        group.SortOrder = dto.SortOrder;
        group.IsActive = dto.IsActive;
        group.UpdateUser = username;
        group.UpdateDate = DateTime.UtcNow;

        await _modifierRepository.UpdateGroupAsync(group, ct);
        await _modifierRepository.SaveChangesAsync(ct);

        return await GetGroupByIdAsync(group.Id, ct);
    }

    public async Task<ApiResponse<bool>> DeleteGroupAsync(long id, CancellationToken ct = default)
    {
        var group = await _modifierRepository.GetGroupByIdAsync(id, ct);
        if (group == null)
            return ApiResponse<bool>.CreateFailure("Modifier group not found", null, 404);

        group.IsActive = false;
        group.UpdateDate = DateTime.UtcNow;
        await _modifierRepository.UpdateGroupAsync(group, ct);
        await _modifierRepository.SaveChangesAsync(ct);

        return ApiResponse<bool>.CreateSuccess(true);
    }

    public async Task<ApiResponse<PosModifierOptionDto>> UpsertOptionAsync(long groupId, UpsertPosModifierOptionDto dto, CancellationToken ct = default)
    {
        var group = await _modifierRepository.GetGroupByIdAsync(groupId, ct);
        if (group == null)
            return ApiResponse<PosModifierOptionDto>.CreateFailure("Modifier group not found", null, 404);

        if (string.IsNullOrWhiteSpace(dto.OptionNameLocal))
            return ApiResponse<PosModifierOptionDto>.CreateFailure("OptionNameLocal is required", null, 400);

        if (dto.RelatedItemId.HasValue && dto.RelatedItemId.Value > 0)
        {
            var relatedItem = await _itemRepository.GetByIdAsync(dto.RelatedItemId.Value, ct);
            if (relatedItem == null)
                return ApiResponse<PosModifierOptionDto>.CreateFailure($"Related item {dto.RelatedItemId.Value} not found", null, 404);
        }

        PosModifierOption? option = null;
        if (dto.Id.HasValue && dto.Id.Value > 0)
        {
            option = await _modifierRepository.GetOptionByIdAsync(dto.Id.Value, ct);
            if (option == null || option.ModifierGroupId != groupId)
                return ApiResponse<PosModifierOptionDto>.CreateFailure("Modifier option not found in this group", null, 404);

            option.OptionNameLocal = dto.OptionNameLocal.Trim();
            option.OptionNameEn = dto.OptionNameEn?.Trim();
            option.PriceAdjustment = dto.PriceAdjustment;
            option.RelatedItemId = dto.RelatedItemId;
            option.IsDefault = dto.IsDefault;
            option.SortOrder = dto.SortOrder;
            option.IsActive = dto.IsActive;

            await _modifierRepository.UpdateOptionAsync(option, ct);
        }
        else
        {
            option = new PosModifierOption
            {
                ModifierGroupId = groupId,
                OptionNameLocal = dto.OptionNameLocal.Trim(),
                OptionNameEn = dto.OptionNameEn?.Trim(),
                PriceAdjustment = dto.PriceAdjustment,
                RelatedItemId = dto.RelatedItemId,
                IsDefault = dto.IsDefault,
                SortOrder = dto.SortOrder,
                IsActive = dto.IsActive
            };
            await _modifierRepository.AddOptionAsync(option, ct);
        }

        try
        {
            await _modifierRepository.SaveChangesAsync(ct);
        }
        catch (Exception ex) when (ex.ToString().Contains("ORA-02291") || ex.ToString().Contains("FK_POS_MOD_OPT_ITM"))
        {
            return ApiResponse<PosModifierOptionDto>.CreateFailure("Related item not found", null, 404);
        }

        var resultDto = MapToOptionDto(option);
        return ApiResponse<PosModifierOptionDto>.CreateSuccess(resultDto);
    }

    public async Task<ApiResponse<bool>> DeleteOptionAsync(long groupId, long optionId, CancellationToken ct = default)
    {
        var option = await _modifierRepository.GetOptionByIdAsync(optionId, ct);
        if (option == null || option.ModifierGroupId != groupId)
            return ApiResponse<bool>.CreateFailure("Modifier option not found", null, 404);

        await _modifierRepository.DeleteOptionAsync(option, ct);
        await _modifierRepository.SaveChangesAsync(ct);

        return ApiResponse<bool>.CreateSuccess(true);
    }

    public async Task<ApiResponse<ItemModifierGroupsDto>> GetGroupsByItemIdAsync(long itemId, CancellationToken ct = default)
    {
        var item = await _itemRepository.GetByIdAsync(itemId, ct);
        if (item == null)
            return ApiResponse<ItemModifierGroupsDto>.CreateFailure("Item not found", null, 404);

        var groups = await _modifierRepository.GetGroupsByItemIdAsync(itemId, ct);
        var result = new ItemModifierGroupsDto
        {
            ItemId = item.Id,
            ItemName = item.ItemNameLocal,
            ModifierGroups = groups.Select(MapToGroupDto).ToList()
        };

        return ApiResponse<ItemModifierGroupsDto>.CreateSuccess(result);
    }

    public async Task<ApiResponse<bool>> AssignGroupsToItemAsync(long itemId, List<long> groupIds, CancellationToken ct = default)
    {
        var item = await _itemRepository.GetByIdAsync(itemId, ct);
        if (item == null)
            return ApiResponse<bool>.CreateFailure("Item not found", null, 404);

        if (groupIds != null && groupIds.Count > 0)
        {
            var distinctIds = groupIds.Distinct().ToList();

            // Validate all groups exist BEFORE clearing or modifying any existing links!
            foreach (var gId in distinctIds)
            {
                var grp = await _modifierRepository.GetGroupByIdAsync(gId, ct);
                if (grp == null)
                    return ApiResponse<bool>.CreateFailure($"Modifier group {gId} not found", null, 404);
            }

            // All validated! Now safely clear and re-add links
            await _modifierRepository.ClearItemModifierGroupsAsync(itemId, ct);
            int sort = 1;
            foreach (var gId in distinctIds)
            {
                await _modifierRepository.AddItemModifierGroupAsync(new PosItemModifierGroup
                {
                    ItemId = itemId,
                    ModifierGroupId = gId,
                    SortOrder = sort++
                }, ct);
            }
        }
        else
        {
            // Empty list means clear all links
            await _modifierRepository.ClearItemModifierGroupsAsync(itemId, ct);
        }

        await _modifierRepository.SaveChangesAsync(ct);
        return ApiResponse<bool>.CreateSuccess(true);
    }

    public async Task<ApiResponse<bool>> RemoveGroupFromItemAsync(long itemId, long groupId, CancellationToken ct = default)
    {
        var link = await _modifierRepository.GetItemModifierGroupAsync(itemId, groupId, ct);
        if (link == null)
            return ApiResponse<bool>.CreateFailure("Modifier group not linked to this item", null, 404);

        await _modifierRepository.DeleteItemModifierGroupAsync(link, ct);
        await _modifierRepository.SaveChangesAsync(ct);

        return ApiResponse<bool>.CreateSuccess(true);
    }

    private static (bool IsValid, string? ErrorMessage) ValidateSelectionConfig(PosSelectionType selectionType, int minSelections, int? maxSelections)
    {
        if (!Enum.IsDefined(typeof(PosSelectionType), selectionType))
        {
            return (false, "Invalid selection type. Must be 1 (Single) or 2 (Multiple)");
        }

        if (minSelections < 0)
        {
            return (false, "MinSelections cannot be negative");
        }

        if (maxSelections.HasValue && maxSelections.Value < 0)
        {
            return (false, "MaxSelections cannot be negative");
        }

        if (maxSelections.HasValue && maxSelections.Value > 0 && minSelections > maxSelections.Value)
        {
            return (false, "MinSelections cannot be greater than MaxSelections");
        }

        return (true, null);
    }

    private static PosModifierGroupDto MapToGroupDto(PosModifierGroup g)
    {
        return new PosModifierGroupDto
        {
            Id = g.Id,
            BranchId = g.BranchId,
            GroupCode = g.GroupCode,
            GroupNameLocal = g.GroupNameLocal,
            GroupNameEn = g.GroupNameEn,
            IsRequired = g.IsRequired,
            SelectionType = g.SelectionType,
            MinSelections = g.MinSelections,
            MaxSelections = g.MaxSelections,
            SortOrder = g.SortOrder,
            IsActive = g.IsActive,
            Options = g.Options.Select(MapToOptionDto).ToList(),
            LinkedItemIds = g.ItemLinks.Select(l => l.ItemId).ToList()
        };
    }

    private static PosModifierOptionDto MapToOptionDto(PosModifierOption o)
    {
        return new PosModifierOptionDto
        {
            Id = o.Id,
            ModifierGroupId = o.ModifierGroupId,
            OptionNameLocal = o.OptionNameLocal,
            OptionNameEn = o.OptionNameEn,
            PriceAdjustment = o.PriceAdjustment,
            RelatedItemId = o.RelatedItemId,
            RelatedItemName = o.RelatedItem?.ItemNameLocal,
            IsDefault = o.IsDefault,
            SortOrder = o.SortOrder,
            IsActive = o.IsActive
        };
    }
}
