using ThinkOnErp.Domain.Entities;

namespace ThinkOnErp.Domain.Interfaces;

/// <summary>
/// Repository interface for screen/page management operations.
/// </summary>
public interface IScreenRepository
{
    /// <summary>
    /// Gets all active screens.
    /// </summary>
    /// <returns>List of all active screens</returns>
    Task<List<SysScreen>> GetAllScreensAsync();

    /// <summary>
    /// Gets all screens for a specific system.
    /// </summary>
    /// <param name="systemId">System ID</param>
    /// <returns>List of screens belonging to the system</returns>
    Task<List<SysScreen>> GetScreensBySystemIdAsync(long systemId);

    /// <summary>
    /// Gets a screen by ID.
    /// </summary>
    /// <param name="screenId">Screen ID</param>
    /// <returns>Screen entity or null if not found</returns>
    Task<SysScreen?> GetScreenByIdAsync(long screenId);

    /// <summary>
    /// Creates a new screen.
    /// </summary>
    /// <param name="screen">Screen entity to create</param>
    /// <returns>The newly created screen ID</returns>
    Task<long> CreateScreenAsync(SysScreen screen);

    /// <summary>
    /// Updates an existing screen.
    /// </summary>
    /// <param name="screen">Screen entity with updated values</param>
    Task UpdateScreenAsync(SysScreen screen);

    /// <summary>
    /// Soft deletes a screen (sets IS_ACTIVE to false).
    /// </summary>
    /// <param name="screenId">Screen ID to delete</param>
    /// <param name="updateUser">Username for audit</param>
    Task DeleteScreenAsync(long screenId, string updateUser);
}
