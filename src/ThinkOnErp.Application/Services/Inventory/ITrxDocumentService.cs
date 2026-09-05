using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using ThinkOnErp.Application.Common;
using ThinkOnErp.Application.DTOs.Inventory.Documents;

namespace ThinkOnErp.Application.Services.Inventory;

public interface ITrxDocumentService
{
    Task<ApiResponse<TrxDocumentDto>> CreateDocumentAsync(CreateTrxDocumentDto dto, string username, CancellationToken ct = default);
    Task<ApiResponse<TrxDocumentDto>> GetDocumentByKeyAsync(long branchId, int docYear, int docType, long id, CancellationToken ct = default);
    Task<ApiResponse<PagedResultDto<TrxDocumentDto>>> GetDocumentsPagedAsync(TrxDocumentFilterDto filter, CancellationToken ct = default);
    Task<ApiResponse<TrxDocumentDto>> UpdateDocumentAsync(long branchId, int docYear, int docType, long id, UpdateTrxDocumentDto dto, string username, CancellationToken ct = default);
    Task<ApiResponse<bool>> DeleteDocumentAsync(long branchId, int docYear, int docType, long id, string username, CancellationToken ct = default);
    Task<ApiResponse<TrxDocumentDto>> PostDocumentAsync(long branchId, int docYear, int docType, long id, string username, CancellationToken ct = default);
    Task<ApiResponse<ProfitabilitySummaryReportDto>> GetProfitabilityReportAsync(long? branchId, int? docYear, DateTime? fromDate, DateTime? toDate, string? customerCode, long? itemId, CancellationToken ct = default);
}
