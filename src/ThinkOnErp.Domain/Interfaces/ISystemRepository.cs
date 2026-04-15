using ThinkOnErp.Domain.Entities;

namespace ThinkOnErp.Domain.Interfaces;

/// <summary>
/// Repository interface for system/module management operations.
/// </summary>
public interface ISystemRepository
{
    /// <summary>
    /// Gets all active systems.
    /// </summary>
    /// <returns>List of all active systems</returns>
    Task<List<SysSystem>> GetAllSystemsAsync();

    /// <summary>
    /// Gets a system by ID.
    /// </summary>
    /// <param name="systemId">System ID</param>
    /// <returns>System entity or null if not found</returns>
    Task<SysSystem?> GetSystemByIdAsync(long systemId);

    /// <summary>
    /// Creates a new system.
    /// </summary>
    /// <param name="system">System entity to create</param>
    /// <returns>The newly created system ID</returns>
    Task<long> CreateSystemAsync(SysSystem system);

    /// <summary>
    /// Updates an existing system.
    /// </summary>
    /// <param name="system">System entity with updated values</param>
    Task UpdateSystemAsync(SysSystem system);

    /// <summary>
    /// Soft deletes a system (sets IS_ACTIVE to false).
    /// </summary>
    /// <param name="systemId">System ID to delete</param>
    /// <param name="updateUser">Username for audit</param>
    Task DeleteSystemAsync(long systemId, string updateUser);
}
