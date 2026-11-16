using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using ThinkOnErp.Application.Common;
using ThinkOnErp.Application.DTOs.Inventory.Modifiers;
using ThinkOnErp.Domain.Entities.Inventory;
using ThinkOnErp.Domain.Entities.Inventory.Enums;
using ThinkOnErp.Domain.Interfaces;
using ThinkOnErp.Domain.Interfaces.Inventory;
using ThinkOnErp.Domain.Interfaces.Pos;
using ThinkOnErp.Domain.Entities.Pos.Enums;

namespace ThinkOnErp.Application.Services.Inventory;

public class InvModifierService : IInvModifierService
{
    private readonly IInvModifierRepository _modifierRepository;
    private readonly IInvItemRepository _itemRepository;
    private readonly IBranchRepository _branchRepository;
    private readonly IInvBomRepository _bomRepository;
    private readonly IPosOrderRepository? _posOrderRepository;

    public InvModifierService(
        IInvModifierRepository modifierRepository,
        IInvItemRepository itemRepository,
        IBranchRepository branchRepository,
        IInvBomRepository bomRepository,
        IPosOrderRepository? posOrderRepository = null)
    {
        _modifierRepository = modifierRepository;
        _itemRepository = itemRepository;
        _branchRepository = branchRepository;
        _bomRepository = bomRepository;
        _posOrderRepository = posOrderRepository;
    }

    public async Task<ApiResponse<IReadOnlyList<InvModifierGroupDto>>> GetGroupsByBranchAsync(long branchId, bool? isActive = null, CancellationToken ct = default)
    {
        var groups = await _modifierRepository.GetGroupsByBranchAsync(branchId, isActive, ct);
        var dtos = groups.Select(MapToGroupDto).ToList();
        return ApiResponse<IReadOnlyList<InvModifierGroupDto>>.CreateSuccess(dtos);
    }

    public async Task<ApiResponse<InvModifierGroupDto>> GetGroupByIdAsync(long id, CancellationToken ct = default)
    {
        var group = await _modifierRepository.GetGroupByIdAsync(id, ct);
        if (group == null)
            return ApiResponse<InvModifierGroupDto>.CreateFailure("Modifier group not found", null, 404);

        return ApiResponse<InvModifierGroupDto>.CreateSuccess(MapToGroupDto(group));
    }

