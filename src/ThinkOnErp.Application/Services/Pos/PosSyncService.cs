using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using ThinkOnErp.Application.Common;
using ThinkOnErp.Application.DTOs.Pos;
using ThinkOnErp.Domain.Entities.Pos;
using ThinkOnErp.Domain.Entities.Pos.Enums;
using ThinkOnErp.Domain.Interfaces.Inventory;
using ThinkOnErp.Domain.Interfaces.Pos;

namespace ThinkOnErp.Application.Services.Pos;

public class PosSyncService : IPosSyncService
{
    private readonly IPosOrderService _orderService;
    private readonly IPosOrderRepository _orderRepository;
    private readonly IPosPromotionRepository _promotionRepository;
    private readonly IPosTableRepository _tableRepository;
    private readonly IInvItemRepository _itemRepository;
    private readonly IInvStockBalanceRepository _stockBalanceRepository;
    private readonly IPosModifierRepository _modifierRepository;

    public PosSyncService(
        IPosOrderService orderService,
        IPosOrderRepository orderRepository,
        IPosPromotionRepository promotionRepository,
        IPosTableRepository tableRepository,
        IInvItemRepository itemRepository,
        IInvStockBalanceRepository stockBalanceRepository,
        IPosModifierRepository modifierRepository)
    {
        _orderService = orderService;
        _orderRepository = orderRepository;
        _promotionRepository = promotionRepository;
        _tableRepository = tableRepository;
        _itemRepository = itemRepository;
        _stockBalanceRepository = stockBalanceRepository;
        _modifierRepository = modifierRepository;
    }

    public async Task<ApiResponse<PullCatalogSyncDto>> PullCatalogSyncAsync(long branchId, DateTime? lastSyncTime, CancellationToken ct = default)
    {
        var result = new PullCatalogSyncDto { ServerTime = DateTime.UtcNow };

        // 1. Items
        var (items, _) = await _itemRepository.GetAllAsync(null, 1, 10000, ct);
        result.Items = items.Where(i => i.IsActive && i.ShowInPos).Select(i => new SyncItemDto
        {
            Id = i.Id,
            ItemCode = i.ItemCode,
            ItemNameLocal = i.ItemNameLocal,
            ItemNameEn = i.ItemNameEn,
            GroupId = i.MainGroupId,
            UomId = i.UomBase,
            StandardPrice = i.DefaultSellingPrice > 0 ? i.DefaultSellingPrice : i.StandardCost,
            IsScaleItem = false,
            Barcode = i.ItemCode
        }).ToList();

        // 2. Promotions
        var promos = await _promotionRepository.GetActivePromotionsForBranchAsync(branchId, DateTime.UtcNow, ct);
        result.Promotions = promos.Select(p => new SyncPromotionDto
        {
            Id = p.Id,
            PromotionCode = p.PromotionCode,
            PromotionName = p.PromotionNameLocal,
            PromotionType = (int)p.PromotionType,
            StartDate = p.StartDate,
            EndDate = p.EndDate
        }).ToList();

        // 3. Floors and Tables
        var floors = await _tableRepository.GetFloorsWithTablesAsync(branchId, ct);
        result.Floors = floors.Select(f => new SyncFloorDto
        {
            Id = f.Id,
            FloorName = f.FloorName,
            Tables = f.Tables.Select(t => new SyncTableDto
            {
                Id = t.Id,
                TableNumber = t.TableNumber,
                Capacity = t.Capacity,
                PositionX = t.PositionX,
                PositionY = t.PositionY,
                Width = t.Width,
                Height = t.Height,
                Shape = t.Shape
            }).ToList()
        }).ToList();

        // 4. Modifiers
        var groups = await _modifierRepository.GetGroupsByBranchAsync(branchId, true, ct);
        result.ModifierGroups = groups.Select(g => new PosModifierGroupDto
        {
            Id = g.Id,
            BranchId = g.BranchId,
            GroupCode = g.GroupCode,
            GroupNameLocal = g.GroupNameLocal,
            GroupNameEn = g.GroupNameEn,
            IsRequired = g.IsRequired,
            SelectionType = (PosSelectionType)g.SelectionType,
            MinSelections = g.MinSelections,
            MaxSelections = g.MaxSelections,
            SortOrder = g.SortOrder,
            IsActive = g.IsActive,
            Options = g.Options.Where(o => o.IsActive).Select(o => new PosModifierOptionDto
            {
                Id = o.Id,
                ModifierGroupId = o.ModifierGroupId,
                OptionNameLocal = o.OptionNameLocal,
                OptionNameEn = o.OptionNameEn,
                PriceAdjustment = o.PriceAdjustment,
                RelatedItemId = o.RelatedItemId,
                IsDefault = o.IsDefault,
                SortOrder = o.SortOrder,
                IsActive = o.IsActive
            }).ToList(),
            LinkedItemIds = g.ItemLinks.Select(l => l.ItemId).ToList()
        }).ToList();

        return ApiResponse<PullCatalogSyncDto>.CreateSuccess(result);
    }

