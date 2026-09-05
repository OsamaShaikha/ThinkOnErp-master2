using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using ThinkOnErp.Application.Common;
using ThinkOnErp.Application.DTOs.Inventory.Types;

namespace ThinkOnErp.Application.Services.Inventory;

public interface ITrxTypeService
{
    // DocType CRUD
    Task<ApiResponse<TrxDocTypeDto>> CreateDocTypeAsync(CreateTrxDocTypeDto dto, string username, CancellationToken ct = default);
    Task<ApiResponse<List<TrxDocTypeDto>>> GetAllDocTypesAsync(CancellationToken ct = default);
    Task<ApiResponse<TrxDocTypeDto>> GetDocTypeByCodeAsync(int code, CancellationToken ct = default);
    Task<ApiResponse<TrxDocTypeDto>> UpdateDocTypeAsync(int code, UpdateTrxDocTypeDto dto, string username, CancellationToken ct = default);
    Task<ApiResponse<bool>> DeleteDocTypeAsync(int code, CancellationToken ct = default);

    // TrxType CRUD
    Task<ApiResponse<TrxTransactionTypeDto>> CreateTrxTypeAsync(CreateTrxTransactionTypeDto dto, string username, CancellationToken ct = default);
    Task<ApiResponse<List<TrxTransactionTypeDto>>> GetAllTrxTypesAsync(CancellationToken ct = default);
    Task<ApiResponse<List<TrxTransactionTypeDto>>> GetTrxTypesByDocTypeAsync(int docTypeCode, CancellationToken ct = default);
    Task<ApiResponse<TrxTransactionTypeDto>> GetTrxTypeByCodeAsync(int code, CancellationToken ct = default);
    Task<ApiResponse<TrxTransactionTypeDto>> UpdateTrxTypeAsync(int code, UpdateTrxTransactionTypeDto dto, string username, CancellationToken ct = default);
    Task<ApiResponse<bool>> DeleteTrxTypeAsync(int code, CancellationToken ct = default);
}