    public async Task<ApiResponse<InvModifierGroupDto>> CreateGroupAsync(CreateInvModifierGroupDto dto, string username, CancellationToken ct = default)
    {
        if (dto.BranchId <= 0)
            return ApiResponse<InvModifierGroupDto>.CreateFailure("Valid BranchId is required", null, 400);

        var branch = await _branchRepository.GetByIdAsync(dto.BranchId);
        if (branch == null)
            return ApiResponse<InvModifierGroupDto>.CreateFailure($"Branch {dto.BranchId} not found", null, 404);

        if (string.IsNullOrWhiteSpace(dto.GroupCode))
            return ApiResponse<InvModifierGroupDto>.CreateFailure("GroupCode is required", null, 400);

        if (string.IsNullOrWhiteSpace(dto.GroupNameLocal))
            return ApiResponse<InvModifierGroupDto>.CreateFailure("GroupNameLocal is required", null, 400);

        var (isValid, errorMsg) = ValidateSelectionConfig(dto.SelectionType, dto.MinSelections, dto.MaxSelections);
        if (!isValid)
            return ApiResponse<InvModifierGroupDto>.CreateFailure(errorMsg!, null, 400);

        var normalizedCode = dto.GroupCode.Trim().ToUpperInvariant();
        var existing = await _modifierRepository.GetGroupByCodeAsync(dto.BranchId, normalizedCode, ct);
        if (existing != null)
            return ApiResponse<InvModifierGroupDto>.CreateFailure($"Modifier group code '{dto.GroupCode}' already exists for branch {dto.BranchId}", null, 400);

        var group = new InvModifierGroup
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
                var (validOpt, optError) = await this.ValidateOptionAsync(dto.BranchId, opt, ct);
                if (!validOpt)
                    return ApiResponse<InvModifierGroupDto>.CreateFailure(optError!, null, 400);

                group.Options.Add(new InvModifierOption
                {
                    OptionNameLocal = opt.OptionNameLocal.Trim(),
                    OptionNameEn = opt.OptionNameEn?.Trim(),
                    PriceAdjustment = opt.PriceAdjustment,
                    RelatedItemId = opt.RelatedItemId,
                    RelatedItemQty = opt.RelatedItemQty > 0 ? opt.RelatedItemQty : 1m,
                    RelatedUomCode = opt.RelatedUomCode?.Trim(),
                    UomFactor = opt.UomFactor > 0 ? opt.UomFactor : 1m,
                    OptionType = opt.OptionType,
                    ReplacesItemId = opt.ReplacesItemId,
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
                    return ApiResponse<InvModifierGroupDto>.CreateFailure($"Linked item {itemId} not found", null, 404);
            }

            int sort = 1;
            foreach (var itemId in distinctItemIds)
            {
                group.ItemLinks.Add(new InvItemModifierGroup
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
        catch (Exception ex) when (ex.ToString().Contains("ORA-00001") || ex.ToString().Contains("UQ_INV_MOD_GRP_BR_CD") || ex.ToString().Contains("UQ_POS_MOD_GRP_BR_CD"))
        {
            return ApiResponse<InvModifierGroupDto>.CreateFailure($"Modifier group code '{dto.GroupCode}' already exists for branch {dto.BranchId}", null, 400);
        }

        return await GetGroupByIdAsync(group.Id, ct);
    }

    public async Task<ApiResponse<InvModifierGroupDto>> UpdateGroupAsync(long id, UpdateInvModifierGroupDto dto, string username, CancellationToken ct = default)
    {
        var group = await _modifierRepository.GetGroupByIdAsync(id, ct);
        if (group == null)
            return ApiResponse<InvModifierGroupDto>.CreateFailure("Modifier group not found", null, 404);

        if (string.IsNullOrWhiteSpace(dto.GroupNameLocal))
            return ApiResponse<InvModifierGroupDto>.CreateFailure("GroupNameLocal is required", null, 400);

        var effectiveSelectionType = dto.SelectionType != 0 ? dto.SelectionType : group.SelectionType;
        var (isValid, errorMsg) = ValidateSelectionConfig(effectiveSelectionType, dto.MinSelections, dto.MaxSelections);
        if (!isValid)
            return ApiResponse<InvModifierGroupDto>.CreateFailure(errorMsg!, null, 400);

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

    public async Task<ApiResponse<InvModifierOptionDto>> UpsertOptionAsync(long groupId, UpsertInvModifierOptionDto dto, CancellationToken ct = default)
    {
        var group = await _modifierRepository.GetGroupByIdAsync(groupId, ct);
        if (group == null)
            return ApiResponse<InvModifierOptionDto>.CreateFailure("Modifier group not found", null, 404);

        var (validOpt, optError) = await ValidateOptionAsync(group.BranchId, dto, ct);
        if (!validOpt)
            return ApiResponse<InvModifierOptionDto>.CreateFailure(optError!, null, 400);

        InvModifierOption? option = null;
        if (dto.Id.HasValue && dto.Id.Value > 0)
        {
            option = await _modifierRepository.GetOptionByIdAsync(dto.Id.Value, ct);
            if (option == null || option.ModifierGroupId != groupId)
                return ApiResponse<InvModifierOptionDto>.CreateFailure("Modifier option not found in this group", null, 404);

            option.OptionNameLocal = dto.OptionNameLocal.Trim();
            option.OptionNameEn = dto.OptionNameEn?.Trim();
            option.PriceAdjustment = dto.PriceAdjustment;
            option.RelatedItemId = dto.RelatedItemId;
            option.RelatedItemQty = dto.RelatedItemQty > 0 ? dto.RelatedItemQty : 1m;
            option.RelatedUomCode = dto.RelatedUomCode?.Trim();
            option.UomFactor = dto.UomFactor > 0 ? dto.UomFactor : 1m;
            option.OptionType = dto.OptionType;
            option.ReplacesItemId = dto.ReplacesItemId;
            option.IsDefault = dto.IsDefault;
            option.SortOrder = dto.SortOrder;
            option.IsActive = dto.IsActive;

            await _modifierRepository.UpdateOptionAsync(option, ct);
        }
        else
        {
            option = new InvModifierOption
            {
                ModifierGroupId = groupId,
                OptionNameLocal = dto.OptionNameLocal.Trim(),
                OptionNameEn = dto.OptionNameEn?.Trim(),
                PriceAdjustment = dto.PriceAdjustment,
                RelatedItemId = dto.RelatedItemId,
                RelatedItemQty = dto.RelatedItemQty > 0 ? dto.RelatedItemQty : 1m,
                RelatedUomCode = dto.RelatedUomCode?.Trim(),
                UomFactor = dto.UomFactor > 0 ? dto.UomFactor : 1m,
                OptionType = dto.OptionType,
                ReplacesItemId = dto.ReplacesItemId,
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
        catch (Exception ex) when (ex.ToString().Contains("ORA-02291") || ex.ToString().Contains("FK_INV_MOD_OPT_ITM") || ex.ToString().Contains("FK_POS_MOD_OPT_ITM"))
        {
            return ApiResponse<InvModifierOptionDto>.CreateFailure("Related item not found", null, 404);
        }

        var resultDto = MapToOptionDto(option);
        return ApiResponse<InvModifierOptionDto>.CreateSuccess(resultDto);
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

            foreach (var gId in distinctIds)
            {
                var grp = await _modifierRepository.GetGroupByIdAsync(gId, ct);
                if (grp == null)
                    return ApiResponse<bool>.CreateFailure($"Modifier group {gId} not found", null, 404);
            }

            await _modifierRepository.ClearItemModifierGroupsAsync(itemId, ct);
            int sort = 1;
            foreach (var gId in distinctIds)
            {
                await _modifierRepository.AddItemModifierGroupAsync(new InvItemModifierGroup
                {
                    ItemId = itemId,
                    ModifierGroupId = gId,
                    SortOrder = sort++
                }, ct);
            }
        }
        else
        {
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

    private static (bool IsValid, string? ErrorMessage) ValidateSelectionConfig(ModifierSelectionType selectionType, int minSelections, int? maxSelections)
    {
        if (!Enum.IsDefined(typeof(ModifierSelectionType), selectionType))
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

    private async Task<(bool IsValid, string? ErrorMessage)> ValidateOptionAsync(long branchId, UpsertInvModifierOptionDto opt, CancellationToken ct)
    {
        if (opt == null || string.IsNullOrWhiteSpace(opt.OptionNameLocal))
            return (false, "OptionNameLocal is required for all options");

        if (opt.RelatedItemQty < 0)
            return (false, "RelatedItemQty cannot be negative");

        if (opt.UomFactor <= 0)
            opt.UomFactor = 1m;

        if (opt.RelatedItemId.HasValue && opt.RelatedItemId.Value > 0)
        {
            var relatedItem = await _itemRepository.GetByIdAsync(opt.RelatedItemId.Value, ct);
            if (relatedItem == null)
                return (false, $"Related item {opt.RelatedItemId.Value} not found");

            if (!relatedItem.IsActive)
                return (false, $"Related item '{relatedItem.ItemNameLocal}' is inactive");

            if (relatedItem.BranchId != branchId)
                return (false, $"Related item '{relatedItem.ItemNameLocal}' belongs to branch {relatedItem.BranchId}, but this modifier group is for branch {branchId}");

            bool isStock = relatedItem.ItemType == ItemType.Stock || relatedItem.ItemType == ItemType.Assembly || relatedItem.ItemType == ItemType.Kit;
            if (!isStock)
            {
                var bom = await _bomRepository.GetDefaultByParentItemIdAsync(relatedItem.Id, ct);
                if (bom == null)
                {
                    return (false, $"Related item '{relatedItem.ItemNameLocal}' must be a stock item or have an active Bill of Materials (BOM)");
                }
            }
        }

        if (opt.OptionType == OptionType.SUBSTITUTE)
        {
            if (!opt.ReplacesItemId.HasValue || opt.ReplacesItemId.Value <= 0)
                return (false, "ReplacesItemId is required when OptionType is SUBSTITUTE");

            var replacesItem = await _itemRepository.GetByIdAsync(opt.ReplacesItemId.Value, ct);
            if (replacesItem == null)
                return (false, $"Replaces item {opt.ReplacesItemId.Value} not found");

            if (!replacesItem.IsActive)
                return (false, $"Replaces item '{replacesItem.ItemNameLocal}' is inactive");

            if (replacesItem.BranchId != branchId)
                return (false, $"Replaces item '{replacesItem.ItemNameLocal}' belongs to branch {replacesItem.BranchId}, but this modifier group is for branch {branchId}");
        }

        return (true, null);
    }

    public async Task<ApiResponse<List<ModifierConsumptionReportItemDto>>> GetConsumptionReportAsync(
        long branchId,
        DateTime fromDate,
        DateTime toDate,
        long? itemId = null,
        CancellationToken ct = default)
    {
        if (_posOrderRepository == null)
        {
            return ApiResponse<List<ModifierConsumptionReportItemDto>>.CreateSuccess(new List<ModifierConsumptionReportItemDto>());
        }

        var (orders, _) = await _posOrderRepository.GetOrdersPagedAsync(
            branchId,
            fromDate: fromDate,
            toDate: toDate,
            pageIndex: 1,
            pageSize: 50000,
            ct: ct);

        var validOrders = orders.Where(o => (o.Status == PosOrderStatus.Completed || o.IsPaid) && o.Status != PosOrderStatus.Voided).ToList();

        var lineModifiers = validOrders
            .SelectMany(o => o.Lines.Where(l => !l.IsVoided && l.Quantity > 0))
            .SelectMany(l => l.Modifiers.Select(m => new { Line = l, Modifier = m }))
            .ToList();

        if (!lineModifiers.Any())
        {
            return ApiResponse<List<ModifierConsumptionReportItemDto>>.CreateSuccess(new List<ModifierConsumptionReportItemDto>());
        }

        var optionIds = lineModifiers
            .Where(x => x.Modifier.ModifierOptionId.HasValue)
            .Select(x => x.Modifier.ModifierOptionId!.Value)
            .Distinct()
            .ToList();

        var optionsMap = optionIds.Any()
            ? (await _modifierRepository.GetOptionsByIdsAsync(optionIds, ct)).ToDictionary(o => o.Id)
            : new Dictionary<long, InvModifierOption>();

        var consumptionBuckets = new Dictionary<(long ItemId, long? OptionId), decimal>();

        foreach (var entry in lineModifiers)
        {
            var line = entry.Line;
            var mod = entry.Modifier;

            InvModifierOption? opt = null;
            if (mod.ModifierOptionId.HasValue && optionsMap.TryGetValue(mod.ModifierOptionId.Value, out var foundOpt))
            {
                opt = foundOpt;
            }

            long? consumedItemId = opt?.RelatedItemId ?? (mod.ModifierItemId > 0 ? mod.ModifierItemId : null);
            if (!consumedItemId.HasValue) continue;

            if (itemId.HasValue && consumedItemId.Value != itemId.Value) continue;

            if (opt != null && opt.OptionType != OptionType.ADD && opt.OptionType != OptionType.SUBSTITUTE)
            {
                continue;
            }

            var relQty = opt?.RelatedItemQty > 0 ? opt.RelatedItemQty : 1m;
            var factor = opt?.UomFactor > 0 ? opt.UomFactor : 1m;
            var modQty = mod.Quantity > 0 ? mod.Quantity : 1m;

            var totalQty = line.Quantity * modQty * relQty * factor;

            var key = (consumedItemId.Value, mod.ModifierOptionId);
            if (consumptionBuckets.ContainsKey(key))
            {
                consumptionBuckets[key] += totalQty;
            }
            else
            {
                consumptionBuckets[key] = totalQty;
            }
        }

        var results = new List<ModifierConsumptionReportItemDto>();

        foreach (var kvp in consumptionBuckets)
        {
            var (cItemId, cOptionId) = kvp.Key;
            var totalQty = kvp.Value;

            var item = await _itemRepository.GetByIdAsync(cItemId, ct);
            if (item == null) continue;

            InvModifierOption? opt = null;
            if (cOptionId.HasValue && optionsMap.TryGetValue(cOptionId.Value, out var foundOpt))
            {
                opt = foundOpt;
            }

            var unitCost = item.StandardCost;
            var totalCost = totalQty * unitCost;

            results.Add(new ModifierConsumptionReportItemDto
            {
                ItemId = item.Id,
                ItemCode = item.ItemCode,
                ItemNameLocal = item.ItemNameLocal,
                ItemNameEn = item.ItemNameEn,
                UomCode = opt?.RelatedUomCode ?? item.UomBase.ToString(),
                TotalQuantityConsumed = totalQty,
                UnitCost = unitCost,
                TotalCost = totalCost,
                ModifierOptionId = cOptionId,
                ModifierOptionName = opt?.OptionNameLocal,
                BranchId = branchId
            });
        }

        return ApiResponse<List<ModifierConsumptionReportItemDto>>.CreateSuccess(
            results.OrderBy(r => r.ItemCode).ThenBy(r => r.ModifierOptionName).ToList());
    }

    private static InvModifierGroupDto MapToGroupDto(InvModifierGroup g)
    {
        return new InvModifierGroupDto
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

    private static InvModifierOptionDto MapToOptionDto(InvModifierOption o)
    {
        return new InvModifierOptionDto
        {
            Id = o.Id,
            ModifierGroupId = o.ModifierGroupId,
            OptionNameLocal = o.OptionNameLocal,
            OptionNameEn = o.OptionNameEn,
            PriceAdjustment = o.PriceAdjustment,
            RelatedItemId = o.RelatedItemId,
            RelatedItemName = o.RelatedItem?.ItemNameLocal,
            RelatedItemQty = o.RelatedItemQty,
            RelatedUomCode = o.RelatedUomCode,
            UomFactor = o.UomFactor,
            OptionType = o.OptionType,
            ReplacesItemId = o.ReplacesItemId,
            ReplacesItemName = o.ReplacesItem?.ItemNameLocal,
            IsDefault = o.IsDefault,
            SortOrder = o.SortOrder,
            IsActive = o.IsActive
        };
    }
}
