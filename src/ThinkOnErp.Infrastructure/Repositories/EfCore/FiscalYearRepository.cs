using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using ThinkOnErp.Domain.Entities;
using ThinkOnErp.Domain.Interfaces;
using ThinkOnErp.Infrastructure.Data;
using ThinkOnErp.Infrastructure.Exceptions;

namespace ThinkOnErp.Infrastructure.Repositories.EfCore;

/// <summary>
/// EF Core repository implementation for SysFiscalYear entity using LINQ queries.
/// Implements IFiscalYearRepository interface from the Domain layer.
/// Uses ThinkOnErpDbContext for database operations with automatic change tracking and ID generation.
/// </summary>
public class FiscalYearRepository : IFiscalYearRepository
{
    private readonly ThinkOnErpDbContext _context;
    private readonly ILogger<FiscalYearRepository> _logger;

    /// <summary>
    /// Initializes a new instance of the FiscalYearRepository class.
    /// </summary>
    /// <param name="context">The EF Core database context for entity management.</param>
    /// <param name="logger">Logger for recording operations and errors.</param>
    /// <exception cref="ArgumentNullException">Thrown when context or logger is null.</exception>
    public FiscalYearRepository(
        ThinkOnErpDbContext context,
        ILogger<FiscalYearRepository> logger)
    {
        _context = context ?? throw new ArgumentNullException(nameof(context));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <summary>
    /// Retrieves all active fiscal years from the database using LINQ.
    /// Uses AsNoTracking for read-only query optimization.
    /// Eagerly loads Company and Branch navigation properties.
    /// Global query filter automatically filters out soft-deleted records (IsActive = false).
    /// </summary>
    /// <returns>A list of all active SysFiscalYear entities ordered by StartDate descending</returns>
    public async Task<List<SysFiscalYear>> GetAllAsync()
    {
        return await RepositoryExceptionHandler.ExecuteWithExceptionHandlingAsync(
            async () =>
            {
                _logger.LogDebug("Retrieving all fiscal years");

                var fiscalYears = await _context.FiscalYears
                    .AsNoTracking()
                    .Include(f => f.Company)
                    .Include(f => f.Branch)
                    .OrderByDescending(f => f.StartDate)
                    .ToListAsync();

                _logger.LogDebug("Retrieved {Count} fiscal years", fiscalYears.Count);
                return fiscalYears;
            },
            "GetAllFiscalYears",
            _logger);
    }

    /// <summary>
    /// Retrieves a specific fiscal year by its ID using LINQ.
    /// Uses AsNoTracking for read-only query optimization.
    /// Eagerly loads Company and Branch navigation properties.
    /// Global query filter automatically filters out soft-deleted records (IsActive = false).
    /// </summary>
    /// <param name="rowId">The unique identifier of the fiscal year</param>
    /// <returns>The SysFiscalYear entity if found, null otherwise</returns>
    public async Task<SysFiscalYear?> GetByIdAsync(long rowId)
    {
        return await RepositoryExceptionHandler.ExecuteWithExceptionHandlingAsync(
            async () =>
            {
                _logger.LogDebug("Retrieving fiscal year with ID: {RowId}", rowId);

                var fiscalYear = await _context.FiscalYears
                    .AsNoTracking()
                    .Include(f => f.Company)
                    .Include(f => f.Branch)
                    .FirstOrDefaultAsync(f => f.RowId == rowId);

                if (fiscalYear == null)
                {
                    _logger.LogDebug("Fiscal year with ID {RowId} not found", rowId);
                }
                else
                {
                    _logger.LogDebug("Retrieved fiscal year: {FiscalYearCode}", fiscalYear.FiscalYearCode);
                }

                return fiscalYear;
            },
            "GetFiscalYearById",
            _logger);
    }

    /// <summary>
    /// Retrieves all active fiscal years for a specific company using LINQ.
    /// Uses AsNoTracking for read-only query optimization.
    /// Eagerly loads Company and Branch navigation properties.
    /// </summary>
    /// <param name="companyId">The unique identifier of the company</param>
    /// <returns>A list of SysFiscalYear entities belonging to the specified company</returns>
    public async Task<List<SysFiscalYear>> GetByCompanyIdAsync(long companyId)
    {
        return await RepositoryExceptionHandler.ExecuteWithExceptionHandlingAsync(
            async () =>
            {
                _logger.LogDebug("Retrieving fiscal years for company ID: {CompanyId}", companyId);

                var fiscalYears = await _context.FiscalYears
                    .AsNoTracking()
                    .Include(f => f.Company)
                    .Include(f => f.Branch)
                    .Where(f => f.CompanyId == companyId)
                    .OrderByDescending(f => f.StartDate)
                    .ToListAsync();

                _logger.LogDebug("Retrieved {Count} fiscal years for company ID: {CompanyId}", 
                    fiscalYears.Count, companyId);
                return fiscalYears;
            },
            "GetFiscalYearsByCompanyId",
            _logger);
    }

    /// <summary>
    /// Retrieves all active fiscal years for a specific branch using LINQ.
    /// Uses AsNoTracking for read-only query optimization.
    /// Eagerly loads Company and Branch navigation properties.
    /// </summary>
    /// <param name="branchId">The unique identifier of the branch</param>
    /// <returns>A list of SysFiscalYear entities belonging to the specified branch</returns>
    public async Task<List<SysFiscalYear>> GetByBranchIdAsync(long branchId)
    {
        return await RepositoryExceptionHandler.ExecuteWithExceptionHandlingAsync(
            async () =>
            {
                _logger.LogDebug("Retrieving fiscal years for branch ID: {BranchId}", branchId);

                var fiscalYears = await _context.FiscalYears
                    .AsNoTracking()
                    .Include(f => f.Company)
                    .Include(f => f.Branch)
                    .Where(f => f.BranchId == branchId)
                    .OrderByDescending(f => f.StartDate)
                    .ToListAsync();

                _logger.LogDebug("Retrieved {Count} fiscal years for branch ID: {BranchId}", 
                    fiscalYears.Count, branchId);
                return fiscalYears;
            },
            "GetFiscalYearsByBranchId",
            _logger);
    }

    /// <summary>
    /// Creates a new fiscal year in the database using EF Core.
    /// EF Core automatically retrieves the generated ID from Oracle sequence SEQ_SYS_FISCAL_YEAR.
    /// </summary>
    /// <param name="fiscalYear">The fiscal year entity to create</param>
    /// <returns>The generated RowId from SEQ_SYS_FISCAL_YEAR sequence</returns>
    public async Task<long> CreateAsync(SysFiscalYear fiscalYear)
    {
        return await RepositoryExceptionHandler.ExecuteWithExceptionHandlingAsync(
            async () =>
            {
                _logger.LogDebug("Creating new fiscal year: {FiscalYearCode}", fiscalYear.FiscalYearCode);

                // Set creation date if not already set
                if (!fiscalYear.CreationDate.HasValue)
                {
                    fiscalYear.CreationDate = DateTime.Now;
                }

                // Set IsActive to true by default
                fiscalYear.IsActive = true;

                // Set IsClosed to false by default for new fiscal years
                if (!fiscalYear.IsClosed)
                {
                    fiscalYear.IsClosed = false;
                }

                // Add the entity to the context
                _context.FiscalYears.Add(fiscalYear);

                // Save changes - EF Core will automatically retrieve the generated ID from the sequence
                await _context.SaveChangesAsync();

                _logger.LogInformation("Created fiscal year with ID: {RowId}, Code: {FiscalYearCode}, Branch: {BranchId}", 
                    fiscalYear.RowId, fiscalYear.FiscalYearCode, fiscalYear.BranchId);

                return fiscalYear.RowId;
            },
            "CreateFiscalYear",
            _logger);
    }

    /// <summary>
    /// Updates an existing fiscal year in the database using EF Core.
    /// EF Core automatically tracks changes and generates the appropriate UPDATE statement.
    /// </summary>
    /// <param name="fiscalYear">The fiscal year entity with updated values</param>
    /// <returns>The number of rows affected (1 if successful, 0 if not found)</returns>
    public async Task<long> UpdateAsync(SysFiscalYear fiscalYear)
    {
        return await RepositoryExceptionHandler.ExecuteWithExceptionHandlingAsync(
            async () =>
            {
                _logger.LogDebug("Updating fiscal year with ID: {RowId}", fiscalYear.RowId);

                // Set update date
                fiscalYear.UpdateDate = DateTime.Now;

                // Update the entity - EF Core will track changes
                _context.FiscalYears.Update(fiscalYear);

                // Save changes and get the number of affected rows
                var rowsAffected = await _context.SaveChangesAsync();

                if (rowsAffected > 0)
                {
                    _logger.LogInformation("Updated fiscal year with ID: {RowId}, Code: {FiscalYearCode}", 
                        fiscalYear.RowId, fiscalYear.FiscalYearCode);
                }
                else
                {
                    _logger.LogWarning("No rows affected when updating fiscal year with ID: {RowId}", 
                        fiscalYear.RowId);
                }

                return rowsAffected;
            },
            "UpdateFiscalYear",
            _logger);
    }

    /// <summary>
    /// Performs a soft delete on a fiscal year by setting IS_ACTIVE to false.
    /// Loads the entity, modifies the IsActive property, and saves changes.
    /// </summary>
    /// <param name="rowId">The unique identifier of the fiscal year to delete</param>
    /// <returns>The number of rows affected (1 if successful, 0 if not found)</returns>
    public async Task<long> DeleteAsync(long rowId)
    {
        return await RepositoryExceptionHandler.ExecuteWithExceptionHandlingAsync(
            async () =>
            {
                _logger.LogDebug("Soft deleting fiscal year with ID: {RowId}", rowId);

                // Find the fiscal year to delete - need to ignore query filter to find it even if already deleted
                var fiscalYear = await _context.FiscalYears
                    .IgnoreQueryFilters()
                    .FirstOrDefaultAsync(f => f.RowId == rowId);

                if (fiscalYear == null)
                {
                    _logger.LogWarning("Fiscal year with ID {RowId} not found for deletion", rowId);
                    return 0;
                }

                // Perform soft delete by setting IsActive to false
                fiscalYear.IsActive = false;
                fiscalYear.UpdateDate = DateTime.Now;

                var rowsAffected = await _context.SaveChangesAsync();

                if (rowsAffected > 0)
                {
                    _logger.LogInformation("Soft deleted fiscal year with ID: {RowId}, Code: {FiscalYearCode}", 
                        rowId, fiscalYear.FiscalYearCode);
                }

                return rowsAffected;
            },
            "DeleteFiscalYear",
            _logger);
    }

    /// <summary>
    /// Closes a fiscal year by setting IS_CLOSED to true.
    /// Loads the entity, modifies the IsClosed property, and saves changes.
    /// Closed fiscal years typically cannot be modified.
    /// </summary>
    /// <param name="rowId">The unique identifier of the fiscal year to close</param>
    /// <param name="userName">The username of the user closing the fiscal year</param>
    /// <returns>The number of rows affected (1 if successful, 0 if not found)</returns>
    public async Task<long> CloseAsync(long rowId, string userName)
    {
        return await RepositoryExceptionHandler.ExecuteWithExceptionHandlingAsync(
            async () =>
            {
                _logger.LogDebug("Closing fiscal year with ID: {RowId}", rowId);

                // Find the fiscal year to close
                var fiscalYear = await _context.FiscalYears
                    .FirstOrDefaultAsync(f => f.RowId == rowId);

                if (fiscalYear == null)
                {
                    _logger.LogWarning("Fiscal year with ID {RowId} not found for closing", rowId);
                    return 0;
                }

                // Check if already closed
                if (fiscalYear.IsClosed)
                {
                    _logger.LogWarning("Fiscal year with ID {RowId} is already closed", rowId);
                    throw new InvalidOperationException($"Fiscal year with ID {rowId} is already closed");
                }

                // Close the fiscal year by setting IsClosed to true
                fiscalYear.IsClosed = true;
                fiscalYear.UpdateUser = userName;
                fiscalYear.UpdateDate = DateTime.Now;

                var rowsAffected = await _context.SaveChangesAsync();

                if (rowsAffected > 0)
                {
                    _logger.LogInformation("Closed fiscal year with ID: {RowId}, Code: {FiscalYearCode}", 
                        rowId, fiscalYear.FiscalYearCode);
                }

                return rowsAffected;
            },
            "CloseFiscalYear",
            _logger);
    }
}
