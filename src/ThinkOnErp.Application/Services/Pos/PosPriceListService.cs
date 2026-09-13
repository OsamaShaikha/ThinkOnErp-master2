using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using ThinkOnErp.Application.Common;
using ThinkOnErp.Application.DTOs.Pos;
using ThinkOnErp.Domain.Entities.Pos;
using ThinkOnErp.Domain.Entities.Pos.Enums;
using ThinkOnErp.Domain.Interfaces.Pos;

namespace ThinkOnErp.Application.Services.Pos;

public interface IPosPriceListService
{
    Task<ApiResponse<IReadOnlyList<PriceListDto>>> GetPriceListsByBranchAsync(long branchId, PosOrderType? orderType = null, bool? isActive = null, CancellationToken ct = default);
    Task<ApiResponse<PriceListDetailsDto>> GetPriceListByIdAsync(long id, CancellationToken ct = default);
    Task<ApiResponse<PriceListDetailsDto>> CreatePriceListAsync(CreatePriceListDto dto, string username, CancellationToken ct = default);
    Task<ApiResponse<PriceListDetailsDto>> UpdatePriceListAsync(long id, UpdatePriceListDto dto, string username, CancellationToken ct = default);
    Task<ApiResponse<bool>> DeletePriceListAsync(long id, CancellationToken ct = default);
    Task<ApiResponse<PriceListItemDto>> UpsertItemAsync(long priceListId, UpsertPriceListItemDto dto, CancellationToken ct = default);
    Task<ApiResponse<bool>> RemoveItemAsync(long priceListId, long itemId, CancellationToken ct = default);
}

public class PosPriceListService : IPosPriceListService
{
    private readonly IPosPriceListRepository _priceListRepository;

    public PosPriceListService(IPosPriceListRepository priceListRepository)
    {
        _priceListRepository = priceListRepository;
    }

    public async Task<ApiResponse<IReadOnlyList<PriceListDto>>> GetPriceListsByBranchAsync(long branchId, PosOrderType? orderType = null, bool? isActive = null, CancellationToken ct = default)
    {
        var lists = await _priceListRepository.GetPriceListsByBranchAsync(branchId, orderType, isActive, ct);
        var dtos = lists.Select(p => new PriceListDto
        {
            Id = p.Id,
            BranchId = p.BranchId,
            PriceListCode = p.PriceListCode,
            PriceListNameLocal = p.PriceListNameLocal,
            PriceListNameEn = p.PriceListNameEn,
            ApplicableOrderType = p.ApplicableOrderType,
            CurrencyId = p.CurrencyId,
            IsDefault = p.IsDefault,
            IsActive = p.IsActive,
            ItemCount = p.Items?.Count ?? 0
        }).ToList();

        return ApiResponse<IReadOnlyList<PriceListDto>>.CreateSuccess(dtos);
    }

    public async Task<ApiResponse<PriceListDetailsDto>> GetPriceListByIdAsync(long id, CancellationToken ct = default)
    {
        var p = await _priceListRepository.GetPriceListByIdAsync(id, ct);
        if (p == null)
            return ApiResponse<PriceListDetailsDto>.CreateFailure("Price list not found", null, 404);

        return ApiResponse<PriceListDetailsDto>.CreateSuccess(MapToDetailsDto(p));
    }

    public async Task<ApiResponse<PriceListDetailsDto>> CreatePriceListAsync(CreatePriceListDto dto, string username, CancellationToken ct = default)
    {
        var existing = await _priceListRepository.GetByCodeAsync(dto.BranchId, dto.PriceListCode, ct);
        if (existing != null)
            return ApiResponse<PriceListDetailsDto>.CreateFailure($"Price list code '{dto.PriceListCode}' already exists for this branch", null, 400);

        var list = new PosPriceList
        {
            BranchId = dto.BranchId,
            PriceListCode = dto.PriceListCode.Trim().ToUpper(),
            PriceListNameLocal = dto.PriceListNameLocal.Trim(),
            PriceListNameEn = dto.PriceListNameEn?.Trim(),
            ApplicableOrderType = dto.ApplicableOrderType,
            CurrencyId = dto.CurrencyId,
            IsDefault = dto.IsDefault,
            IsActive = true,
            CreationUser = username,
            CreationDate = DateTime.UtcNow
        };

        if (dto.Items != null && dto.Items.Count > 0)
        {
            foreach (var item in dto.Items)
            {
                list.Items.Add(new PosPriceListItem
                {
                    ItemId = item.ItemId,
                    Price = item.Price,
                    MinPrice = item.MinPrice
                });
            }
        }

        await _priceListRepository.AddPriceListAsync(list, ct);
        await _priceListRepository.SaveChangesAsync(ct);

        return ApiResponse<PriceListDetailsDto>.CreateSuccess(MapToDetailsDto(list), "Price list created successfully");
    }

