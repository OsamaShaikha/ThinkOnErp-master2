using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using ThinkOnErp.Application.Common;
using ThinkOnErp.Application.DTOs.Inventory.Bom;
using ThinkOnErp.Application.Mappings.Inventory;
using ThinkOnErp.Domain.Entities.Inventory;
using ThinkOnErp.Domain.Interfaces.Inventory;

namespace ThinkOnErp.Application.Services.Inventory;

public sealed class InvBomService : IInvBomService
{
    private readonly IInvBomRepository _bomRepository;
    private readonly ILogger<InvBomService> _logger;

    public InvBomService(IInvBomRepository bomRepository, ILogger<InvBomService> logger)
    {
        _bomRepository = bomRepository;
        _logger = logger;
    }

    public async Task<ApiResponse<InvBomDto>> CreateBomAsync(CreateInvBomDto dto, string username, CancellationToken ct = default)
    {
        if (await _bomRepository.ExistsAsync(dto.BomCode.Trim(), null, ct))
            return ApiResponse<InvBomDto>.CreateFailure($"BOM code '{dto.BomCode}' already exists", null, 400);

        var bom = InvBomMapper.ToEntity(dto, username);
        var created = await _bomRepository.CreateAsync(bom, ct);
        return ApiResponse<InvBomDto>.CreateSuccess(InvBomMapper.ToDto(created), "BOM recipe created successfully", 201);
    }

    public async Task<ApiResponse<InvBomDto>> GetByIdAsync(long id, CancellationToken ct = default)
    {
        var bom = await _bomRepository.GetByIdAsync(id, ct);
        if (bom == null)
            return ApiResponse<InvBomDto>.CreateFailure("BOM recipe not found", null, 404);

        return ApiResponse<InvBomDto>.CreateSuccess(InvBomMapper.ToDto(bom));
    }

    public async Task<ApiResponse<InvBomDto>> GetDefaultByParentItemIdAsync(long parentItemId, CancellationToken ct = default)
    {
        var bom = await _bomRepository.GetDefaultByParentItemIdAsync(parentItemId, ct);
        if (bom == null)
            return ApiResponse<InvBomDto>.CreateFailure("No default BOM found for item", null, 404);

        return ApiResponse<InvBomDto>.CreateSuccess(InvBomMapper.ToDto(bom));
    }

    public async Task<ApiResponse<List<InvBomDto>>> GetAllByParentItemIdAsync(long parentItemId, CancellationToken ct = default)
    {
        var list = await _bomRepository.GetAllByParentItemIdAsync(parentItemId, ct);
        return ApiResponse<List<InvBomDto>>.CreateSuccess(list.Select(InvBomMapper.ToDto).ToList());
    }

    public async Task<ApiResponse<(List<InvBomDto> Items, int TotalCount)>> GetPagedAsync(long? branchId, int pageIndex, int pageSize, CancellationToken ct = default)
    {
        var (items, total) = await _bomRepository.GetPagedAsync(branchId, pageIndex, pageSize, ct);
        var dtos = items.Select(InvBomMapper.ToDto).ToList();
        return ApiResponse<(List<InvBomDto> Items, int TotalCount)>.CreateSuccess((dtos, total));
    }

    public async Task<ApiResponse<InvBomDto>> UpdateBomAsync(long id, UpdateInvBomDto dto, string username, CancellationToken ct = default)
    {
        var bom = await _bomRepository.GetByIdAsync(id, ct);
        if (bom == null)
            return ApiResponse<InvBomDto>.CreateFailure("BOM recipe not found", null, 404);

        if (!string.IsNullOrWhiteSpace(dto.BomNameAr)) bom.BomNameAr = dto.BomNameAr.Trim();
        if (dto.BomNameEn != null) bom.BomNameEn = dto.BomNameEn.Trim();
        if (dto.OutputQty.HasValue) bom.OutputQty = dto.OutputQty.Value;
        if (!string.IsNullOrWhiteSpace(dto.UomCode)) bom.UomCode = dto.UomCode.Trim();
        if (dto.BomType.HasValue) bom.BomType = dto.BomType.Value;
        if (dto.LaborCost.HasValue) bom.LaborCost = dto.LaborCost.Value;
        if (dto.OverheadCost.HasValue) bom.OverheadCost = dto.OverheadCost.Value;
        if (dto.IsDefault.HasValue) bom.IsDefault = dto.IsDefault.Value;
        if (dto.Notes != null) bom.Notes = dto.Notes.Trim();

        if (dto.Lines != null && dto.Lines.Count > 0)
        {
            bom.Lines.Clear();
            int lineNo = 1;
            foreach (var l in dto.Lines)
            {
                bom.Lines.Add(new InvBomLine
                {
                    BomId = bom.Id,
                    LineNo = lineNo++,
                    ComponentItemId = l.ComponentItemId,
                    UomCode = l.UomCode,
                    UomFactor = l.UomFactor,
                    Quantity = l.Quantity,
                    ScrapPercent = l.ScrapPercent,
                    CostSharePercent = l.CostSharePercent,
                    AllowSubstitute = l.AllowSubstitute,
                    SubstituteItemId = l.SubstituteItemId,
                    Notes = l.Notes
                });
            }
        }

        bom.UpdateUser = username;
        bom.UpdateDate = DateTime.UtcNow;

        await _bomRepository.UpdateAsync(bom, ct);
        return ApiResponse<InvBomDto>.CreateSuccess(InvBomMapper.ToDto(bom), "BOM recipe updated successfully");
    }

    public async Task<ApiResponse<bool>> DeleteBomAsync(long id, CancellationToken ct = default)
    {
        var bom = await _bomRepository.GetByIdAsync(id, ct);
        if (bom == null)
            return ApiResponse<bool>.CreateFailure("BOM recipe not found", null, 404);

        await _bomRepository.DeleteAsync(id, ct);
        return ApiResponse<bool>.CreateSuccess(true, "BOM recipe deactivated successfully");
    }
}
