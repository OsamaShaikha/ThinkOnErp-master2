using MediatR;
using ThinkOnErp.Application.Common;
using ThinkOnErp.Application.DTOs.Documents;

namespace ThinkOnErp.Application.Features.Documents.Commands.UploadDocument;

public class UploadDocumentCommand : IRequest<ApiResponse<DocumentUploadResult>>
{
    public Stream FileStream { get; set; } = Stream.Null;
    public string FileName { get; set; } = string.Empty;
    public long FileSize { get; set; }
    public string ContentType { get; set; } = "application/octet-stream";
    public string? Description { get; set; }
    public int DocumentType { get; set; }
    public string? Tags { get; set; }
    public int OwnerType { get; set; }
    public long OwnerId { get; set; }
    public string CreationUser { get; set; } = string.Empty;
}
