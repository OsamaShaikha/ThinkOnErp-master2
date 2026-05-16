using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using ThinkOnErp.Domain.Entities;
using ThinkOnErp.Domain.Interfaces;
using ThinkOnErp.Infrastructure.Data;
using ThinkOnErp.Infrastructure.Exceptions;

namespace ThinkOnErp.Infrastructure.Repositories.EfCore;

/// <summary>
/// EF Core repository implementation for SysRole entity using LINQ queries.
/// Implements IRoleRepository interface from the Domain layer.
/// Uses ThinkOnErpDbContext for database operations with automatic change tracking and ID generation.
/// </summary>
public class RoleRepository : IRoleRepository
{
    private readonly ThinkOnErpDbContext _context;
    private readonly ILogger<RoleRepository> _logger;

    /// <summary>
    /// Initializes a new instance of the RoleRepository class.
    /// </summary>
    /// <param name="context">The EF Core database context for entity management.</param>
    /// <param name="logger">Logger for recording operations and errors.</param>
    /// <exception cref="ArgumentNullException">Thrown when context or logger is null.</exception>
    public RoleRepository(
        ThinkOnErpDbContext context,
        ILogger<RoleRepository> logger)
    {
        _context = context ?? throw new ArgumentNullException(nameof(context));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <summary>
    /// Retrieves all active roles from the database using LINQ.
    /// Uses AsNoTracking for read-only query optimization.
    /// Global query filter automatically filters out soft-deleted records (IsActive = false).
    /// </summary>
    /// <returns>A list of all active SysRole entities ordered by RowDesc</returns>
    public async Task<List<SysRole>> GetAllAsync()
    {
        return await RepositoryExceptionHandler.ExecuteWithExceptionHandlingAsync(
            async () =>
            {
                _logger.LogDebug("Retrieving all roles");

                var roles = await _context.Roles
                    .AsNoTracking()
                    .OrderBy(r => r.RowDesc)
                    .ToListAsync();

                _logger.LogDebug("Retrieved {Count} roles", roles.Count);
                return roles;
            },
            "GetAllRoles",
            _logger);
    }

    /// <summary>
    /// Retrieves a specific role by its ID using LINQ.
    /// Uses AsNoTracking for read-only query optimization.
    /// Global query filter automatically filters out soft-deleted records (IsActive = false).
    /// </summary>
    /// <param name="rowId">The unique identifier of the role</param>
    /// <returns>The SysRole entity if found, null otherwise</returns>
    public async Task<SysRole?> GetByIdAsync(long rowId)
    {
        return await RepositoryExceptionHandler.ExecuteWithExceptionHandlingAsync(
            async () =>
            {
                _logger.LogDebug("Retrieving role with ID: {RowId}", rowId);

                var role = await _context.Roles
                    .AsNoTracking()
                    .FirstOrDefaultAsync(r => r.RowId == rowId);

                if (role == null)
                {
                    _logger.LogDebug("Role with ID {RowId} not found", rowId);
                }
                else
                {
                    _logger.LogDebug("Retrieved role: {RowDesc}", role.RowDesc);
                }

                return role;
            },
            "GetRoleById",
            _logger);
    }

    /// <summary>
    /// Creates a new role in the database using EF Core.
    /// EF Core automatically retrieves the generated ID from Oracle sequence SEQ_SYS_ROLE.
    /// </summary>
    /// <param name="role">The role entity to create</param>
    /// <returns>The generated RowId from SEQ_SYS_ROLE sequence</returns>
    public async Task<long> CreateAsync(SysRole role)
    {
        return await RepositoryExceptionHandler.ExecuteWithExceptionHandlingAsync(
            async () =>
            {
                _logger.LogDebug("Creating new role: {RowDesc}", role.RowDesc);

                // Set creation date if not already set
                if (!role.CreationDate.HasValue)
                {
                    role.CreationDate = DateTime.Now;
                }

                // Set IsActive to true by default
                role.IsActive = true;

                // Add the entity to the context
                _context.Roles.Add(role);

                // Save changes - EF Core will automatically retrieve the generated ID from the sequence
                await _context.SaveChangesAsync();

                _logger.LogInformation("Created role with ID: {RowId}, Name: {RowDesc}", 
                    role.RowId, role.RowDesc);

                return role.RowId;
            },
            "CreateRole",
            _logger);
    }

    /// <summary>
    /// Updates an existing role in the database using EF Core.
    /// EF Core automatically tracks changes and generates the appropriate UPDATE statement.
    /// </summary>
    /// <param name="role">The role entity with updated values</param>
    /// <returns>The number of rows affected (1 if successful, 0 if not found)</returns>
    public async Task<long> UpdateAsync(SysRole role)
    {
        return await RepositoryExceptionHandler.ExecuteWithExceptionHandlingAsync(
            async () =>
            {
                _logger.LogDebug("Updating role with ID: {RowId}", role.RowId);

                // Set update date
                role.UpdateDate = DateTime.Now;

                // Update the entity - EF Core will track changes
                _context.Roles.Update(role);

                // Save changes and get the number of affected rows
                var rowsAffected = await _context.SaveChangesAsync();

                if (rowsAffected > 0)
                {
                    _logger.LogInformation("Updated role with ID: {RowId}, Name: {RowDesc}", 
                        role.RowId, role.RowDesc);
                }
                else
                {
                    _logger.LogWarning("No rows affected when updating role with ID: {RowId}", 
                        role.RowId);
                }

                return rowsAffected;
            },
            "UpdateRole",
            _logger);
    }

    /// <summary>
    /// Performs a soft delete on a role by setting IS_ACTIVE to false.
    /// Loads the entity, modifies the IsActive property, and saves changes.
    /// </summary>
    /// <param name="rowId">The unique identifier of the role to delete</param>
    /// <returns>The number of rows affected (1 if successful, 0 if not found)</returns>
    public async Task<long> DeleteAsync(long rowId)
    {
        return await RepositoryExceptionHandler.ExecuteWithExceptionHandlingAsync(
            async () =>
            {
                _logger.LogDebug("Soft deleting role with ID: {RowId}", rowId);

                // Find the role to delete - need to ignore query filter to find it even if already deleted
                var role = await _context.Roles
                    .IgnoreQueryFilters()
                    .FirstOrDefaultAsync(r => r.RowId == rowId);

                if (role == null)
                {
                    _logger.LogWarning("Role with ID {RowId} not found for deletion", rowId);
                    return 0;
                }

                // Perform soft delete by setting IsActive to false
                role.IsActive = false;
                role.UpdateDate = DateTime.Now;

                var rowsAffected = await _context.SaveChangesAsync();

                if (rowsAffected > 0)
                {
                    _logger.LogInformation("Soft deleted role with ID: {RowId}, Name: {RowDesc}", 
                        rowId, role.RowDesc);
                }

                return rowsAffected;
            },
            "DeleteRole",
            _logger);
    }
}
