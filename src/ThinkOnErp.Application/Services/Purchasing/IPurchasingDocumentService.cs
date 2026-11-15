using System.Threading;
using System.Threading.Tasks;
using ThinkOnErp.Application.Common;
using ThinkOnErp.Application.DTOs.Inventory.Documents;
using ThinkOnErp.Application.DTOs.Purchasing;

namespace ThinkOnErp.Application.Services.Purchasing;

public interface IPurchasingDocumentService
{
    // Purchasing Bills (DocType 200)
    Task<ApiResponse<TrxDocumentDto>> CreateBillAsync(CreatePurchasingBillDto dto, string username, CancellationToken ct = default);
    Task<ApiResponse<TrxDocumentDto>> GetBillByKeyAsync(long branchId, int docYear, long id, CancellationToken ct = default);
    Task<ApiResponse<PagedResultDto<TrxDocumentDto>>> GetBillsPagedAsync(TrxDocumentFilterDto filter, CancellationToken ct = default);
    Task<ApiResponse<TrxDocumentDto>> UpdateBillAsync(long branchId, int docYear, long id, UpdatePurchasingBillDto dto, string username, CancellationToken ct = default);
    Task<ApiResponse<TrxDocumentDto>> PostBillAsync(long branchId, int docYear, long id, string username, CancellationToken ct = default);
    Task<ApiResponse<TrxDocumentDto>> CancelBillAsync(long branchId, int docYear, long id, string username, CancellationToken ct = default);

    // Purchasing Returns (DocType 250)
    Task<ApiResponse<TrxDocumentDto>> CreateReturnAsync(CreatePurchasingReturnDto dto, string username, CancellationToken ct = default);
    Task<ApiResponse<TrxDocumentDto>> GetReturnByKeyAsync(long branchId, int docYear, long id, CancellationToken ct = default);
    Task<ApiResponse<PagedResultDto<TrxDocumentDto>>> GetReturnsPagedAsync(TrxDocumentFilterDto filter, CancellationToken ct = default);
    Task<ApiResponse<TrxDocumentDto>> UpdateReturnAsync(long branchId, int docYear, long id, UpdatePurchasingReturnDto dto, string username, CancellationToken ct = default);
    Task<ApiResponse<TrxDocumentDto>> PostReturnAsync(long branchId, int docYear, long id, string username, CancellationToken ct = default);
    Task<ApiResponse<TrxDocumentDto>> CancelReturnAsync(long branchId, int docYear, long id, string username, CancellationToken ct = default);

    // Purchase Orders (DocType 450)
    Task<ApiResponse<TrxDocumentDto>> CreateOrderAsync(CreatePurchaseOrderDto dto, string username, CancellationToken ct = default);
    Task<ApiResponse<TrxDocumentDto>> GetOrderByKeyAsync(long branchId, int docYear, long id, CancellationToken ct = default);
    Task<ApiResponse<PagedResultDto<TrxDocumentDto>>> GetOrdersPagedAsync(TrxDocumentFilterDto filter, CancellationToken ct = default);
    Task<ApiResponse<TrxDocumentDto>> UpdateOrderAsync(long branchId, int docYear, long id, UpdatePurchaseOrderDto dto, string username, CancellationToken ct = default);
    Task<ApiResponse<TrxDocumentDto>> PostOrderAsync(long branchId, int docYear, long id, string username, CancellationToken ct = default);
    Task<ApiResponse<TrxDocumentDto>> CancelOrderAsync(long branchId, int docYear, long id, string username, CancellationToken ct = default);
}
