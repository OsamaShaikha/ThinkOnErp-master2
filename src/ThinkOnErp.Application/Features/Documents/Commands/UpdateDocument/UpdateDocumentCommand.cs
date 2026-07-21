using MediatR;
using ThinkOnErp.Application.Common;
using ThinkOnErp.Application.DTOs.Documents;

namespace ThinkOnErp.Application.Features.Documents.Commands.UpdateDocument;

public class UpdateDocumentCommand : IRequest<ApiResponse<DocumentDto>>
{
    public long Id { get; set; }
    public string? Description { get; set; }
    public int? DocumentType { get; set; }
    public string? Tags { get; set; }
    public string UpdateUser { get; set; } = string.Empty;
}
