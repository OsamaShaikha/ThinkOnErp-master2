using MediatR;
using ThinkOnErp.Application.Common;
using ThinkOnErp.Application.DTOs.Documents;

namespace ThinkOnErp.Application.Features.Documents.Queries.GetDocument;

public class GetDocumentQuery : IRequest<ApiResponse<DocumentDto>>
{
    public long Id { get; set; }
}
