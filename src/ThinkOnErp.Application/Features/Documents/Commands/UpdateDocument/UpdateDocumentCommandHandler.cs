using MediatR;
using Microsoft.Extensions.Logging;
using ThinkOnErp.Application.Common;
using ThinkOnErp.Application.DTOs.Documents;
using ThinkOnErp.Domain.Interfaces;

namespace ThinkOnErp.Application.Features.Documents.Commands.UpdateDocument;

public class UpdateDocumentCommandHandler : IRequestHandler<UpdateDocumentCommand, ApiResponse<DocumentDto>>
{
    private readonly IDocumentRepository _documentRepository;
    private readonly ILogger<UpdateDocumentCommandHandler> _logger;

    public UpdateDocumentCommandHandler(
        IDocumentRepository documentRepository,
        ILogger<UpdateDocumentCommandHandler> logger)
    {
        _documentRepository = documentRepository;
        _logger = logger;
    }

    public async Task<ApiResponse<DocumentDto>> Handle(UpdateDocumentCommand request, CancellationToken cancellationToken)
    {
        _logger.LogInformation("Updating document metadata for ID {DocumentId}", request.Id);

        try
        {
            var document = await _documentRepository.GetByIdAsync(request.Id);
            if (document == null)
            {
                return ApiResponse<DocumentDto>.CreateFailure($"Document with ID {request.Id} not found.", statusCode: 404);
            }

            if (request.Description != null)
                document.Description = request.Description;

            if (request.DocumentType.HasValue)
                document.DocumentType = request.DocumentType.Value;

            if (request.Tags != null)
                document.Tags = request.Tags;

            document.UpdateUser = request.UpdateUser;

            await _documentRepository.UpdateAsync(document);

            _logger.LogInformation("Document {DocumentId} metadata updated successfully", request.Id);

            return ApiResponse<DocumentDto>.CreateSuccess(
                new DocumentDto
                {
                    Id = document.Id,
                    FileName = document.FileName,
                    FileSize = document.FileSize,
                    FormattedFileSize = document.GetFormattedFileSize(),
                    MimeType = document.MimeType,
                    FileExtension = document.FileExtension,
                    Description = document.Description,
                    DocumentType = document.DocumentType,
                    Tags = document.Tags,
                    OwnerType = document.OwnerType,
                    OwnerId = document.OwnerId,
                    CreationUser = document.CreationUser,
                    CreationDate = document.CreationDate,
                    UpdateUser = document.UpdateUser,
                    UpdateDate = document.UpdateDate
                },
                "Document updated successfully");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error updating document {DocumentId}", request.Id);
            throw;
        }
    }
}
