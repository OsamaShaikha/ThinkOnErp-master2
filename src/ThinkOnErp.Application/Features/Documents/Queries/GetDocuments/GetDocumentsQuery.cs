using MediatR;
using ThinkOnErp.Application.Common;
using ThinkOnErp.Application.DTOs.Documents;
using ThinkOnErp.Application.DTOs.Ticket;

namespace ThinkOnErp.Application.Features.Documents.Queries.GetDocuments;

public class GetDocumentsQuery : IRequest<ApiResponse<PagedResult<DocumentDto>>>
{
    public string? OwnerType { get; set; }
    public long? OwnerId { get; set; }
    public string? Category { get; set; }
    public string? Search { get; set; }
    public int Page { get; set; } = 1;
    public int PageSize { get; set; } = 20;
}
