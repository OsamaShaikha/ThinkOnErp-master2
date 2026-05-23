using MediatR;
using Microsoft.Extensions.Logging;
using ThinkOnErp.Application.Common;
using ThinkOnErp.Application.DTOs.Documents;
using ThinkOnErp.Application.DTOs.Ticket;
using ThinkOnErp.Domain.Interfaces;

namespace ThinkOnErp.Application.Features.Documents.Queries.GetDocuments;

public class GetDocumentsQueryHandler : IRequestHandler<GetDocumentsQuery, ApiResponse<PagedResult<DocumentDto>>>
{
    private readonly IDocumentRepository _documentRepository;
    private readonly ILogger<GetDocumentsQueryHandler> _logger;

    public GetDocumentsQueryHandler(
        IDocumentRepository documentRepository,
        ILogger<GetDocumentsQueryHandler> logger)
    {
        _documentRepository = documentRepository;
        _logger = logger;
    }

    public async Task<ApiResponse<PagedResult<DocumentDto>>> Handle(GetDocumentsQuery request, CancellationToken cancellationToken)
    {
        _logger.LogInformation("Fetching documents for {OwnerType}/{OwnerId} - Page {Page}, Size {PageSize}",
            request.OwnerType, request.OwnerId, request.Page, request.PageSize);

        try
        {
            if (string.IsNullOrEmpty(request.OwnerType) || !request.OwnerId.HasValue)
            {
                return ApiResponse<PagedResult<DocumentDto>>.CreateFailure(
                    "OwnerType and OwnerId are required.", statusCode: 400);
            }

            var (items, totalCount) = await _documentRepository.GetByOwnerAsync(
                request.OwnerType, request.OwnerId.Value,
                request.Page, request.PageSize,
                request.Category, request.Search);

            var dtos = items.Select(d => new DocumentDto
            {
                Id = d.Id,
                FileName = d.FileName,
                FileSize = d.FileSize,
                FormattedFileSize = d.GetFormattedFileSize(),
                MimeType = d.MimeType,
                FileExtension = d.FileExtension,
                Description = d.Description,
                Category = d.Category,
                Tags = d.Tags,
                OwnerType = d.OwnerType,
                OwnerId = d.OwnerId,
                CreationUser = d.CreationUser,
                CreationDate = d.CreationDate,
                UpdateUser = d.UpdateUser,
                UpdateDate = d.UpdateDate
            }).ToList();

            var result = new PagedResult<DocumentDto>
            {
                Items = dtos,
                TotalCount = totalCount,
                Page = request.Page,
                PageSize = request.PageSize
            };

            return ApiResponse<PagedResult<DocumentDto>>.CreateSuccess(result, "Documents retrieved successfully.");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error fetching documents");
            throw;
        }
    }
}
