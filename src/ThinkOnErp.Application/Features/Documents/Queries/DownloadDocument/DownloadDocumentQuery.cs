using MediatR;

namespace ThinkOnErp.Application.Features.Documents.Queries.DownloadDocument;

public class DownloadDocumentQuery : IRequest<DownloadDocumentResult>
{
    public long Id { get; set; }
}

public class DownloadDocumentResult
{
    public Stream FileStream { get; set; } = null!;
    public string FileName { get; set; } = string.Empty;
    public string MimeType { get; set; } = string.Empty;
    public long FileSize { get; set; }
}
