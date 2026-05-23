using MediatR;
using Microsoft.Extensions.Logging;
using ThinkOnErp.Application.Common;
using ThinkOnErp.Domain.Interfaces;

namespace ThinkOnErp.Application.Features.Documents.Commands.DeleteDocument;

public class SoftDeleteDocumentCommandHandler : IRequestHandler<SoftDeleteDocumentCommand, ApiResponse<bool>>
{
    private readonly IDocumentRepository _documentRepository;
    private readonly IDocumentStorageService _storageService;
    private readonly ILogger<SoftDeleteDocumentCommandHandler> _logger;

    public SoftDeleteDocumentCommandHandler(
        IDocumentRepository documentRepository,
        IDocumentStorageService storageService,
        ILogger<SoftDeleteDocumentCommandHandler> logger)
    {
        _documentRepository = documentRepository;
        _storageService = storageService;
        _logger = logger;
    }

    public async Task<ApiResponse<bool>> Handle(SoftDeleteDocumentCommand request, CancellationToken cancellationToken)
    {
        _logger.LogInformation("Soft-deleting {Count} document(s)", request.Ids.Length);

        try
        {
            var affected = await _documentRepository.BulkSoftDeleteAsync(request.Ids, request.UpdateUser);

            if (affected == 0)
            {
                return ApiResponse<bool>.CreateFailure("No documents were deleted. Verify the IDs.", statusCode: 404);
            }

            _logger.LogInformation("Successfully soft-deleted {Count} document(s)", affected);

            return ApiResponse<bool>.CreateSuccess(true,
                $"{affected} document(s) deleted successfully.");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error soft-deleting documents");
            throw;
        }
    }
}
