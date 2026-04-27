using MediatR;
using Microsoft.Extensions.Logging;
using ThinkOnErp.Application.DTOs.SavedSearch;
using ThinkOnErp.Domain.Interfaces;

namespace ThinkOnErp.Application.Features.SavedSearches.Queries.GetSavedSearches;

/// <summary>
/// Handler for GetSavedSearchesQuery.
/// Retrieves all saved searches accessible to a user (private + public).
/// Requirements: 8.6, 8.11
/// </summary>
public class GetSavedSearchesQueryHandler : IRequestHandler<GetSavedSearchesQuery, List<SavedSearchDto>>
{
    private readonly ISavedSearchRepository _savedSearchRepository;
    private readonly ILogger<GetSavedSearchesQueryHandler> _logger;

    public GetSavedSearchesQueryHandler(
        ISavedSearchRepository savedSearchRepository,
        ILogger<GetSavedSearchesQueryHandler> logger)
    {
        _savedSearchRepository = savedSearchRepository;
        _logger = logger;
    }

    public async Task<List<SavedSearchDto>> Handle(GetSavedSearchesQuery request, CancellationToken cancellationToken)
    {
        _logger.LogInformation("Retrieving saved searches for user {UserId}", request.UserId);

        try
        {
            var savedSearches = await _savedSearchRepository.GetByUserIdAsync(request.UserId);

            var dtos = savedSearches.Select(s => new SavedSearchDto
            {
                SavedSearchId = s.RowId,
                UserId = s.UserId,
                UserName = s.User?.RowDescE,
                SearchName = s.SearchName,
                SearchDescription = s.SearchDescription,
                SearchCriteria = s.SearchCriteria,
                IsPublic = s.IsPublic,
                IsDefault = s.IsDefault,
                UsageCount = s.UsageCount,
                LastUsedDate = s.LastUsedDate,
                IsActive = s.IsActive,
                CreationUser = s.CreationUser,
                CreationDate = s.CreationDate,
                UpdateUser = s.UpdateUser,
                UpdateDate = s.UpdateDate
            }).ToList();

            _logger.LogInformation("Retrieved {Count} saved searches for user {UserId}", 
                dtos.Count, request.UserId);

            return dtos;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving saved searches for user {UserId}", request.UserId);
            throw;
        }
    }
}
