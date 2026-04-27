using MediatR;
using Microsoft.Extensions.Logging;
using ThinkOnErp.Domain.Entities;
using ThinkOnErp.Domain.Interfaces;

namespace ThinkOnErp.Application.Features.SavedSearches.Commands.CreateSavedSearch;

/// <summary>
/// Handler for CreateSavedSearchCommand.
/// Creates a new saved search in the database.
/// Requirements: 8.6, 8.11
/// </summary>
public class CreateSavedSearchCommandHandler : IRequestHandler<CreateSavedSearchCommand, Int64>
{
    private readonly ISavedSearchRepository _savedSearchRepository;
    private readonly ILogger<CreateSavedSearchCommandHandler> _logger;

    public CreateSavedSearchCommandHandler(
        ISavedSearchRepository savedSearchRepository,
        ILogger<CreateSavedSearchCommandHandler> logger)
    {
        _savedSearchRepository = savedSearchRepository;
        _logger = logger;
    }

    public async Task<Int64> Handle(CreateSavedSearchCommand request, CancellationToken cancellationToken)
    {
        _logger.LogInformation("Creating saved search '{SearchName}' for user {UserId}", 
            request.SearchName, request.UserId);

        try
        {
            var savedSearch = new SysSavedSearch
            {
                UserId = request.UserId,
                SearchName = request.SearchName,
                SearchDescription = request.SearchDescription,
                SearchCriteria = request.SearchCriteria,
                IsPublic = request.IsPublic,
                IsDefault = request.IsDefault,
                IsActive = true,
                CreationUser = request.CreationUser,
                CreationDate = DateTime.UtcNow
            };

            var newId = await _savedSearchRepository.CreateAsync(savedSearch);

            _logger.LogInformation("Successfully created saved search with ID {SavedSearchId}", newId);

            return newId;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error creating saved search '{SearchName}' for user {UserId}", 
                request.SearchName, request.UserId);
            throw;
        }
    }
}
