using MediatR;
using Microsoft.Extensions.Logging;
using ThinkOnErp.Application.Common;
using ThinkOnErp.Application.DTOs.Documents;
using ThinkOnErp.Domain.Entities;
using ThinkOnErp.Domain.Interfaces;

namespace ThinkOnErp.Application.Features.Documents.Commands.UploadDocument;

public class UploadDocumentCommandHandler : IRequestHandler<UploadDocumentCommand, ApiResponse<DocumentUploadResult>>
{
    private readonly IDocumentRepository _documentRepository;
    private readonly IDocumentStorageService _storageService;
    private readonly ILogger<UploadDocumentCommandHandler> _logger;

    public UploadDocumentCommandHandler(
        IDocumentRepository documentRepository,
        IDocumentStorageService storageService,
        ILogger<UploadDocumentCommandHandler> logger)
    {
        _documentRepository = documentRepository;
        _storageService = storageService;
        _logger = logger;
    }

    public async Task<ApiResponse<DocumentUploadResult>> Handle(UploadDocumentCommand request, CancellationToken cancellationToken)
    {
        _logger.LogInformation("Uploading document {FileName} for {OwnerType} {OwnerId}",
            request.FileName, request.OwnerType, request.OwnerId);

        try
        {
            var fileExtension = Path.GetExtension(request.FileName).ToLowerInvariant();

            // Validate extension
            if (!SysDocument.AllowedFileExtensions.Contains(fileExtension))
            {
                return ApiResponse<DocumentUploadResult>.CreateFailure(
                    $"File type '{fileExtension}' is not allowed. Allowed: {string.Join(", ", SysDocument.AllowedFileExtensions)}");
            }

            // Validate size
            if (request.FileSize > SysDocument.MaxFileSizeBytes)
            {
                return ApiResponse<DocumentUploadResult>.CreateFailure(
                    $"File size exceeds maximum allowed size of {SysDocument.MaxFileSizeBytes / (1024 * 1024)} MB");
            }

            // Save file to disk
            var relativePath = Path.Combine(request.OwnerType, request.OwnerId.ToString(), $"{Guid.NewGuid():N}_{request.FileName}");
            var sanitizedPath = relativePath.Replace("..", "");

            var savedPath = await _storageService.SaveFileAsync(request.FileStream, sanitizedPath);

            // Create entity
            var document = new SysDocument
            {
                FileName = request.FileName,
                FileSize = request.FileSize,
                MimeType = request.ContentType,
                FileExtension = fileExtension,
                FilePath = savedPath,
                Description = request.Description,
                Category = request.Category,
                Tags = request.Tags,
                OwnerType = request.OwnerType,
                OwnerId = request.OwnerId,
                CreationUser = request.CreationUser,
                CreationDate = DateTime.UtcNow
            };

            var id = await _documentRepository.CreateAsync(document);

            _logger.LogInformation("Document {FileName} uploaded successfully with ID {DocumentId}", request.FileName, id);

            return ApiResponse<DocumentUploadResult>.CreateSuccess(
                new DocumentUploadResult
                {
                    Id = id,
                    FileName = document.FileName,
                    FileSize = document.FileSize,
                    Message = "Document uploaded successfully"
                },
                "Document uploaded successfully",
                201);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error uploading document {FileName}", request.FileName);
            throw;
        }
    }
}
