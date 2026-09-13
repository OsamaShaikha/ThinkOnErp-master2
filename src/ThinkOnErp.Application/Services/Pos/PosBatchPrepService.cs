using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using ThinkOnErp.Application.Common;
using ThinkOnErp.Application.DTOs.Pos;
using ThinkOnErp.Domain.Entities.Pos;
using ThinkOnErp.Domain.Interfaces.Pos;

namespace ThinkOnErp.Application.Services.Pos;

public interface IPosBatchPrepService
{
    Task<ApiResponse<BatchPrepSummaryDto>> ExecuteBatchPrepAsync(CreateBatchPrepDto dto, string username, CancellationToken ct = default);
    Task<ApiResponse<BatchPrepSummaryDto>> GetBatchPrepByIdAsync(long id, CancellationToken ct = default);
    Task<ApiResponse<PagedResultDto<BatchPrepSummaryDto>>> GetBatchPrepsPagedAsync(
        long branchId,
        DateTime? fromDate = null,
        DateTime? toDate = null,
        int pageIndex = 1,
        int pageSize = 20,
        CancellationToken ct = default);
}

public class PosBatchPrepService : IPosBatchPrepService
{
    private readonly IPosBatchPrepRepository _batchPrepRepository;

    public PosBatchPrepService(IPosBatchPrepRepository batchPrepRepository)
    {
        _batchPrepRepository = batchPrepRepository;
    }

    public async Task<ApiResponse<BatchPrepSummaryDto>> ExecuteBatchPrepAsync(CreateBatchPrepDto dto, string username, CancellationToken ct = default)
    {
        if (dto.ActualOutputQty <= 0)
            return ApiResponse<BatchPrepSummaryDto>.CreateFailure("Actual output quantity must be greater than zero", null, 400);

        var today = DateTime.UtcNow.ToString("yyyyMMdd");
        var batchNumber = $"PREP-{dto.BranchId}-{today}-{Guid.NewGuid().ToString().Substring(0, 4).ToUpper()}";

        decimal totalCost = 0m;
        var batch = new PosBatchPrep
        {
            BranchId = dto.BranchId,
            BatchNumber = batchNumber,
            PrepDate = DateTime.UtcNow,
            OutputItemId = dto.OutputItemId,
            PlannedOutputQty = dto.PlannedOutputQty,
            ActualOutputQty = dto.ActualOutputQty,
            OutputUomId = dto.OutputUomId,
            YieldFactor = dto.YieldFactor > 0 ? dto.YieldFactor : 1.0m,
            PrepNotes = dto.PrepNotes,
            IsPostedToInventory = true,
            PostedDate = DateTime.UtcNow,
            CreationUser = username,
            CreationDate = DateTime.UtcNow
        };

        foreach (var line in dto.ConsumedLines)
        {
            var lineCost = line.QuantityUsed * line.UnitCost;
            totalCost += lineCost;

            batch.ConsumedLines.Add(new PosBatchPrepLine
            {
                RawItemId = line.RawItemId,
                QuantityUsed = line.QuantityUsed,
                UomId = line.UomId,
                UnitCost = line.UnitCost,
                LineCost = lineCost,
                ScrapWasteQty = line.ScrapWasteQty
            });
        }

        batch.TotalRawCost = totalCost;
        batch.UnitCostProduced = Math.Round(totalCost / (dto.ActualOutputQty * batch.YieldFactor), 4);

        await _batchPrepRepository.AddBatchPrepAsync(batch, ct);
        await _batchPrepRepository.SaveChangesAsync(ct);

        return ApiResponse<BatchPrepSummaryDto>.CreateSuccess(MapToSummary(batch), "Batch preparation recorded and costed successfully");
    }

    public async Task<ApiResponse<BatchPrepSummaryDto>> GetBatchPrepByIdAsync(long id, CancellationToken ct = default)
    {
        var batch = await _batchPrepRepository.GetByIdAsync(id, ct);
        if (batch == null)
            return ApiResponse<BatchPrepSummaryDto>.CreateFailure("Batch prep not found", null, 404);

        return ApiResponse<BatchPrepSummaryDto>.CreateSuccess(MapToSummary(batch));
    }

    public async Task<ApiResponse<PagedResultDto<BatchPrepSummaryDto>>> GetBatchPrepsPagedAsync(
        long branchId,
        DateTime? fromDate = null,
        DateTime? toDate = null,
        int pageIndex = 1,
        int pageSize = 20,
        CancellationToken ct = default)
    {
        var (items, totalCount) = await _batchPrepRepository.GetBatchPrepsPagedAsync(
            branchId, fromDate, toDate, pageIndex, pageSize, ct);

        var dtos = items.Select(MapToSummary).ToList();
        var paged = new PagedResultDto<BatchPrepSummaryDto>(dtos, totalCount, pageIndex, pageSize);
        return ApiResponse<PagedResultDto<BatchPrepSummaryDto>>.CreateSuccess(paged);
    }

    private static BatchPrepSummaryDto MapToSummary(PosBatchPrep batch)
    {
        return new BatchPrepSummaryDto
        {
            Id = batch.Id,
            BatchNumber = batch.BatchNumber,
            PrepDate = batch.PrepDate,
            OutputItemId = batch.OutputItemId,
            PlannedOutputQty = batch.PlannedOutputQty,
            ActualOutputQty = batch.ActualOutputQty,
            YieldFactor = batch.YieldFactor,
            TotalRawCost = batch.TotalRawCost,
            UnitCostProduced = batch.UnitCostProduced,
            IsPostedToInventory = batch.IsPostedToInventory
        };
    }
}
