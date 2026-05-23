using MediatR;
using Microsoft.Extensions.Logging;
using ThinkOnErp.Domain.Exceptions;
using ThinkOnErp.Domain.Interfaces;

namespace ThinkOnErp.Application.Features.Documents.Queries.DownloadDocument;

public class DownloadDocumentQueryHandler : IRequestHandler<DownloadDocumentQuery, DownloadDocumentResult>
{
    private readonly IDocumentRepository _documentRepository;
    private readonly IDocumentStorageService _storageService;
    private readonly ILogger<DownloadDocumentQueryHandler> _logger;

    public DownloadDocumentQueryHandler(
        IDocumentRepository documentRepository,
        IDocumentStorageService storageService,
        ILogger<DownloadDocumentQueryHandler> logger)
    {
        _documentRepository = documentRepository;
        _storageService = storageService;
        _logger = logger;
    }

    public async Task<DownloadDocumentResult> Handle(DownloadDocumentQuery request, CancellationToken cancellationToken)
    {
        _logger.LogInformation("Processing download request for document {DocumentId}", request.Id);

        var document = await _documentRepository.GetByIdAsync(request.Id);
        if (document == null)
        {
            throw new ArgumentException($"Document with ID {request.Id} not found.");
        }

        var fileStream = await _storageService.GetFileAsync(document.FilePath);

        _logger.LogInformation("Document {DocumentId} ({FileName}) downloaded successfully", request.Id, document.FileName);

        return new DownloadDocumentResult
        {
            FileStream = fileStream,
            FileName = document.FileName,
            MimeType = document.MimeType,
            FileSize = document.FileSize
        };
    }
}
