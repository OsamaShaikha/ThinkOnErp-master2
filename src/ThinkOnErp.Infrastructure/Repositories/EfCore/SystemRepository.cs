using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using ThinkOnErp.Domain.Entities;
using ThinkOnErp.Domain.Interfaces;
using ThinkOnErp.Infrastructure.Data;
using ThinkOnErp.Infrastructure.Exceptions;

namespace ThinkOnErp.Infrastructure.Repositories.EfCore;

/// <summary>
/// EF Core repository implementation for SysSystem entity using LINQ queries.
/// Implements ISystemRepository interface from the Domain layer.
/// Uses ThinkOnErpDbContext for database operations with automatic change tracking.
/// </summary>
public class SystemRepository : ISystemRepository
{
    private readonly ThinkOnErpDbContext _context;
    private readonly ILogger<SystemRepository> _logger;

    /// <summary>
    /// Initializes a new instance of the SystemRepository class.
    /// </summary>
    /// <param name="context">The EF Core database context for entity management.</param>
    /// <param name="logger">Logger for recording operations and errors.</param>
    /// <exception cref="ArgumentNullException">Thrown when context or logger is null.</exception>
    public SystemRepository(
        ThinkOnErpDbContext context,
        ILogger<SystemRepository> logger)
    {
        _context = context ?? throw new ArgumentNullException(nameof(context));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <summary>
    /// Retrieves all active systems from the database using LINQ.
    /// Uses AsNoTracking for read-only query optimization.
    /// Global query filter automatically filters out soft-deleted records (IsActive = false).
    /// </summary>
    /// <returns>A list of all active SysSystem entities ordered by DisplayOrder</returns>
    public async Task<List<SysSystem>> GetAllSystemsAsync()
    {
        return await RepositoryExceptionHandler.ExecuteWithExceptionHandlingAsync(
            async () =>
            {
                _logger.LogDebug("Retrieving all systems");

                var systems = await _context.Systems
                    .AsNoTracking()
                    .Where(s => s.IsActive)
                    .OrderBy(s => s.DisplayOrder)
                    .ToListAsync();

                _logger.LogDebug("Retrieved {Count} systems", systems.Count);
                return systems;
            },
            "GetAllSystems",
            _logger);
    }

    /// <summary>
    /// Retrieves a specific system by its ID using LINQ.
    /// Uses AsNoTracking for read-only query optimization.
    /// </summary>
    /// <param name="systemId">The unique identifier of the system</param>
    /// <returns>The SysSystem entity if found, null otherwise</returns>
    public async Task<SysSystem?> GetSystemByIdAsync(long systemId)
    {
        return await RepositoryExceptionHandler.ExecuteWithExceptionHandlingAsync(
            async () =>
            {
                _logger.LogDebug("Retrieving system with ID: {SystemId}", systemId);

                var system = await _context.Systems
                    .AsNoTracking()
                    .FirstOrDefaultAsync(s => s.RowId == systemId);

                if (system == null)
                {
                    _logger.LogDebug("System with ID {SystemId} not found", systemId);
                }
                else
                {
                    _logger.LogDebug("Retrieved system: {SystemName}", system.SystemName);
                }

                return system;
            },
            "GetSystemById",
            _logger);
    }

    /// <summary>
    /// Creates a new system in the database using EF Core.
    /// EF Core automatically retrieves the generated ID from Oracle sequence SEQ_SYS_SYSTEM.
    /// </summary>
    /// <param name="system">The system entity to create</param>
    /// <returns>The generated RowId from SEQ_SYS_SYSTEM sequence</returns>
    public async Task<long> CreateSystemAsync(SysSystem system)
    {
        return await RepositoryExceptionHandler.ExecuteWithExceptionHandlingAsync(
            async () =>
            {
                _logger.LogDebug("Creating new system: {SystemName}", system.SystemName);

                // Set creation date if not already set
                if (!system.CreationDate.HasValue)
                {
                    system.CreationDate = DateTime.Now;
                }

                // Set IsActive to true by default
                system.IsActive = true;

                // Add the entity to the context
                _context.Systems.Add(system);

                // Save changes - EF Core will automatically retrieve the generated ID from the sequence
                await _context.SaveChangesAsync();

                _logger.LogInformation("Created system with ID: {RowId}, Name: {SystemName}, Code: {SystemCode}", 
                    system.RowId, system.SystemName, system.SystemCode);

                return system.RowId;
            },
            "CreateSystem",
            _logger);
    }

    /// <summary>
    /// Updates an existing system in the database using EF Core.
    /// EF Core automatically tracks changes and generates the appropriate UPDATE statement.
    /// </summary>
    /// <param name="system">The system entity with updated values</param>
    public async Task UpdateSystemAsync(SysSystem system)
    {
        await RepositoryExceptionHandler.ExecuteWithExceptionHandlingAsync(
            async () =>
            {
                _logger.LogDebug("Updating system with ID: {RowId}", system.RowId);

                // Set update date
                system.UpdateDate = DateTime.Now;

                // Update the entity - EF Core will track changes
                _context.Systems.Update(system);

                // Save changes and get the number of affected rows
                var rowsAffected = await _context.SaveChangesAsync();

                if (rowsAffected > 0)
                {
                    _logger.LogInformation("Updated system with ID: {RowId}, Name: {SystemName}", 
                        system.RowId, system.SystemName);
                }
                else
                {
                    _logger.LogWarning("No rows affected when updating system with ID: {RowId}", 
                        system.RowId);
                }

                return Task.CompletedTask;
            },
            "UpdateSystem",
            _logger);
    }

    /// <summary>
    /// Performs a soft delete on a system by setting IS_ACTIVE to false.
    /// Loads the entity, modifies the IsActive property, and saves changes.
    /// </summary>
    /// <param name="systemId">The unique identifier of the system to delete</param>
    /// <param name="updateUser">Username for audit</param>
    public async Task DeleteSystemAsync(long systemId, string updateUser)
    {
        await RepositoryExceptionHandler.ExecuteWithExceptionHandlingAsync(
            async () =>
            {
                _logger.LogDebug("Soft deleting system with ID: {SystemId}", systemId);

                // Find the system to delete - need to ignore query filter to find it even if already deleted
                var system = await _context.Systems
                    .IgnoreQueryFilters()
                    .FirstOrDefaultAsync(s => s.RowId == systemId);

                if (system == null)
                {
                    _logger.LogWarning("System with ID {SystemId} not found for deletion", systemId);
                    return Task.CompletedTask;
                }

                // Perform soft delete by setting IsActive to false
                system.IsActive = false;
                system.UpdateUser = updateUser;
                system.UpdateDate = DateTime.Now;

                var rowsAffected = await _context.SaveChangesAsync();

                if (rowsAffected > 0)
                {
                    _logger.LogInformation("Soft deleted system with ID: {SystemId}, Name: {SystemName}", 
                        systemId, system.SystemName);
                }

                return Task.CompletedTask;
            },
            "DeleteSystem",
            _logger);
    }
}
