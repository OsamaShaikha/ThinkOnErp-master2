using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using ThinkOnErp.Application.Common;
using ThinkOnErp.Application.DTOs.Inventory.Warehouses;
using ThinkOnErp.Application.Mappings.Inventory;
using ThinkOnErp.Domain.Entities.Inventory;
using ThinkOnErp.Domain.Entities.Inventory.Enums;
using ThinkOnErp.Domain.Interfaces.Inventory;

namespace ThinkOnErp.Application.Services.Inventory;

public sealed class InvWarehouseService : IInvWarehouseService
{
    private readonly IInvWarehouseRepository _warehouseRepository;
    private readonly IInvStockBalanceRepository _stockBalanceRepository;
    private readonly ILogger<InvWarehouseService> _logger;

    public InvWarehouseService(
        IInvWarehouseRepository warehouseRepository,
        IInvStockBalanceRepository stockBalanceRepository,
        ILogger<InvWarehouseService> logger)
    {
        _warehouseRepository = warehouseRepository;
        _stockBalanceRepository = stockBalanceRepository;
        _logger = logger;
    }

    public async Task<ApiResponse<InvWarehouseDto>> CreateAsync(CreateInvWarehouseDto request, CancellationToken cancellationToken = default)
    {
        var existing = await _warehouseRepository.GetByCodeAsync(request.WarehouseCode, cancellationToken);
        if (existing != null)
            return ApiResponse<InvWarehouseDto>.CreateFailure("Warehouse code already exists", null, 400);

        Enum.TryParse<WarehouseType>(request.WarehouseType, true, out var whType);

        var warehouse = new InvWarehouse
        {
            BranchId = request.BranchId,
            WarehouseCode = request.WarehouseCode,
            WarehouseNameLocal = request.WarehouseNameLocal.Trim(),
            WarehouseNameEn = request.WarehouseNameEn?.Trim() ?? string.Empty,
            WarehouseType = whType,
            Address = request.Address?.Trim(),
            EnableBinTracking = request.EnableBinTracking,
            IsActive = true,
            CreationUser = "admin",
            CreationDate = DateTime.UtcNow
        };

        await _warehouseRepository.AddAsync(warehouse, cancellationToken);
        return ApiResponse<InvWarehouseDto>.CreateSuccess(InvWarehouseMapper.ToDto(warehouse), "Warehouse created successfully", 201);
    }

    public async Task<ApiResponse<InvWarehouseDto>> UpdateAsync(long id, UpdateInvWarehouseDto request, CancellationToken cancellationToken = default)
    {
        var warehouse = await _warehouseRepository.GetByIdAsync(id, cancellationToken);
        if (warehouse == null)
            return ApiResponse<InvWarehouseDto>.CreateFailure("Warehouse not found", null, 404);

        if (!string.IsNullOrWhiteSpace(request.WarehouseNameLocal)) warehouse.WarehouseNameLocal = request.WarehouseNameLocal.Trim();
        if (!string.IsNullOrWhiteSpace(request.WarehouseNameEn)) warehouse.WarehouseNameEn = request.WarehouseNameEn.Trim();
        if (!string.IsNullOrWhiteSpace(request.WarehouseType) && Enum.TryParse<WarehouseType>(request.WarehouseType, true, out var wt)) warehouse.WarehouseType = wt;
        if (request.Address != null) warehouse.Address = request.Address.Trim();
        if (request.EnableBinTracking.HasValue) warehouse.EnableBinTracking = request.EnableBinTracking.Value;

        warehouse.UpdateUser = "admin";
        warehouse.UpdateDate = DateTime.UtcNow;
        await _warehouseRepository.UpdateAsync(warehouse, cancellationToken);
        return ApiResponse<InvWarehouseDto>.CreateSuccess(InvWarehouseMapper.ToDto(warehouse), "Warehouse updated successfully");
    }

    public async Task<ApiResponse<InvWarehouseDto>> GetByIdAsync(long id, CancellationToken cancellationToken = default)
    {
        var warehouse = await _warehouseRepository.GetByIdAsync(id, cancellationToken);
        if (warehouse == null)
            return ApiResponse<InvWarehouseDto>.CreateFailure("Warehouse not found", null, 404);

        return ApiResponse<InvWarehouseDto>.CreateSuccess(InvWarehouseMapper.ToDto(warehouse));
    }

    public async Task<ApiResponse<List<InvWarehouseDto>>> GetAllByBranchAsync(long branchId, CancellationToken cancellationToken = default)
    {
        var list = await _warehouseRepository.GetAllByBranchAsync(branchId, cancellationToken);
        return ApiResponse<List<InvWarehouseDto>>.CreateSuccess(list.Select(InvWarehouseMapper.ToDto).ToList());
    }

    public async Task<ApiResponse<bool>> DeleteWarehouseAsync(long id, CancellationToken cancellationToken = default)
    {
        var warehouse = await _warehouseRepository.GetByIdAsync(id, cancellationToken);
        if (warehouse == null)
            return ApiResponse<bool>.CreateFailure("Warehouse not found", null, 404);

        await _warehouseRepository.DeleteAsync(id, cancellationToken);
        return ApiResponse<bool>.CreateSuccess(true, "Warehouse deactivated successfully");
    }