    public async Task<ApiResponse<PriceListDetailsDto>> UpdatePriceListAsync(long id, UpdatePriceListDto dto, string username, CancellationToken ct = default)
    {
        var list = await _priceListRepository.GetPriceListByIdAsync(id, ct);
        if (list == null)
            return ApiResponse<PriceListDetailsDto>.CreateFailure("Price list not found", null, 404);

        list.PriceListNameLocal = dto.PriceListNameLocal.Trim();
        list.PriceListNameEn = dto.PriceListNameEn?.Trim();
        list.ApplicableOrderType = dto.ApplicableOrderType;
        list.CurrencyId = dto.CurrencyId;
        list.IsDefault = dto.IsDefault;
        list.IsActive = dto.IsActive;

        await _priceListRepository.UpdatePriceListAsync(list, ct);
        await _priceListRepository.SaveChangesAsync(ct);

        return ApiResponse<PriceListDetailsDto>.CreateSuccess(MapToDetailsDto(list), "Price list updated successfully");
    }

    public async Task<ApiResponse<bool>> DeletePriceListAsync(long id, CancellationToken ct = default)
    {
        var list = await _priceListRepository.GetPriceListByIdAsync(id, ct);
        if (list == null)
            return ApiResponse<bool>.CreateFailure("Price list not found", null, 404);

        list.IsActive = false;
        await _priceListRepository.UpdatePriceListAsync(list, ct);
        await _priceListRepository.SaveChangesAsync(ct);

        return ApiResponse<bool>.CreateSuccess(true, "Price list deactivated successfully");
    }

    public async Task<ApiResponse<PriceListItemDto>> UpsertItemAsync(long priceListId, UpsertPriceListItemDto dto, CancellationToken ct = default)
    {
        var list = await _priceListRepository.GetPriceListByIdAsync(priceListId, ct);
        if (list == null)
            return ApiResponse<PriceListItemDto>.CreateFailure("Price list not found", null, 404);

        var existingItem = await _priceListRepository.GetPriceListItemByIdAsync(priceListId, dto.ItemId, ct);
        if (existingItem != null)
        {
            existingItem.Price = dto.Price;
            existingItem.MinPrice = dto.MinPrice;
            await _priceListRepository.UpdatePriceListItemAsync(existingItem, ct);
        }
        else
        {
            existingItem = new PosPriceListItem
            {
                PriceListId = priceListId,
                ItemId = dto.ItemId,
                Price = dto.Price,
                MinPrice = dto.MinPrice
            };
            await _priceListRepository.AddPriceListItemAsync(existingItem, ct);
        }

        await _priceListRepository.SaveChangesAsync(ct);

        return ApiResponse<PriceListItemDto>.CreateSuccess(new PriceListItemDto
        {
            Id = existingItem.Id,
            PriceListId = existingItem.PriceListId,
            ItemId = existingItem.ItemId,
            Price = existingItem.Price,
            MinPrice = existingItem.MinPrice
        }, "Price list item saved successfully");
    }

    public async Task<ApiResponse<bool>> RemoveItemAsync(long priceListId, long itemId, CancellationToken ct = default)
    {
        var existingItem = await _priceListRepository.GetPriceListItemByIdAsync(priceListId, itemId, ct);
        if (existingItem == null)
            return ApiResponse<bool>.CreateFailure("Price list item not found", null, 404);

        await _priceListRepository.DeletePriceListItemAsync(existingItem, ct);
        await _priceListRepository.SaveChangesAsync(ct);

        return ApiResponse<bool>.CreateSuccess(true, "Price list item removed successfully");
    }

    private static PriceListDetailsDto MapToDetailsDto(PosPriceList p)
    {
        return new PriceListDetailsDto
        {
            Id = p.Id,
            BranchId = p.BranchId,
            PriceListCode = p.PriceListCode,
            PriceListNameLocal = p.PriceListNameLocal,
            PriceListNameEn = p.PriceListNameEn,
            ApplicableOrderType = p.ApplicableOrderType,
            CurrencyId = p.CurrencyId,
            IsDefault = p.IsDefault,
            IsActive = p.IsActive,
            ItemCount = p.Items?.Count ?? 0,
            Items = p.Items?.Select(i => new PriceListItemDto
            {
                Id = i.Id,
                PriceListId = i.PriceListId,
                ItemId = i.ItemId,
                ItemCode = i.Item?.ItemCode,
                ItemName = i.Item?.ItemNameLocal,
                Price = i.Price,
                MinPrice = i.MinPrice
            }).ToList() ?? new List<PriceListItemDto>()
        };
    }
}
