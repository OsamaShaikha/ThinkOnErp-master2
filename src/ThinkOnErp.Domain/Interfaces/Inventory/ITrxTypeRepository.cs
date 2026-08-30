using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using ThinkOnErp.Domain.Entities.Inventory;

namespace ThinkOnErp.Domain.Interfaces.Inventory;

public interface ITrxTypeRepository
{
    Task<TrxDocType?> GetDocTypeAsync(int code, CancellationToken ct = default);
    Task<List<TrxDocType>> GetAllDocTypesAsync(CancellationToken ct = default);
    Task<TrxTransactionType?> GetTrxTypeAsync(int code, CancellationToken ct = default);
    Task<List<TrxTransactionType>> GetTrxTypesByDocTypeAsync(int docTypeCode, CancellationToken ct = default);
}
