using ThinkOnErp.Domain.Entities;

namespace ThinkOnErp.Domain.Interfaces;

/// <summary>
/// Repository interface for ticket configuration operations.
/// Provides methods for managing ticket system configuration settings.
/// </summary>
public interface ITicketConfigRepository
{
    /// <summary>
    /// Retrieves all active ticket configuration settings
    /// </summary>
    /// <returns>List of all active configuration settings</returns>
    Task<List<SysTicketConfig>> GetAllAsync();

    /// <summary>
    /// Retrieves a specific configuration by its unique key
    /// </summary>
    /// <param name="configKey">The configuration key to search for</param>
    /// <returns>Configuration setting if found, null otherwise</returns>
    Task<SysTicketConfig?> GetByKeyAsync(string configKey);

    /// <summary>
    /// Retrieves all configurations of a specific type
    /// </summary>
    /// <param name="configType">The configuration type (SLA, FileAttachment, Notification, Workflow, General)</param>
    /// <returns>List of configuration settings of the specified type</returns>
    Task<List<SysTicketConfig>> GetByTypeAsync(string configType);

    /// <summary>
    /// Creates a new ticket configuration setting
    /// </summary>
    /// <param name="config">The configuration to create</param>
    /// <returns>The ID of the created configuration</returns>
    Task<Int64> CreateAsync(SysTicketConfig config);

    /// <summary>
    /// Updates an existing ticket configuration setting
    /// </summary>
    /// <param name="config">The configuration to update</param>
    /// <returns>The ID of the updated configuration</returns>
    Task<Int64> UpdateAsync(SysTicketConfig config);

    /// <summary>
    /// Updates a configuration value by its key
    /// </summary>
    /// <param name="configKey">The configuration key</param>
    /// <param name="configValue">The new value</param>
    /// <param name="updateUser">User performing the update</param>
    /// <returns>True if update was successful</returns>
    Task<bool> UpdateByKeyAsync(string configKey, string configValue, string updateUser);

    /// <summary>
    /// Soft deletes a ticket configuration (sets IS_ACTIVE to false)
    /// </summary>
    /// <param name="rowId">The ID of the configuration to delete</param>
    /// <param name="updateUser">User performing the deletion</param>
    /// <returns>True if deletion was successful</returns>
    Task<bool> DeleteAsync(Int64 rowId, string updateUser);
}