    public async Task<ApiResponse<bool>> AddZoneAsync(long warehouseId, CreateInvZoneDto request, CancellationToken cancellationToken = default)
    {
        var warehouse = await _warehouseRepository.GetByIdAsync(warehouseId, cancellationToken);
        if (warehouse == null)
            return ApiResponse<bool>.CreateFailure("Warehouse not found", null, 404);

        Enum.TryParse<ZoneType>(request.ZoneType, true, out var zoneType);

        var zone = new InvZone
        {
            WarehouseId = warehouseId,
            ZoneCode = request.ZoneCode,
            ZoneName = request.ZoneName.Trim(),
            ZoneType = zoneType
        };

        warehouse.Zones.Add(zone);
        await _warehouseRepository.UpdateAsync(warehouse, cancellationToken);
        return ApiResponse<bool>.CreateSuccess(true, "Zone added successfully", 201);
    }

    public async Task<ApiResponse<bool>> UpdateZoneAsync(long zoneId, UpdateInvZoneDto request, CancellationToken cancellationToken = default)
    {
        var zone = await _warehouseRepository.GetZoneByIdAsync(zoneId, cancellationToken);
        if (zone == null)
            return ApiResponse<bool>.CreateFailure("Zone not found", null, 404);

        if (!string.IsNullOrWhiteSpace(request.ZoneName)) zone.ZoneName = request.ZoneName.Trim();
        if (!string.IsNullOrWhiteSpace(request.ZoneType) && Enum.TryParse<ZoneType>(request.ZoneType, true, out var zt)) zone.ZoneType = zt;

        await _warehouseRepository.UpdateZoneAsync(zone, cancellationToken);
        return ApiResponse<bool>.CreateSuccess(true, "Zone updated successfully");
    }

    public async Task<ApiResponse<bool>> DeleteZoneAsync(long zoneId, CancellationToken cancellationToken = default)
    {
        var zone = await _warehouseRepository.GetZoneByIdAsync(zoneId, cancellationToken);
        if (zone == null)
            return ApiResponse<bool>.CreateFailure("Zone not found", null, 404);

        await _warehouseRepository.DeleteZoneAsync(zoneId, cancellationToken);
        return ApiResponse<bool>.CreateSuccess(true, "Zone deleted successfully");
    }

    public async Task<ApiResponse<bool>> AddBinAsync(long zoneId, CreateInvBinDto request, CancellationToken cancellationToken = default)
    {
        var zone = await _warehouseRepository.GetZoneByIdAsync(zoneId, cancellationToken);
        if (zone == null)
            return ApiResponse<bool>.CreateFailure("Zone not found", null, 404);

        var bin = new InvBin
        {
            ZoneId = zoneId,
            BinCode = request.BinCode,
            MaxWeight = request.MaxWeight,
            MaxVolume = request.MaxVolume,
            IsActive = true
        };

        zone.Bins.Add(bin);
        await _warehouseRepository.UpdateZoneAsync(zone, cancellationToken);
        return ApiResponse<bool>.CreateSuccess(true, "Bin added successfully", 201);
    }

    public async Task<ApiResponse<bool>> UpdateBinAsync(long binId, UpdateInvBinDto request, CancellationToken cancellationToken = default)
    {
        var bin = await _warehouseRepository.GetBinByIdAsync(binId, cancellationToken);
        if (bin == null)
            return ApiResponse<bool>.CreateFailure("Bin not found", null, 404);

        if (request.BinCode.HasValue) bin.BinCode = request.BinCode.Value;
        if (request.MaxWeight.HasValue) bin.MaxWeight = request.MaxWeight;
        if (request.MaxVolume.HasValue) bin.MaxVolume = request.MaxVolume;
        if (request.IsActive.HasValue) bin.IsActive = request.IsActive.Value;

        await _warehouseRepository.UpdateBinAsync(bin, cancellationToken);
        return ApiResponse<bool>.CreateSuccess(true, "Bin updated successfully");
    }

    public async Task<ApiResponse<bool>> DeleteBinAsync(long binId, CancellationToken cancellationToken = default)
    {
        var bin = await _warehouseRepository.GetBinByIdAsync(binId, cancellationToken);
        if (bin == null)
            return ApiResponse<bool>.CreateFailure("Bin not found", null, 404);

        await _warehouseRepository.DeleteBinAsync(binId, cancellationToken);
        return ApiResponse<bool>.CreateSuccess(true, "Bin deleted successfully");
    }

    public async Task<ApiResponse<object>> GetStockSummaryAsync(long warehouseId, CancellationToken cancellationToken = default)
    {
        var balances = await _stockBalanceRepository.GetByWarehouseAsync(warehouseId, cancellationToken);
        var totalQty = balances.Sum(b => b.OnHandQty);
        var totalVal = balances.Sum(b => b.OnHandQty * b.AvgCost);
        return ApiResponse<object>.CreateSuccess(new
        {
            WarehouseId = warehouseId,
            ItemCount = balances.Count,
            TotalOnHandQuantity = totalQty,
            TotalValuation = totalVal
        });
    }
}
