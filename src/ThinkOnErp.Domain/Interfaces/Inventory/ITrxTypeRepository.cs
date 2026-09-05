using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using ThinkOnErp.Domain.Entities.Inventory;

namespace ThinkOnErp.Domain.Interfaces.Inventory;

public interface ITrxTypeRepository
{
    Task<TrxDocType?> GetDocTypeAsync(int code, CancellationToken ct = default);
    Task<List<TrxDocType>> GetAllDocTypesAsync(CancellationToken ct = default);
    Task<TrxDocType> CreateDocTypeAsync(TrxDocType docType, CancellationToken ct = default);
    Task UpdateDocTypeAsync(TrxDocType docType, CancellationToken ct = default);
    Task DeleteDocTypeAsync(TrxDocType docType, CancellationToken ct = default);

    Task<TrxTransactionType?> GetTrxTypeAsync(int code, CancellationToken ct = default);
    Task<List<TrxTransactionType>> GetAllTrxTypesAsync(CancellationToken ct = default);
    Task<List<TrxTransactionType>> GetTrxTypesByDocTypeAsync(int docTypeCode, CancellationToken ct = default);
    Task<TrxTransactionType> CreateTrxTypeAsync(TrxTransactionType trxType, CancellationToken ct = default);
    Task UpdateTrxTypeAsync(TrxTransactionType trxType, CancellationToken ct = default);
    Task DeleteTrxTypeAsync(TrxTransactionType trxType, CancellationToken ct = default);
}
