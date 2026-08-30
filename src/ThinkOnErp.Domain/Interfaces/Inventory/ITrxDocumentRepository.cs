using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using ThinkOnErp.Domain.Entities.Inventory;

namespace ThinkOnErp.Domain.Interfaces.Inventory;

public interface ITrxDocumentRepository
{
    Task<TrxDocumentHeader?> GetByKeyAsync(long branchId, int docYear, int docType, long id, CancellationToken ct = default);
    Task<TrxDocumentHeader?> GetByDocNoAsync(long branchId, int docYear, int docType, string docNo, CancellationToken ct = default);
    Task<(List<TrxDocumentHeader> Items, int TotalCount)> GetPagedAsync(
        long branchId, int? docYear, int? docType, int? trxType, int? partyTypeCode, long? partyId, int? statusCode,
        int pageNumber, int pageSize, CancellationToken ct = default);
    Task<long> GetNextIdAsync(long branchId, int docYear, int docType, CancellationToken ct = default);
    Task<TrxDocumentHeader> CreateAsync(TrxDocumentHeader doc, CancellationToken ct = default);
    Task UpdateAsync(TrxDocumentHeader doc, CancellationToken ct = default);
    Task DeleteAsync(TrxDocumentHeader doc, CancellationToken ct = default);
}
