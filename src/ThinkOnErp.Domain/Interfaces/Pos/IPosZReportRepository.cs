using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using ThinkOnErp.Domain.Entities.Pos;

namespace ThinkOnErp.Domain.Interfaces.Pos;

public interface IPosZReportRepository
{
    Task<long> GetNextZSequenceNumberAsync(long branchId, CancellationToken ct = default);
    Task<PosZReport?> GetByIdAsync(long id, CancellationToken ct = default);
    Task<(IReadOnlyList<PosZReport> Items, long TotalCount)> GetZReportsPagedAsync(
        long branchId,
        DateTime? fromDate = null,
        DateTime? toDate = null,
        int pageIndex = 1,
        int pageSize = 20,
        CancellationToken ct = default);
    Task AddZReportAsync(PosZReport report, CancellationToken ct = default);
    Task SaveChangesAsync(CancellationToken ct = default);
}

public interface IPosGiftCardRepository
{
    Task<PosGiftCard?> GetByCodeAsync(long branchId, string cardCode, CancellationToken ct = default);
    Task<PosGiftCard?> GetByIdAsync(long id, CancellationToken ct = default);
    Task<(IReadOnlyList<PosGiftCard> Items, long TotalCount)> GetGiftCardsPagedAsync(
        long branchId,
        string? cardCode = null,
        bool? isActive = null,
        int pageIndex = 1,
        int pageSize = 20,
        CancellationToken ct = default);
    Task AddGiftCardAsync(PosGiftCard card, CancellationToken ct = default);
    Task UpdateGiftCardAsync(PosGiftCard card, CancellationToken ct = default);
    Task DeleteGiftCardAsync(PosGiftCard card, CancellationToken ct = default);
    Task AddTransactionAsync(PosGiftCardTransaction transaction, CancellationToken ct = default);
    Task SaveChangesAsync(CancellationToken ct = default);
}

public interface IPosBatchPrepRepository
{
    Task<PosBatchPrep?> GetByIdAsync(long id, CancellationToken ct = default);
    Task<(IReadOnlyList<PosBatchPrep> Items, long TotalCount)> GetBatchPrepsPagedAsync(
        long branchId,
        DateTime? fromDate = null,
        DateTime? toDate = null,
        int pageIndex = 1,
        int pageSize = 20,
        CancellationToken ct = default);
    Task AddBatchPrepAsync(PosBatchPrep batchPrep, CancellationToken ct = default);
    Task SaveChangesAsync(CancellationToken ct = default);
}