    public async Task<ApiResponse<PushOfflineOrdersResultDto>> PushOfflineOrdersAsync(long branchId, List<CreatePosOrderDto> orders, string username, CancellationToken ct = default)
    {
        var result = new PushOfflineOrdersResultDto
        {
            TotalProcessed = orders.Count
        };

        foreach (var orderDto in orders)
        {
            orderDto.BranchId = branchId;
            try
            {
                // Process order creation
                var createResult = await _orderService.CreateOrderAsync(orderDto, username, ct);
                if (createResult.Success && createResult.Data != null)
                {
                    bool conflictDetected = false;

                    // Section 3 of BRD: Check for offline inventory conflict
                    foreach (var line in orderDto.Lines)
                    {
                        var stockBalances = await _stockBalanceRepository.GetByItemAsync(line.ItemId, ct);
                        var totalStockAvailable = stockBalances.Sum(b => b.OnHandQty - b.ReservedQty);

                        if (totalStockAvailable < line.Quantity)
                        {
                            conflictDetected = true;
                            result.ConflictCount++;

                            var conflict = new PosInventoryConflict
                            {
                                BranchId = branchId,
                                OrderId = createResult.Data.Id,
                                ItemId = line.ItemId,
                                SoldQuantity = line.Quantity,
                                AvailableStockAtSync = totalStockAvailable,
                                DeficitQuantity = line.Quantity - totalStockAvailable,
                                Status = PosInventoryConflictStatus.PendingReview,
                                CreationUser = username,
                                CreationDate = DateTime.UtcNow
                            };

                            await _orderRepository.AddInventoryConflictAsync(conflict, ct);
                        }
                    }

                    if (conflictDetected)
                    {
                        await _orderRepository.SaveChangesAsync(ct);
                    }

                    result.SuccessCount++;
                    result.SyncedOrders.Add(new OrderSyncResultItem
                    {
                        ClientUuid = orderDto.ClientUuid,
                        ServerOrderId = createResult.Data.Id,
                        OrderNumber = createResult.Data.OrderNumber,
                        HasConflict = conflictDetected,
                        Message = conflictDetected ? "Order synced with inventory deficit conflict" : "Synced successfully"
                    });
                }
                else
                {
                    result.SyncedOrders.Add(new OrderSyncResultItem
                    {
                        ClientUuid = orderDto.ClientUuid,
                        ServerOrderId = 0,
                        OrderNumber = string.Empty,
                        HasConflict = false,
                        Message = createResult.Message
                    });
                }
            }
            catch (Exception ex)
            {
                result.SyncedOrders.Add(new OrderSyncResultItem
                {
                    ClientUuid = orderDto.ClientUuid,
                    ServerOrderId = 0,
                    OrderNumber = string.Empty,
                    HasConflict = false,
                    Message = $"Error syncing order: {ex.Message}"
                });
            }
        }

        return ApiResponse<PushOfflineOrdersResultDto>.CreateSuccess(result, $"Processed {result.TotalProcessed} offline orders");
    }
}
