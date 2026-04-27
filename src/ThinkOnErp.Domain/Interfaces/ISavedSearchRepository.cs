using ThinkOnErp.Domain.Entities;

namespace ThinkOnErp.Domain.Interfaces;

/// <summary>
/// Repository interface for SysSavedSearch entity data access operations.
/// Defines the contract for saved search management in the Domain layer.
/// Requirements: 8.6, 8.11, 19.9
/// </summary>
public interface ISavedSearchRepository
{
    /// <summary>
    /// Creates a new saved search.
    /// Calls SP_SYS_SAVED_SEARCH_INSERT stored procedure.
    /// </summary>
    /// <param name="savedSearch">The saved search entity to create</param>
    /// <returns>The generated RowId from SEQ_SYS_SAVED_SEARCH sequence</returns>
    Task<Int64> CreateAsync(SysSavedSearch savedSearch);

    /// <summary>
    /// Updates an existing saved search.
    /// Calls SP_SYS_SAVED_SEARCH_UPDATE stored procedure.
    /// </summary>
    /// <param name="savedSearch">The saved search entity with updated values</param>
    /// <returns>The number of rows affected</returns>
    Task<Int64> UpdateAsync(SysSavedSearch savedSearch);

    /// <summary>
    /// Retrieves all saved searches for a specific user (private + public).
    /// Calls SP_SYS_SAVED_SEARCH_SELECT_BY_USER stored procedure.
    /// </summary>
    /// <param name="userId">The user ID to retrieve searches for</param>
    /// <returns>List of saved searches accessible to the user</returns>
    Task<List<SysSavedSearch>> GetByUserIdAsync(Int64 userId);

    /// <summary>
    /// Retrieves a specific saved search by ID.
    /// Calls SP_SYS_SAVED_SEARCH_SELECT_BY_ID stored procedure.
    /// </summary>
    /// <param name="rowId">The unique identifier of the saved search</param>
    /// <returns>The SysSavedSearch entity if found, null otherwise</returns>
    Task<SysSavedSearch?> GetByIdAsync(Int64 rowId);

    /// <summary>
    /// Performs a soft delete on a saved search by setting IS_ACTIVE to false.
    /// Calls SP_SYS_SAVED_SEARCH_DELETE stored procedure.
    /// </summary>
    /// <param name="rowId">The unique identifier of the saved search to delete</param>
    /// <param name="userName">The username of the user performing the deletion</param>
    /// <returns>The number of rows affected</returns>
    Task<Int64> DeleteAsync(Int64 rowId, string userName);

    /// <summary>
    /// Increments the usage count and updates last used date for a saved search.
    /// Calls SP_SYS_SAVED_SEARCH_INCREMENT_USAGE stored procedure.
    /// </summary>
    /// <param name="rowId">The unique identifier of the saved search</param>
    /// <returns>Task representing the async operation</returns>
    Task IncrementUsageAsync(Int64 rowId);
}
