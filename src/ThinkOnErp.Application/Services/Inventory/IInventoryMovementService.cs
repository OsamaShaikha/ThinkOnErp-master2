using System.Threading;
using System.Threading.Tasks;
using ThinkOnErp.Application.Common;
using ThinkOnErp.Application.DTOs.Inventory.Documents;

namespace ThinkOnErp.Application.Services.Inventory;

public interface IInventoryMovementService
{
    // Stock Vouchers (DocType 300)
    Task<ApiResponse<TrxDocumentDto>> CreateVoucherAsync(CreateStockVoucherDto dto, string username, CancellationToken ct = default);
    Task<ApiResponse<TrxDocumentDto>> GetVoucherByKeyAsync(long branchId, int docYear, long id, CancellationToken ct = default);
    Task<ApiResponse<PagedResultDto<TrxDocumentDto>>> GetVouchersPagedAsync(TrxDocumentFilterDto filter, CancellationToken ct = default);
    Task<ApiResponse<TrxDocumentDto>> UpdateVoucherAsync(long branchId, int docYear, long id, UpdateStockVoucherDto dto, string username, CancellationToken ct = default);
    Task<ApiResponse<TrxDocumentDto>> PostVoucherAsync(long branchId, int docYear, long id, string username, CancellationToken ct = default);
    Task<ApiResponse<TrxDocumentDto>> CancelVoucherAsync(long branchId, int docYear, long id, string username, CancellationToken ct = default);

    // Stock Transfers (DocType 350)
    Task<ApiResponse<TrxDocumentDto>> CreateTransferAsync(CreateStockTransferDto dto, string username, CancellationToken ct = default);
    Task<ApiResponse<TrxDocumentDto>> GetTransferByKeyAsync(long branchId, int docYear, long id, CancellationToken ct = default);
    Task<ApiResponse<PagedResultDto<TrxDocumentDto>>> GetTransfersPagedAsync(TrxDocumentFilterDto filter, CancellationToken ct = default);
    Task<ApiResponse<TrxDocumentDto>> UpdateTransferAsync(long branchId, int docYear, long id, UpdateStockTransferDto dto, string username, CancellationToken ct = default);
    Task<ApiResponse<TrxDocumentDto>> PostTransferAsync(long branchId, int docYear, long id, string username, CancellationToken ct = default);
    Task<ApiResponse<TrxDocumentDto>> CancelTransferAsync(long branchId, int docYear, long id, string username, CancellationToken ct = default);
}
