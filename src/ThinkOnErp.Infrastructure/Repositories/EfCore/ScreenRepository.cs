using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using ThinkOnErp.Domain.Entities;
using ThinkOnErp.Domain.Interfaces;
using ThinkOnErp.Infrastructure.Data;
using ThinkOnErp.Infrastructure.Exceptions;

namespace ThinkOnErp.Infrastructure.Repositories.EfCore;

/// <summary>
/// EF Core repository implementation for SysScreen entity using LINQ queries.
/// Implements IScreenRepository interface from the Domain layer.
/// Uses ThinkOnErpDbContext for database operations with automatic change tracking.
/// </summary>
public class ScreenRepository : IScreenRepository
{
    private readonly ThinkOnErpDbContext _context;
    private readonly ILogger<ScreenRepository> _logger;

    /// <summary>
    /// Initializes a new instance of the ScreenRepository class.
    /// </summary>
    /// <param name="context">The EF Core database context for entity management.</param>
    /// <param name="logger">Logger for recording operations and errors.</param>
    /// <exception cref="ArgumentNullException">Thrown when context or logger is null.</exception>
    public ScreenRepository(
        ThinkOnErpDbContext context,
        ILogger<ScreenRepository> logger)
    {
        _context = context ?? throw new ArgumentNullException(nameof(context));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <summary>
    /// Retrieves all active screens from the database using LINQ.
    /// Uses AsNoTracking for read-only query optimization.
    /// Eagerly loads System and ParentScreen navigation properties.
    /// Global query filter automatically filters out soft-deleted records (IsActive = false).
    /// </summary>
    /// <returns>A list of all active SysScreen entities ordered by DisplayOrder</returns>
    public async Task<List<SysScreen>> GetAllScreensAsync()
    {
        return await RepositoryExceptionHandler.ExecuteWithExceptionHandlingAsync(
            async () =>
            {
                _logger.LogDebug("Retrieving all screens");

                var screens = await _context.Screens
                    .AsNoTracking()
                    .Include(s => s.System)
                    .Include(s => s.ParentScreen)
                    .Where(s => s.IsActive)
                    .OrderBy(s => s.DisplayOrder)
                    .ToListAsync();

                _logger.LogDebug("Retrieved {Count} screens", screens.Count);
                return screens;
            },
            "GetAllScreens",
            _logger);
    }

    /// <summary>
    /// Retrieves all screens for a specific system using LINQ.
    /// Uses AsNoTracking for read-only query optimization.
    /// Eagerly loads System and ParentScreen navigation properties.
    /// </summary>
    /// <param name="systemId">The system ID to filter screens by</param>
    /// <returns>A list of screens belonging to the specified system</returns>
    public async Task<List<SysScreen>> GetScreensBySystemIdAsync(long systemId)
    {
        return await RepositoryExceptionHandler.ExecuteWithExceptionHandlingAsync(
            async () =>
            {
                _logger.LogDebug("Retrieving screens for system ID: {SystemId}", systemId);

                var screens = await _context.Screens
                    .AsNoTracking()
                    .Include(s => s.System)
                    .Include(s => s.ParentScreen)
                    .Where(s => s.SystemId == systemId && s.IsActive)
                    .OrderBy(s => s.DisplayOrder)
                    .ToListAsync();

                _logger.LogDebug("Retrieved {Count} screens for system ID: {SystemId}", 
                    screens.Count, systemId);
                return screens;
            },
            "GetScreensBySystemId",
            _logger);
    }

    /// <summary>
    /// Retrieves a specific screen by its ID using LINQ.
    /// Uses AsNoTracking for read-only query optimization.
    /// Eagerly loads System and ParentScreen navigation properties.
    /// </summary>
    /// <param name="screenId">The unique identifier of the screen</param>
    /// <returns>The SysScreen entity if found, null otherwise</returns>
    public async Task<SysScreen?> GetScreenByIdAsync(long screenId)
    {
        return await RepositoryExceptionHandler.ExecuteWithExceptionHandlingAsync(
            async () =>
            {
                _logger.LogDebug("Retrieving screen with ID: {ScreenId}", screenId);

                var screen = await _context.Screens
                    .AsNoTracking()
                    .Include(s => s.System)
                    .Include(s => s.ParentScreen)
                    .FirstOrDefaultAsync(s => s.RowId == screenId);

                if (screen == null)
                {
                    _logger.LogDebug("Screen with ID {ScreenId} not found", screenId);
                }
                else
                {
                    _logger.LogDebug("Retrieved screen: {ScreenName}", screen.ScreenName);
                }

                return screen;
            },
            "GetScreenById",
            _logger);
    }

    /// <summary>
    /// Creates a new screen in the database using EF Core.
    /// EF Core automatically retrieves the generated ID from Oracle sequence SEQ_SYS_SCREEN.
    /// </summary>
    /// <param name="screen">The screen entity to create</param>
    /// <returns>The generated RowId from SEQ_SYS_SCREEN sequence</returns>
    public async Task<long> CreateScreenAsync(SysScreen screen)
    {
        return await RepositoryExceptionHandler.ExecuteWithExceptionHandlingAsync(
            async () =>
            {
                _logger.LogDebug("Creating new screen: {ScreenName}", screen.ScreenName);

                // Set creation date if not already set
                if (!screen.CreationDate.HasValue)
                {
                    screen.CreationDate = DateTime.Now;
                }

                // Set IsActive to true by default
                screen.IsActive = true;

                // Add the entity to the context
                _context.Screens.Add(screen);

                // Save changes - EF Core will automatically retrieve the generated ID from the sequence
                await _context.SaveChangesAsync();

                _logger.LogInformation("Created screen with ID: {RowId}, Name: {ScreenName}, Code: {ScreenCode}", 
                    screen.RowId, screen.ScreenName, screen.ScreenCode);

                return screen.RowId;
            },
            "CreateScreen",
            _logger);
    }

    /// <summary>
    /// Updates an existing screen in the database using EF Core.
    /// EF Core automatically tracks changes and generates the appropriate UPDATE statement.
    /// </summary>
    /// <param name="screen">The screen entity with updated values</param>
    public async Task UpdateScreenAsync(SysScreen screen)
    {
        await RepositoryExceptionHandler.ExecuteWithExceptionHandlingAsync(
            async () =>
            {
                _logger.LogDebug("Updating screen with ID: {RowId}", screen.RowId);

                // Set update date
                screen.UpdateDate = DateTime.Now;

                // Update the entity - EF Core will track changes
                _context.Screens.Update(screen);

                // Save changes and get the number of affected rows
                var rowsAffected = await _context.SaveChangesAsync();

                if (rowsAffected > 0)
                {
                    _logger.LogInformation("Updated screen with ID: {RowId}, Name: {ScreenName}", 
                        screen.RowId, screen.ScreenName);
                }
                else
                {
                    _logger.LogWarning("No rows affected when updating screen with ID: {RowId}", 
                        screen.RowId);
                }

                return Task.CompletedTask;
            },
            "UpdateScreen",
            _logger);
    }

    /// <summary>
    /// Performs a soft delete on a screen by setting IS_ACTIVE to false.
    /// Loads the entity, modifies the IsActive property, and saves changes.
    /// </summary>
    /// <param name="screenId">The unique identifier of the screen to delete</param>
    /// <param name="updateUser">Username for audit</param>
    public async Task DeleteScreenAsync(long screenId, string updateUser)
    {
        await RepositoryExceptionHandler.ExecuteWithExceptionHandlingAsync(
            async () =>
            {
                _logger.LogDebug("Soft deleting screen with ID: {ScreenId}", screenId);

                // Find the screen to delete - need to ignore query filter to find it even if already deleted
                var screen = await _context.Screens
                    .IgnoreQueryFilters()
                    .FirstOrDefaultAsync(s => s.RowId == screenId);

                if (screen == null)
                {
                    _logger.LogWarning("Screen with ID {ScreenId} not found for deletion", screenId);
                    return Task.CompletedTask;
                }

                // Perform soft delete by setting IsActive to false
                screen.IsActive = false;
                screen.UpdateUser = updateUser;
                screen.UpdateDate = DateTime.Now;

                var rowsAffected = await _context.SaveChangesAsync();

                if (rowsAffected > 0)
                {
                    _logger.LogInformation("Soft deleted screen with ID: {ScreenId}, Name: {ScreenName}", 
                        screenId, screen.ScreenName);
                }

                return Task.CompletedTask;
            },
            "DeleteScreen",
            _logger);
    }
}
