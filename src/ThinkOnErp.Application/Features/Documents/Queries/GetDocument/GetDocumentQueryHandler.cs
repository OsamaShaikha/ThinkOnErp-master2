using MediatR;
using Microsoft.Extensions.Logging;
using ThinkOnErp.Application.Common;
using ThinkOnErp.Application.DTOs.Documents;
using ThinkOnErp.Domain.Interfaces;

namespace ThinkOnErp.Application.Features.Documents.Queries.GetDocument;

public class GetDocumentQueryHandler : IRequestHandler<GetDocumentQuery, ApiResponse<DocumentDto>>
{
    private readonly IDocumentRepository _documentRepository;
    private readonly ILogger<GetDocumentQueryHandler> _logger;

    public GetDocumentQueryHandler(
        IDocumentRepository documentRepository,
        ILogger<GetDocumentQueryHandler> logger)
    {
        _documentRepository = documentRepository;
        _logger = logger;
    }

    public async Task<ApiResponse<DocumentDto>> Handle(GetDocumentQuery request, CancellationToken cancellationToken)
    {
        _logger.LogInformation("Fetching document by ID {DocumentId}", request.Id);

        try
        {
            var document = await _documentRepository.GetByIdAsync(request.Id);
            if (document == null)
            {
                return ApiResponse<DocumentDto>.CreateFailure($"Document with ID {request.Id} not found.", statusCode: 404);
            }

            var dto = new DocumentDto
            {
                Id = document.Id,
                FileName = document.FileName,
                FileSize = document.FileSize,
                FormattedFileSize = document.GetFormattedFileSize(),
                MimeType = document.MimeType,
                FileExtension = document.FileExtension,
                Description = document.Description,
                Category = document.Category,
                Tags = document.Tags,
                OwnerType = document.OwnerType,
                OwnerId = document.OwnerId,
                CreationUser = document.CreationUser,
                CreationDate = document.CreationDate,
                UpdateUser = document.UpdateUser,
                UpdateDate = document.UpdateDate
            };

            return ApiResponse<DocumentDto>.CreateSuccess(dto, "Document retrieved successfully.");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error fetching document {DocumentId}", request.Id);
            throw;
        }
    }
}
