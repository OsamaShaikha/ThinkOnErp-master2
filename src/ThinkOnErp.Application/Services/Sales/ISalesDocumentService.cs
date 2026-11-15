using System.Threading;
using System.Threading.Tasks;
using ThinkOnErp.Application.Common;
using ThinkOnErp.Application.DTOs.Inventory.Documents;
using ThinkOnErp.Application.DTOs.Sales;

namespace ThinkOnErp.Application.Services.Sales;

public interface ISalesDocumentService
{
    // Sales Invoices (DocType 100)
    Task<ApiResponse<TrxDocumentDto>> CreateInvoiceAsync(CreateSalesInvoiceDto dto, string username, CancellationToken ct = default);
    Task<ApiResponse<TrxDocumentDto>> GetInvoiceByKeyAsync(long branchId, int docYear, long id, CancellationToken ct = default);
    Task<ApiResponse<PagedResultDto<TrxDocumentDto>>> GetInvoicesPagedAsync(TrxDocumentFilterDto filter, CancellationToken ct = default);
    Task<ApiResponse<TrxDocumentDto>> UpdateInvoiceAsync(long branchId, int docYear, long id, UpdateSalesInvoiceDto dto, string username, CancellationToken ct = default);
    Task<ApiResponse<TrxDocumentDto>> PostInvoiceAsync(long branchId, int docYear, long id, string username, CancellationToken ct = default);
    Task<ApiResponse<TrxDocumentDto>> CancelInvoiceAsync(long branchId, int docYear, long id, string username, CancellationToken ct = default);

    // Sales Returns (DocType 150)
    Task<ApiResponse<TrxDocumentDto>> CreateReturnAsync(CreateSalesReturnDto dto, string username, CancellationToken ct = default);
    Task<ApiResponse<TrxDocumentDto>> GetReturnByKeyAsync(long branchId, int docYear, long id, CancellationToken ct = default);
    Task<ApiResponse<PagedResultDto<TrxDocumentDto>>> GetReturnsPagedAsync(TrxDocumentFilterDto filter, CancellationToken ct = default);
    Task<ApiResponse<TrxDocumentDto>> UpdateReturnAsync(long branchId, int docYear, long id, UpdateSalesReturnDto dto, string username, CancellationToken ct = default);
    Task<ApiResponse<TrxDocumentDto>> PostReturnAsync(long branchId, int docYear, long id, string username, CancellationToken ct = default);
    Task<ApiResponse<TrxDocumentDto>> CancelReturnAsync(long branchId, int docYear, long id, string username, CancellationToken ct = default);

    // Sales Quotations (DocType 400)
    Task<ApiResponse<TrxDocumentDto>> CreateQuotationAsync(CreateSalesQuotationDto dto, string username, CancellationToken ct = default);
    Task<ApiResponse<TrxDocumentDto>> GetQuotationByKeyAsync(long branchId, int docYear, long id, CancellationToken ct = default);
    Task<ApiResponse<PagedResultDto<TrxDocumentDto>>> GetQuotationsPagedAsync(TrxDocumentFilterDto filter, CancellationToken ct = default);
    Task<ApiResponse<TrxDocumentDto>> UpdateQuotationAsync(long branchId, int docYear, long id, UpdateSalesQuotationDto dto, string username, CancellationToken ct = default);
    Task<ApiResponse<TrxDocumentDto>> PostQuotationAsync(long branchId, int docYear, long id, string username, CancellationToken ct = default);
    Task<ApiResponse<TrxDocumentDto>> CancelQuotationAsync(long branchId, int docYear, long id, string username, CancellationToken ct = default);
}
