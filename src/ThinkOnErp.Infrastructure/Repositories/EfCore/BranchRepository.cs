using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using ThinkOnErp.Domain.Entities;
using ThinkOnErp.Domain.Interfaces;
using ThinkOnErp.Infrastructure.Data;
using ThinkOnErp.Infrastructure.Exceptions;

namespace ThinkOnErp.Infrastructure.Repositories.EfCore;

/// <summary>
/// EF Core repository implementation for SysBranch entity using LINQ queries.
/// Implements IBranchRepository interface from the Domain layer.
/// Uses ThinkOnErpDbContext for database operations with automatic change tracking and ID generation.
/// </summary>
public class BranchRepository : IBranchRepository
{
    private readonly ThinkOnErpDbContext _context;
    private readonly ILogger<BranchRepository> _logger;

    /// <summary>
    /// Initializes a new instance of the BranchRepository class.
    /// </summary>
    /// <param name="context">The EF Core database context for entity management.</param>
    /// <param name="logger">Logger for recording operations and errors.</param>
    /// <exception cref="ArgumentNullException">Thrown when context or logger is null.</exception>
    public BranchRepository(
        ThinkOnErpDbContext context,
        ILogger<BranchRepository> logger)
    {
        _context = context ?? throw new ArgumentNullException(nameof(context));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <summary>
    /// Retrieves all active branches from the database using LINQ.
    /// Uses AsNoTracking for read-only query optimization.
    /// </summary>
    /// <returns>A list of all active SysBranch entities ordered by RowDesc</returns>
    public async Task<List<SysBranch>> GetAllAsync()
    {
        return await RepositoryExceptionHandler.ExecuteWithExceptionHandlingAsync(
            async () =>
            {
                _logger.LogDebug("Retrieving all branches");

                var branches = await _context.Branches
                    .AsNoTracking()
                    .Include(b => b.BaseCurrency)
                    .OrderBy(b => b.RowDesc)
                    .ToListAsync();

                _logger.LogDebug("Retrieved {Count} branches", branches.Count);
                return branches;
            },
            "GetAllBranches",
            _logger);
    }

    /// <summary>
    /// Retrieves a specific branch by its ID using LINQ.
    /// Uses AsNoTracking for read-only query optimization.
    /// </summary>
    /// <param name="rowId">The unique identifier of the branch</param>
    /// <returns>The SysBranch entity if found, null otherwise</returns>
    public async Task<SysBranch?> GetByIdAsync(long rowId)
    {
        return await RepositoryExceptionHandler.ExecuteWithExceptionHandlingAsync(
            async () =>
            {
                _logger.LogDebug("Retrieving branch with ID: {RowId}", rowId);

                var branch = await _context.Branches
                    .AsNoTracking()
                    .Include(b => b.BaseCurrency)
                    .FirstOrDefaultAsync(b => b.RowId == rowId);

                if (branch == null)
                {
                    _logger.LogDebug("Branch with ID {RowId} not found", rowId);
                }
                else
                {
                    _logger.LogDebug("Retrieved branch: {RowDesc}", branch.RowDesc);
                }

                return branch;
            },
            "GetBranchById",
            _logger);
    }

    /// <summary>
    /// Retrieves all active branches for a specific company using LINQ.
    /// Uses AsNoTracking for read-only query optimization.
    /// </summary>
    /// <param name="companyId">The unique identifier of the company</param>
    /// <returns>A list of SysBranch entities belonging to the specified company</returns>
    public async Task<List<SysBranch>> GetByCompanyIdAsync(long companyId)
    {
        return await RepositoryExceptionHandler.ExecuteWithExceptionHandlingAsync(
            async () =>
            {
                _logger.LogDebug("Retrieving branches for company ID: {CompanyId}", companyId);

                var branches = await _context.Branches
                    .AsNoTracking()
                    .Include(b => b.BaseCurrency)
                    .Where(b => b.ParRowId == companyId)
                    .OrderBy(b => b.RowDesc)
                    .ToListAsync();

                _logger.LogDebug("Retrieved {Count} branches for company ID: {CompanyId}", 
                    branches.Count, companyId);
                return branches;
            },
            "GetBranchesByCompanyId",
            _logger);
    }

    /// <summary>
    /// Creates a new branch in the database using EF Core.
    /// EF Core automatically retrieves the generated ID from Oracle sequence SEQ_SYS_BRANCH.
    /// </summary>
    /// <param name="branch">The branch entity to create</param>
    /// <returns>The generated RowId from SEQ_SYS_BRANCH sequence</returns>
    public async Task<long> CreateAsync(SysBranch branch)
    {
        return await RepositoryExceptionHandler.ExecuteWithExceptionHandlingAsync(
            async () =>
            {
                _logger.LogDebug("Creating new branch: {RowDesc}", branch.RowDesc);

                // Set creation date if not already set
                if (!branch.CreationDate.HasValue)
                {
                    branch.CreationDate = DateTime.Now;
                }

                // Ensure IsActive is set to true for new branches
                branch.IsActive = true;

                // Add the entity to the context
                _context.Branches.Add(branch);

                // Save changes - EF Core will automatically retrieve the generated ID from the sequence
                await _context.SaveChangesAsync();

                _logger.LogInformation("Created branch with ID: {RowId}, Name: {RowDesc}", 
                    branch.RowId, branch.RowDesc);

                return branch.RowId;
            },
            "CreateBranch",
            _logger);
    }

    /// <summary>
    /// Updates an existing branch in the database using EF Core.
    /// EF Core automatically tracks changes and generates the appropriate UPDATE statement.
    /// </summary>
    /// <param name="branch">The branch entity with updated values</param>
    /// <returns>The number of rows affected (1 if successful, 0 if not found)</returns>
    public async Task<long> UpdateAsync(SysBranch branch)
    {
        return await RepositoryExceptionHandler.ExecuteWithExceptionHandlingAsync(
            async () =>
            {
                _logger.LogDebug("Updating branch with ID: {RowId}", branch.RowId);

                // Set update date
                branch.UpdateDate = DateTime.Now;

                // Update the entity - EF Core will track changes
                _context.Branches.Update(branch);

                // Save changes and get the number of affected rows
                var rowsAffected = await _context.SaveChangesAsync();

                if (rowsAffected > 0)
                {
                    _logger.LogInformation("Updated branch with ID: {RowId}, Name: {RowDesc}", 
                        branch.RowId, branch.RowDesc);
                }
                else
                {
                    _logger.LogWarning("No rows affected when updating branch with ID: {RowId}", 
                        branch.RowId);
                }

                return rowsAffected;
            },
            "UpdateBranch",
            _logger);
    }

    /// <summary>
    /// Performs a soft delete on a branch by setting IS_ACTIVE to false.
    /// Loads the entity, modifies the IsActive property, and saves changes.
    /// </summary>
    /// <param name="rowId">The unique identifier of the branch to delete</param>
    /// <returns>The number of rows affected (1 if successful, 0 if not found)</returns>
    public async Task<long> DeleteAsync(long rowId)
    {
        return await RepositoryExceptionHandler.ExecuteWithExceptionHandlingAsync(
            async () =>
            {
                _logger.LogDebug("Soft deleting branch with ID: {RowId}", rowId);

                // Find the branch to delete - need to use IgnoreQueryFilters to find it even if already soft-deleted
                var branch = await _context.Branches
                    .IgnoreQueryFilters()
                    .FirstOrDefaultAsync(b => b.RowId == rowId);

                if (branch == null)
                {
                    _logger.LogWarning("Branch with ID {RowId} not found for deletion", rowId);
                    return 0;
                }

                // Perform soft delete by setting IsActive to false
                branch.IsActive = false;
                branch.UpdateDate = DateTime.Now;

                var rowsAffected = await _context.SaveChangesAsync();

                if (rowsAffected > 0)
                {
                    _logger.LogInformation("Soft deleted branch with ID: {RowId}, Name: {RowDesc}", 
                        rowId, branch.RowDesc);
                }

                return rowsAffected;
            },
            "DeleteBranch",
            _logger);
    }

    /// <summary>
    /// Updates the branch logo using EF Core.
    /// Loads the entity, updates the BranchLogo property, and saves changes.
    /// </summary>
    /// <param name="rowId">The unique identifier of the branch</param>
    /// <param name="logo">The logo image as byte array</param>
    /// <param name="userName">The username of the user updating the logo</param>
    /// <returns>The number of rows affected (1 if successful, 0 if not found)</returns>
    public async Task<long> UpdateLogoAsync(long rowId, byte[] logo, string userName)
    {
        return await RepositoryExceptionHandler.ExecuteWithExceptionHandlingAsync(
            async () =>
            {
                _logger.LogDebug("Updating logo for branch with ID: {RowId}", rowId);

                // Find the branch to update
                var branch = await _context.Branches
                    .FirstOrDefaultAsync(b => b.RowId == rowId);

                if (branch == null)
                {
                    _logger.LogWarning("Branch with ID {RowId} not found for logo update", rowId);
                    return 0;
                }

                // Update the logo and audit fields
                branch.BranchLogo = logo;
                branch.UpdateUser = userName;
                branch.UpdateDate = DateTime.Now;

                var rowsAffected = await _context.SaveChangesAsync();

                if (rowsAffected > 0)
                {
                    _logger.LogInformation("Updated logo for branch with ID: {RowId}, Size: {Size} bytes", 
                        rowId, logo?.Length ?? 0);
                }

                return rowsAffected;
            },
            "UpdateBranchLogo",
            _logger);
    }

    /// <summary>
    /// Retrieves the branch logo using LINQ projection.
    /// Uses Select to retrieve only the logo column for performance optimization.
    /// </summary>
    /// <param name="rowId">The unique identifier of the branch</param>
    /// <returns>The logo image as byte array, null if not found</returns>
    public async Task<byte[]?> GetLogoAsync(long rowId)
    {
        return await RepositoryExceptionHandler.ExecuteWithExceptionHandlingAsync(
            async () =>
            {
                _logger.LogDebug("Retrieving logo for branch with ID: {RowId}", rowId);

                // Use projection to select only the logo column for performance
                var logo = await _context.Branches
                    .AsNoTracking()
                    .Where(b => b.RowId == rowId)
                    .Select(b => b.BranchLogo)
                    .FirstOrDefaultAsync();

                if (logo == null)
                {
                    _logger.LogDebug("Logo not found for branch with ID: {RowId}", rowId);
                }
                else
                {
                    _logger.LogDebug("Retrieved logo for branch with ID: {RowId}, Size: {Size} bytes", 
                        rowId, logo.Length);
                }

                return logo;
            },
            "GetBranchLogo",
            _logger);
    }
}
