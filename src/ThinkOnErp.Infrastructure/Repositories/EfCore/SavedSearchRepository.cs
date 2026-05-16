using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using ThinkOnErp.Domain.Entities;
using ThinkOnErp.Domain.Interfaces;
using ThinkOnErp.Infrastructure.Data;
using ThinkOnErp.Infrastructure.Exceptions;

namespace ThinkOnErp.Infrastructure.Repositories.EfCore;

/// <summary>
/// EF Core repository implementation for SysSavedSearch entity using LINQ queries.
/// Implements ISavedSearchRepository interface from the Domain layer.
/// Uses ThinkOnErpDbContext for database operations with CRUD support for saved search management.
/// Requirements: 8.6, 8.11, 19.9
/// </summary>
public class SavedSearchRepository : ISavedSearchRepository
{
    private readonly ThinkOnErpDbContext _context;
    private readonly ILogger<SavedSearchRepository> _logger;

    /// <summary>
    /// Initializes a new instance of the SavedSearchRepository class.
    /// </summary>
    /// <param name="context">The EF Core database context for entity management.</param>
    /// <param name="logger">Logger for recording operations and errors.</param>
    /// <exception cref="ArgumentNullException">Thrown when context or logger is null.</exception>
    public SavedSearchRepository(
        ThinkOnErpDbContext context,
        ILogger<SavedSearchRepository> logger)
    {
        _context = context ?? throw new ArgumentNullException(nameof(context));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <summary>
    /// Creates a new saved search.
    /// Uses EF Core Add() and SaveChangesAsync() for insertion.
    /// </summary>
    /// <param name="savedSearch">The saved search entity to create</param>
    /// <returns>The generated RowId from SEQ_SYS_SAVED_SEARCH sequence</returns>
    public async Task<long> CreateAsync(SysSavedSearch savedSearch)
    {
        return await RepositoryExceptionHandler.ExecuteWithExceptionHandlingAsync(
            async () =>
            {
                _logger.LogDebug("Creating new saved search: {SearchName} for user {UserId}", 
                    savedSearch.SearchName, savedSearch.UserId);

                // Set creation date if not already set
                if (!savedSearch.CreationDate.HasValue)
                {
                    savedSearch.CreationDate = DateTime.Now;
                }

                // Initialize usage count if not set
                if (savedSearch.UsageCount == 0)
                {
                    savedSearch.UsageCount = 0;
                }

                // Add the entity to the context
                _context.SavedSearches.Add(savedSearch);

                // Save changes - EF Core will automatically retrieve the generated ID from the sequence
                await _context.SaveChangesAsync();

                _logger.LogInformation("Created saved search with ID: {RowId}, Name: {SearchName}", 
                    savedSearch.RowId, savedSearch.SearchName);

                return savedSearch.RowId;
            },
            "CreateSavedSearch",
            _logger);
    }

    /// <summary>
    /// Updates an existing saved search.
    /// Uses EF Core Update() and SaveChangesAsync() for modification.
    /// </summary>
    /// <param name="savedSearch">The saved search entity with updated values</param>
    /// <returns>The number of rows affected</returns>
    public async Task<long> UpdateAsync(SysSavedSearch savedSearch)
    {
        return await RepositoryExceptionHandler.ExecuteWithExceptionHandlingAsync(
            async () =>
            {
                _logger.LogDebug("Updating saved search with ID: {RowId}", savedSearch.RowId);

                // Set update date
                savedSearch.UpdateDate = DateTime.Now;

                // Update the entity - EF Core will track changes
                _context.SavedSearches.Update(savedSearch);

                // Save changes and get the number of affected rows
                var rowsAffected = await _context.SaveChangesAsync();

                if (rowsAffected > 0)
                {
                    _logger.LogInformation("Updated saved search with ID: {RowId}, Name: {SearchName}", 
                        savedSearch.RowId, savedSearch.SearchName);
                }
                else
                {
                    _logger.LogWarning("No rows affected when updating saved search with ID: {RowId}", 
                        savedSearch.RowId);
                }

                return rowsAffected;
            },
            "UpdateSavedSearch",
            _logger);
    }

    /// <summary>
    /// Retrieves all saved searches for a specific user (private + public).
    /// Uses LINQ Where() clause to filter by user ID or public searches.
    /// Includes eager loading of User navigation property.
    /// </summary>
    /// <param name="userId">The user ID to retrieve searches for</param>
    /// <returns>List of saved searches accessible to the user</returns>
    public async Task<List<SysSavedSearch>> GetByUserIdAsync(long userId)
    {
        return await RepositoryExceptionHandler.ExecuteWithExceptionHandlingAsync(
            async () =>
            {
                _logger.LogDebug("Retrieving saved searches for user ID: {UserId}", userId);

                // Get searches that are either owned by the user or are public
                var savedSearches = await _context.SavedSearches
                    .AsNoTracking()
                    .Include(s => s.User)
                    .Where(s => s.UserId == userId || s.IsPublic)
                    .OrderBy(s => s.SearchName)
                    .ToListAsync();

                _logger.LogDebug("Retrieved {Count} saved searches for user ID: {UserId}", 
                    savedSearches.Count, userId);

                return savedSearches;
            },
            "GetSavedSearchesByUserId",
            _logger);
    }

    /// <summary>
    /// Retrieves a specific saved search by ID.
    /// Uses LINQ FirstOrDefaultAsync() with eager loading of User navigation property.
    /// </summary>
    /// <param name="rowId">The unique identifier of the saved search</param>
    /// <returns>The SysSavedSearch entity if found, null otherwise</returns>
    public async Task<SysSavedSearch?> GetByIdAsync(long rowId)
    {
        return await RepositoryExceptionHandler.ExecuteWithExceptionHandlingAsync(
            async () =>
            {
                _logger.LogDebug("Retrieving saved search with ID: {RowId}", rowId);

                var savedSearch = await _context.SavedSearches
                    .AsNoTracking()
                    .Include(s => s.User)
                    .FirstOrDefaultAsync(s => s.RowId == rowId);

                if (savedSearch == null)
                {
                    _logger.LogDebug("Saved search with ID {RowId} not found", rowId);
                }
                else
                {
                    _logger.LogDebug("Retrieved saved search: {SearchName}", savedSearch.SearchName);
                }

                return savedSearch;
            },
            "GetSavedSearchById",
            _logger);
    }

    /// <summary>
    /// Performs a soft delete on a saved search by setting IS_ACTIVE to false.
    /// Loads the entity, modifies the IsActive property, and saves changes.
    /// </summary>
    /// <param name="rowId">The unique identifier of the saved search to delete</param>
    /// <param name="userName">The username of the user performing the deletion</param>
    /// <returns>The number of rows affected</returns>
    public async Task<long> DeleteAsync(long rowId, string userName)
    {
        return await RepositoryExceptionHandler.ExecuteWithExceptionHandlingAsync(
            async () =>
            {
                _logger.LogDebug("Deleting saved search with ID: {RowId} by user: {UserName}", rowId, userName);

                // Find the saved search to delete
                var savedSearch = await _context.SavedSearches
                    .FirstOrDefaultAsync(s => s.RowId == rowId);

                if (savedSearch == null)
                {
                    _logger.LogWarning("Saved search with ID {RowId} not found for deletion", rowId);
                    return 0;
                }

                // Perform soft delete by setting IsActive to false
                savedSearch.IsActive = false;
                savedSearch.UpdateUser = userName;
                savedSearch.UpdateDate = DateTime.Now;

                // Save changes
                var rowsAffected = await _context.SaveChangesAsync();

                if (rowsAffected > 0)
                {
                    _logger.LogInformation("Soft deleted saved search with ID: {RowId}, Name: {SearchName}", 
                        rowId, savedSearch.SearchName);
                }

                return rowsAffected;
            },
            "DeleteSavedSearch",
            _logger);
    }

    /// <summary>
    /// Increments the usage count and updates last used date for a saved search.
    /// Loads the entity, updates the usage tracking properties, and saves changes.
    /// </summary>
    /// <param name="rowId">The unique identifier of the saved search</param>
    /// <returns>Task representing the async operation</returns>
    public async Task IncrementUsageAsync(long rowId)
    {
        await RepositoryExceptionHandler.ExecuteWithExceptionHandlingAsync(
            async () =>
            {
                _logger.LogDebug("Incrementing usage count for saved search ID: {RowId}", rowId);

                // Find the saved search
                var savedSearch = await _context.SavedSearches
                    .FirstOrDefaultAsync(s => s.RowId == rowId);

                if (savedSearch == null)
                {
                    _logger.LogWarning("Saved search with ID {RowId} not found for usage increment", rowId);
                    return;
                }

                // Increment usage count and update last used date
                savedSearch.UsageCount++;
                savedSearch.LastUsedDate = DateTime.Now;

                // Save changes
                var rowsAffected = await _context.SaveChangesAsync();

                if (rowsAffected > 0)
                {
                    _logger.LogDebug("Incremented usage count for saved search ID: {RowId} to {UsageCount}", 
                        rowId, savedSearch.UsageCount);
                }
            },
            "IncrementSavedSearchUsage",
            _logger);
    }
}
