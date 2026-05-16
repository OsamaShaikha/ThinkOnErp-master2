using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using ThinkOnErp.Domain.Entities;
using ThinkOnErp.Infrastructure.Data;
using ThinkOnErp.Infrastructure.Exceptions;

namespace ThinkOnErp.Infrastructure.Repositories.EfCore;

/// <summary>
/// EF Core repository implementation for SysCompanySystem entity using LINQ queries.
/// Handles company-system access control with composite key (CompanyId, SystemId).
/// Uses ThinkOnErpDbContext for database operations with automatic change tracking.
/// </summary>
public class CompanySystemRepository
{
    private readonly ThinkOnErpDbContext _context;
    private readonly ILogger<CompanySystemRepository> _logger;

    /// <summary>
    /// Initializes a new instance of the CompanySystemRepository class.
    /// </summary>
    /// <param name="context">The EF Core database context for entity management.</param>
    /// <param name="logger">Logger for recording operations and errors.</param>
    /// <exception cref="ArgumentNullException">Thrown when context or logger is null.</exception>
    public CompanySystemRepository(
        ThinkOnErpDbContext context,
        ILogger<CompanySystemRepository> logger)
    {
        _context = context ?? throw new ArgumentNullException(nameof(context));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <summary>
    /// Retrieves all system assignments for a specific company using LINQ.
    /// Uses AsNoTracking for read-only query optimization.
    /// </summary>
    /// <param name="companyId">The company ID to get system assignments for</param>
    /// <returns>A list of company system assignments</returns>
    public async Task<List<SysCompanySystem>> GetByCompanyIdAsync(long companyId)
    {
        return await RepositoryExceptionHandler.ExecuteWithExceptionHandlingAsync(
            async () =>
            {
                _logger.LogDebug("Retrieving system assignments for company ID: {CompanyId}", companyId);

                var companySystems = await _context.CompanySystems
                    .AsNoTracking()
                    .Where(cs => cs.CompanyId == companyId)
                    .OrderBy(cs => cs.SystemId)
                    .ToListAsync();

                _logger.LogDebug("Retrieved {Count} system assignments for company ID: {CompanyId}", 
                    companySystems.Count, companyId);
                return companySystems;
            },
            "GetCompanySystemsByCompanyId",
            _logger);
    }

    /// <summary>
    /// Retrieves all company assignments for a specific system using LINQ.
    /// Uses AsNoTracking for read-only query optimization.
    /// </summary>
    /// <param name="systemId">The system ID to get company assignments for</param>
    /// <returns>A list of company system assignments</returns>
    public async Task<List<SysCompanySystem>> GetBySystemIdAsync(long systemId)
    {
        return await RepositoryExceptionHandler.ExecuteWithExceptionHandlingAsync(
            async () =>
            {
                _logger.LogDebug("Retrieving company assignments for system ID: {SystemId}", systemId);

                var companySystems = await _context.CompanySystems
                    .AsNoTracking()
                    .Where(cs => cs.SystemId == systemId)
                    .OrderBy(cs => cs.CompanyId)
                    .ToListAsync();

                _logger.LogDebug("Retrieved {Count} company assignments for system ID: {SystemId}", 
                    companySystems.Count, systemId);
                return companySystems;
            },
            "GetCompanySystemsBySystemId",
            _logger);
    }

    /// <summary>
    /// Retrieves a specific company system assignment by composite key using LINQ.
    /// Uses AsNoTracking for read-only query optimization.
    /// </summary>
    /// <param name="companyId">The company ID</param>
    /// <param name="systemId">The system ID</param>
    /// <returns>The company system assignment if found, null otherwise</returns>
    public async Task<SysCompanySystem?> GetByIdAsync(long companyId, long systemId)
    {
        return await RepositoryExceptionHandler.ExecuteWithExceptionHandlingAsync(
            async () =>
            {
                _logger.LogDebug("Retrieving company system for CompanyId: {CompanyId}, SystemId: {SystemId}", 
                    companyId, systemId);

                var companySystem = await _context.CompanySystems
                    .AsNoTracking()
                    .FirstOrDefaultAsync(cs => cs.CompanyId == companyId && cs.SystemId == systemId);

                if (companySystem == null)
                {
                    _logger.LogDebug("Company system not found for CompanyId: {CompanyId}, SystemId: {SystemId}", 
                        companyId, systemId);
                }

                return companySystem;
            },
            "GetCompanySystemById",
            _logger);
    }

    /// <summary>
    /// Checks if a system is allowed for a specific company using LINQ.
    /// Uses AsNoTracking for read-only query optimization.
    /// </summary>
    /// <param name="companyId">The company ID</param>
    /// <param name="systemId">The system ID</param>
    /// <returns>True if the system is allowed, false otherwise</returns>
    public async Task<bool> IsSystemAllowedAsync(long companyId, long systemId)
    {
        return await RepositoryExceptionHandler.ExecuteWithExceptionHandlingAsync(
            async () =>
            {
                _logger.LogDebug("Checking if system {SystemId} is allowed for company {CompanyId}", 
                    systemId, companyId);

                var companySystem = await _context.CompanySystems
                    .AsNoTracking()
                    .FirstOrDefaultAsync(cs => cs.CompanyId == companyId && cs.SystemId == systemId);

                var isAllowed = companySystem?.IsAllowed ?? false;

                _logger.LogDebug("System {SystemId} is {Status} for company {CompanyId}", 
                    systemId, isAllowed ? "allowed" : "not allowed", companyId);

                return isAllowed;
            },
            "CheckSystemAllowed",
            _logger);
    }

    /// <summary>
    /// Creates a new company system assignment in the database using EF Core.
    /// EF Core automatically retrieves the generated ID from Oracle sequence.
    /// </summary>
    /// <param name="companySystem">The company system assignment entity to create</param>
    /// <returns>The generated RowId from sequence</returns>
    public async Task<long> CreateAsync(SysCompanySystem companySystem)
    {
        return await RepositoryExceptionHandler.ExecuteWithExceptionHandlingAsync(
            async () =>
            {
                _logger.LogDebug("Creating company system assignment for CompanyId: {CompanyId}, SystemId: {SystemId}", 
                    companySystem.CompanyId, companySystem.SystemId);

                // Set creation date if not already set
                if (!companySystem.CreationDate.HasValue)
                {
                    companySystem.CreationDate = DateTime.Now;
                }

                // Set granted date if not already set and system is allowed
                if (companySystem.IsAllowed && !companySystem.GrantedDate.HasValue)
                {
                    companySystem.GrantedDate = DateTime.Now;
                }

                // Add the entity to the context
                _context.CompanySystems.Add(companySystem);

                // Save changes - EF Core will automatically retrieve the generated ID from the sequence
                await _context.SaveChangesAsync();

                _logger.LogInformation(
                    "Created company system assignment with ID: {RowId}, CompanyId: {CompanyId}, SystemId: {SystemId}, IsAllowed: {IsAllowed}", 
                    companySystem.RowId, companySystem.CompanyId, companySystem.SystemId, companySystem.IsAllowed);

                return companySystem.RowId;
            },
            "CreateCompanySystem",
            _logger);
    }

    /// <summary>
    /// Updates an existing company system assignment in the database using EF Core.
    /// EF Core automatically tracks changes and generates the appropriate UPDATE statement.
    /// </summary>
    /// <param name="companySystem">The company system assignment entity with updated values</param>
    /// <returns>The number of rows affected (1 if successful, 0 if not found)</returns>
    public async Task<int> UpdateAsync(SysCompanySystem companySystem)
    {
        return await RepositoryExceptionHandler.ExecuteWithExceptionHandlingAsync(
            async () =>
            {
                _logger.LogDebug("Updating company system assignment for CompanyId: {CompanyId}, SystemId: {SystemId}", 
                    companySystem.CompanyId, companySystem.SystemId);

                // Set update date
                companySystem.UpdateDate = DateTime.Now;

                // Update the entity - EF Core will track changes
                _context.CompanySystems.Update(companySystem);

                // Save changes and get the number of affected rows
                var rowsAffected = await _context.SaveChangesAsync();

                if (rowsAffected > 0)
                {
                    _logger.LogInformation(
                        "Updated company system assignment with ID: {RowId}, CompanyId: {CompanyId}, SystemId: {SystemId}", 
                        companySystem.RowId, companySystem.CompanyId, companySystem.SystemId);
                }
                else
                {
                    _logger.LogWarning(
                        "No rows affected when updating company system assignment for CompanyId: {CompanyId}, SystemId: {SystemId}", 
                        companySystem.CompanyId, companySystem.SystemId);
                }

                return rowsAffected;
            },
            "UpdateCompanySystem",
            _logger);
    }

    /// <summary>
    /// Deletes a company system assignment by composite key.
    /// Loads the entity and removes it from the context.
    /// </summary>
    /// <param name="companyId">The company ID</param>
    /// <param name="systemId">The system ID</param>
    /// <returns>The number of rows affected (1 if successful, 0 if not found)</returns>
    public async Task<int> DeleteAsync(long companyId, long systemId)
    {
        return await RepositoryExceptionHandler.ExecuteWithExceptionHandlingAsync(
            async () =>
            {
                _logger.LogDebug("Deleting company system assignment for CompanyId: {CompanyId}, SystemId: {SystemId}", 
                    companyId, systemId);

                // Find the company system to delete
                var companySystem = await _context.CompanySystems
                    .FirstOrDefaultAsync(cs => cs.CompanyId == companyId && cs.SystemId == systemId);

                if (companySystem == null)
                {
                    _logger.LogWarning(
                        "Company system assignment not found for deletion - CompanyId: {CompanyId}, SystemId: {SystemId}", 
                        companyId, systemId);
                    return 0;
                }

                // Remove the entity
                _context.CompanySystems.Remove(companySystem);

                var rowsAffected = await _context.SaveChangesAsync();

                if (rowsAffected > 0)
                {
                    _logger.LogInformation(
                        "Deleted company system assignment with ID: {RowId}, CompanyId: {CompanyId}, SystemId: {SystemId}", 
                        companySystem.RowId, companyId, systemId);
                }

                return rowsAffected;
            },
            "DeleteCompanySystem",
            _logger);
    }

    /// <summary>
    /// Sets or updates a company system assignment (upsert operation).
    /// If the assignment exists, updates it; otherwise creates a new one.
    /// </summary>
    /// <param name="companyId">The company ID</param>
    /// <param name="systemId">The system ID</param>
    /// <param name="isAllowed">True to allow, false to block</param>
    /// <param name="grantedBy">Super Admin ID who is granting/revoking</param>
    /// <param name="notes">Optional notes about the assignment</param>
    /// <param name="creationUser">Username for audit</param>
    /// <returns>The company system assignment ID (existing or newly created)</returns>
    public async Task<long> SetSystemAccessAsync(
        long companyId, 
        long systemId, 
        bool isAllowed,
        long? grantedBy,
        string? notes,
        string creationUser)
    {
        return await RepositoryExceptionHandler.ExecuteWithExceptionHandlingAsync(
            async () =>
            {
                _logger.LogDebug("Setting system access for CompanyId: {CompanyId}, SystemId: {SystemId}, IsAllowed: {IsAllowed}", 
                    companyId, systemId, isAllowed);

                // Check if assignment already exists
                var existing = await _context.CompanySystems
                    .FirstOrDefaultAsync(cs => cs.CompanyId == companyId && cs.SystemId == systemId);

                if (existing != null)
                {
                    // Update existing assignment
                    existing.IsAllowed = isAllowed;
                    existing.GrantedBy = grantedBy;
                    existing.Notes = notes;
                    existing.UpdateUser = creationUser;
                    existing.UpdateDate = DateTime.Now;

                    // Update granted/revoked dates
                    if (isAllowed)
                    {
                        existing.GrantedDate = DateTime.Now;
                        existing.RevokedDate = null;
                    }
                    else
                    {
                        existing.RevokedDate = DateTime.Now;
                    }

                    await _context.SaveChangesAsync();

                    _logger.LogInformation(
                        "Updated existing company system assignment with ID: {RowId}", existing.RowId);

                    return existing.RowId;
                }
                else
                {
                    // Create new assignment
                    var newCompanySystem = new SysCompanySystem
                    {
                        CompanyId = companyId,
                        SystemId = systemId,
                        IsAllowed = isAllowed,
                        GrantedBy = grantedBy,
                        GrantedDate = isAllowed ? DateTime.Now : null,
                        RevokedDate = isAllowed ? null : DateTime.Now,
                        Notes = notes,
                        CreationUser = creationUser,
                        CreationDate = DateTime.Now
                    };

                    _context.CompanySystems.Add(newCompanySystem);
                    await _context.SaveChangesAsync();

                    _logger.LogInformation(
                        "Created new company system assignment with ID: {RowId}", newCompanySystem.RowId);

                    return newCompanySystem.RowId;
                }
            },
            "SetCompanySystemAccess",
            _logger);
    }

    /// <summary>
    /// Retrieves all allowed systems for a company using LINQ.
    /// Uses AsNoTracking for read-only query optimization.
    /// </summary>
    /// <param name="companyId">The company ID</param>
    /// <returns>A list of allowed system IDs</returns>
    public async Task<List<long>> GetAllowedSystemIdsAsync(long companyId)
    {
        return await RepositoryExceptionHandler.ExecuteWithExceptionHandlingAsync(
            async () =>
            {
                _logger.LogDebug("Retrieving allowed system IDs for company ID: {CompanyId}", companyId);

                var systemIds = await _context.CompanySystems
                    .AsNoTracking()
                    .Where(cs => cs.CompanyId == companyId && cs.IsAllowed)
                    .Select(cs => cs.SystemId)
                    .ToListAsync();

                _logger.LogDebug("Retrieved {Count} allowed system IDs for company ID: {CompanyId}", 
                    systemIds.Count, companyId);
                return systemIds;
            },
            "GetAllowedSystemIds",
            _logger);
    }
}
