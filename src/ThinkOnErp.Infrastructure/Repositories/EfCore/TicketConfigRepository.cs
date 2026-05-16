using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using ThinkOnErp.Domain.Entities;
using ThinkOnErp.Domain.Interfaces;
using ThinkOnErp.Infrastructure.Data;
using ThinkOnErp.Infrastructure.Exceptions;

namespace ThinkOnErp.Infrastructure.Repositories.EfCore;

/// <summary>
/// EF Core repository implementation for SysTicketConfig entity using LINQ queries.
/// Implements ITicketConfigRepository interface from the Domain layer.
/// Uses ThinkOnErpDbContext for database operations with automatic change tracking and ID generation.
/// </summary>
public class TicketConfigRepository : ITicketConfigRepository
{
    private readonly ThinkOnErpDbContext _context;
    private readonly ILogger<TicketConfigRepository> _logger;

    /// <summary>
    /// Initializes a new instance of the TicketConfigRepository class.
    /// </summary>
    /// <param name="context">The EF Core database context for entity management.</param>
    /// <param name="logger">Logger for recording operations and errors.</param>
    /// <exception cref="ArgumentNullException">Thrown when context or logger is null.</exception>
    public TicketConfigRepository(
        ThinkOnErpDbContext context,
        ILogger<TicketConfigRepository> logger)
    {
        _context = context ?? throw new ArgumentNullException(nameof(context));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <summary>
    /// Retrieves all active ticket configuration settings.
    /// Uses AsNoTracking for read-only query optimization.
    /// </summary>
    /// <returns>List of all active configuration settings</returns>
    public async Task<List<SysTicketConfig>> GetAllAsync()
    {
        return await RepositoryExceptionHandler.ExecuteWithExceptionHandlingAsync(
            async () =>
            {
                _logger.LogDebug("Retrieving all ticket configurations");

                var configs = await _context.TicketConfigs
                    .AsNoTracking()
                    .OrderBy(c => c.ConfigType)
                    .ThenBy(c => c.ConfigKey)
                    .ToListAsync();

                _logger.LogDebug("Retrieved {Count} ticket configurations", configs.Count);
                return configs;
            },
            "GetAllTicketConfigs",
            _logger);
    }

    /// <summary>
    /// Retrieves a specific configuration by its unique key.
    /// Uses LINQ FirstOrDefaultAsync with Where clause.
    /// </summary>
    /// <param name="configKey">The configuration key to search for</param>
    /// <returns>Configuration setting if found, null otherwise</returns>
    public async Task<SysTicketConfig?> GetByKeyAsync(string configKey)
    {
        return await RepositoryExceptionHandler.ExecuteWithExceptionHandlingAsync(
            async () =>
            {
                _logger.LogDebug("Retrieving ticket configuration with key: {ConfigKey}", configKey);

                var config = await _context.TicketConfigs
                    .AsNoTracking()
                    .FirstOrDefaultAsync(c => c.ConfigKey == configKey);

                if (config == null)
                {
                    _logger.LogDebug("Ticket configuration with key {ConfigKey} not found", configKey);
                }
                else
                {
                    _logger.LogDebug("Retrieved ticket configuration: {ConfigKey} = {ConfigValue}", 
                        config.ConfigKey, config.ConfigValue);
                }

                return config;
            },
            "GetTicketConfigByKey",
            _logger);
    }

    /// <summary>
    /// Retrieves all configurations of a specific type.
    /// Uses LINQ Where clause to filter by ConfigType.
    /// </summary>
    /// <param name="configType">The configuration type (SLA, FileAttachment, Notification, Workflow, General)</param>
    /// <returns>List of configuration settings of the specified type</returns>
    public async Task<List<SysTicketConfig>> GetByTypeAsync(string configType)
    {
        return await RepositoryExceptionHandler.ExecuteWithExceptionHandlingAsync(
            async () =>
            {
                _logger.LogDebug("Retrieving ticket configurations of type: {ConfigType}", configType);

                var configs = await _context.TicketConfigs
                    .AsNoTracking()
                    .Where(c => c.ConfigType == configType)
                    .OrderBy(c => c.ConfigKey)
                    .ToListAsync();

                _logger.LogDebug("Retrieved {Count} ticket configurations of type {ConfigType}", 
                    configs.Count, configType);

                return configs;
            },
            "GetTicketConfigsByType",
            _logger);
    }

    /// <summary>
    /// Creates a new ticket configuration setting using EF Core.
    /// EF Core automatically retrieves the generated ID from Oracle sequence SEQ_SYS_TICKET_CONFIG.
    /// </summary>
    /// <param name="config">The configuration to create</param>
    /// <returns>The ID of the created configuration</returns>
    public async Task<long> CreateAsync(SysTicketConfig config)
    {
        return await RepositoryExceptionHandler.ExecuteWithExceptionHandlingAsync(
            async () =>
            {
                _logger.LogDebug("Creating new ticket configuration: {ConfigKey}", config.ConfigKey);

                // Set creation date if not already set
                if (!config.CreationDate.HasValue)
                {
                    config.CreationDate = DateTime.Now;
                }

                // Set IsActive to true by default
                config.IsActive = true;

                // Add the entity to the context
                _context.TicketConfigs.Add(config);

                // Save changes - EF Core will automatically retrieve the generated ID from the sequence
                await _context.SaveChangesAsync();

                _logger.LogInformation("Created ticket configuration with ID: {RowId}, Key: {ConfigKey}", 
                    config.RowId, config.ConfigKey);

                return config.RowId;
            },
            "CreateTicketConfig",
            _logger);
    }

    /// <summary>
    /// Updates an existing ticket configuration setting using EF Core.
    /// EF Core automatically tracks changes and generates the appropriate UPDATE statement.
    /// </summary>
    /// <param name="config">The configuration to update</param>
    /// <returns>The ID of the updated configuration</returns>
    public async Task<long> UpdateAsync(SysTicketConfig config)
    {
        return await RepositoryExceptionHandler.ExecuteWithExceptionHandlingAsync(
            async () =>
            {
                _logger.LogDebug("Updating ticket configuration with ID: {RowId}", config.RowId);

                // Set update date
                config.UpdateDate = DateTime.Now;

                // Update the entity - EF Core will track changes
                _context.TicketConfigs.Update(config);

                // Save changes and get the number of affected rows
                var rowsAffected = await _context.SaveChangesAsync();

                if (rowsAffected > 0)
                {
                    _logger.LogInformation("Updated ticket configuration with ID: {RowId}, Key: {ConfigKey}", 
                        config.RowId, config.ConfigKey);
                }
                else
                {
                    _logger.LogWarning("No rows affected when updating ticket configuration with ID: {RowId}", 
                        config.RowId);
                }

                return config.RowId;
            },
            "UpdateTicketConfig",
            _logger);
    }

    /// <summary>
    /// Updates a configuration value by its key.
    /// Uses LINQ to load, modify, and save the configuration.
    /// </summary>
    /// <param name="configKey">The configuration key</param>
    /// <param name="configValue">The new value</param>
    /// <param name="updateUser">User performing the update</param>
    /// <returns>True if update was successful</returns>
    public async Task<bool> UpdateByKeyAsync(string configKey, string configValue, string updateUser)
    {
        return await RepositoryExceptionHandler.ExecuteWithExceptionHandlingAsync(
            async () =>
            {
                _logger.LogDebug("Updating ticket configuration value for key: {ConfigKey}", configKey);

                var config = await _context.TicketConfigs
                    .FirstOrDefaultAsync(c => c.ConfigKey == configKey);

                if (config == null)
                {
                    _logger.LogWarning("Ticket configuration with key {ConfigKey} not found for update", configKey);
                    return false;
                }

                config.ConfigValue = configValue;
                config.UpdateUser = updateUser;
                config.UpdateDate = DateTime.Now;

                var rowsAffected = await _context.SaveChangesAsync();

                if (rowsAffected > 0)
                {
                    _logger.LogInformation("Updated ticket configuration: {ConfigKey} = {ConfigValue}", 
                        configKey, configValue);
                    return true;
                }

                return false;
            },
            "UpdateTicketConfigByKey",
            _logger);
    }

    /// <summary>
    /// Soft deletes a ticket configuration (sets IS_ACTIVE to false).
    /// Loads the entity, modifies the IsActive property, and saves changes.
    /// </summary>
    /// <param name="rowId">The ID of the configuration to delete</param>
    /// <param name="updateUser">User performing the deletion</param>
    /// <returns>True if deletion was successful</returns>
    public async Task<bool> DeleteAsync(long rowId, string updateUser)
    {
        return await RepositoryExceptionHandler.ExecuteWithExceptionHandlingAsync(
            async () =>
            {
                _logger.LogDebug("Soft deleting ticket configuration with ID: {RowId}", rowId);

                var config = await _context.TicketConfigs
                    .FirstOrDefaultAsync(c => c.RowId == rowId);

                if (config == null)
                {
                    _logger.LogWarning("Ticket configuration with ID {RowId} not found for deletion", rowId);
                    return false;
                }

                // Perform soft delete by setting IsActive to false
                config.IsActive = false;
                config.UpdateUser = updateUser;
                config.UpdateDate = DateTime.Now;

                var rowsAffected = await _context.SaveChangesAsync();

                if (rowsAffected > 0)
                {
                    _logger.LogInformation("Soft deleted ticket configuration with ID: {RowId}, Key: {ConfigKey}", 
                        rowId, config.ConfigKey);
                    return true;
                }

                return false;
            },
            "DeleteTicketConfig",
            _logger);
    }
}
