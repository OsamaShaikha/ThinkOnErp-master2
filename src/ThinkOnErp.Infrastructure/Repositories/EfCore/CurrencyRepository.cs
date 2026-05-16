using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using ThinkOnErp.Domain.Entities;
using ThinkOnErp.Domain.Interfaces;
using ThinkOnErp.Infrastructure.Data;
using ThinkOnErp.Infrastructure.Exceptions;

namespace ThinkOnErp.Infrastructure.Repositories.EfCore;

/// <summary>
/// EF Core repository implementation for SysCurrency entity using LINQ queries.
/// Implements ICurrencyRepository interface from the Domain layer.
/// Uses ThinkOnErpDbContext for database operations with automatic change tracking and ID generation.
/// </summary>
public class CurrencyRepository : ICurrencyRepository
{
    private readonly ThinkOnErpDbContext _context;
    private readonly ILogger<CurrencyRepository> _logger;

    /// <summary>
    /// Initializes a new instance of the CurrencyRepository class.
    /// </summary>
    /// <param name="context">The EF Core database context for entity management.</param>
    /// <param name="logger">Logger for recording operations and errors.</param>
    /// <exception cref="ArgumentNullException">Thrown when context or logger is null.</exception>
    public CurrencyRepository(
        ThinkOnErpDbContext context,
        ILogger<CurrencyRepository> logger)
    {
        _context = context ?? throw new ArgumentNullException(nameof(context));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <summary>
    /// Retrieves all active currencies from the database using LINQ.
    /// Uses AsNoTracking for read-only query optimization.
    /// </summary>
    /// <returns>A list of all active SysCurrency entities ordered by RowDesc</returns>
    public async Task<List<SysCurrency>> GetAllAsync()
    {
        return await RepositoryExceptionHandler.ExecuteWithExceptionHandlingAsync(
            async () =>
            {
                _logger.LogDebug("Retrieving all currencies");

                var currencies = await _context.Currencies
                    .AsNoTracking()
                    .OrderBy(c => c.RowDesc)
                    .ToListAsync();

                _logger.LogDebug("Retrieved {Count} currencies", currencies.Count);
                return currencies;
            },
            "GetAllCurrencies",
            _logger);
    }

    /// <summary>
    /// Retrieves a specific currency by its ID using LINQ.
    /// Uses AsNoTracking for read-only query optimization.
    /// </summary>
    /// <param name="rowId">The unique identifier of the currency</param>
    /// <returns>The SysCurrency entity if found, null otherwise</returns>
    public async Task<SysCurrency?> GetByIdAsync(long rowId)
    {
        return await RepositoryExceptionHandler.ExecuteWithExceptionHandlingAsync(
            async () =>
            {
                _logger.LogDebug("Retrieving currency with ID: {RowId}", rowId);

                var currency = await _context.Currencies
                    .AsNoTracking()
                    .FirstOrDefaultAsync(c => c.RowId == rowId);

                if (currency == null)
                {
                    _logger.LogDebug("Currency with ID {RowId} not found", rowId);
                }
                else
                {
                    _logger.LogDebug("Retrieved currency: {RowDesc}", currency.RowDesc);
                }

                return currency;
            },
            "GetCurrencyById",
            _logger);
    }

    /// <summary>
    /// Creates a new currency in the database using EF Core.
    /// EF Core automatically retrieves the generated ID from Oracle sequence SEQ_SYS_CURRENCY.
    /// </summary>
    /// <param name="currency">The currency entity to create</param>
    /// <returns>The generated RowId from SEQ_SYS_CURRENCY sequence</returns>
    public async Task<long> CreateAsync(SysCurrency currency)
    {
        return await RepositoryExceptionHandler.ExecuteWithExceptionHandlingAsync(
            async () =>
            {
                _logger.LogDebug("Creating new currency: {RowDesc}", currency.RowDesc);

                // Set creation date if not already set
                if (!currency.CreationDate.HasValue)
                {
                    currency.CreationDate = DateTime.Now;
                }

                // Add the entity to the context
                _context.Currencies.Add(currency);

                // Save changes - EF Core will automatically retrieve the generated ID from the sequence
                await _context.SaveChangesAsync();

                _logger.LogInformation("Created currency with ID: {RowId}, Name: {RowDesc}", 
                    currency.RowId, currency.RowDesc);

                return currency.RowId;
            },
            "CreateCurrency",
            _logger);
    }

    /// <summary>
    /// Updates an existing currency in the database using EF Core.
    /// EF Core automatically tracks changes and generates the appropriate UPDATE statement.
    /// </summary>
    /// <param name="currency">The currency entity with updated values</param>
    /// <returns>The number of rows affected (1 if successful, 0 if not found)</returns>
    public async Task<long> UpdateAsync(SysCurrency currency)
    {
        return await RepositoryExceptionHandler.ExecuteWithExceptionHandlingAsync(
            async () =>
            {
                _logger.LogDebug("Updating currency with ID: {RowId}", currency.RowId);

                // Set update date
                currency.UpdateDate = DateTime.Now;

                // Update the entity - EF Core will track changes
                _context.Currencies.Update(currency);

                // Save changes and get the number of affected rows
                var rowsAffected = await _context.SaveChangesAsync();

                if (rowsAffected > 0)
                {
                    _logger.LogInformation("Updated currency with ID: {RowId}, Name: {RowDesc}", 
                        currency.RowId, currency.RowDesc);
                }
                else
                {
                    _logger.LogWarning("No rows affected when updating currency with ID: {RowId}", 
                        currency.RowId);
                }

                return rowsAffected;
            },
            "UpdateCurrency",
            _logger);
    }

    /// <summary>
    /// Performs a soft delete on a currency by setting IS_ACTIVE to false.
    /// Loads the entity, modifies the IsActive property, and saves changes.
    /// Note: The SysCurrency entity currently doesn't have an IsActive property.
    /// This implementation performs a hard delete instead.
    /// </summary>
    /// <param name="rowId">The unique identifier of the currency to delete</param>
    /// <returns>The number of rows affected (1 if successful, 0 if not found)</returns>
    public async Task<long> DeleteAsync(long rowId)
    {
        return await RepositoryExceptionHandler.ExecuteWithExceptionHandlingAsync(
            async () =>
            {
                _logger.LogDebug("Deleting currency with ID: {RowId}", rowId);

                // Find the currency to delete
                var currency = await _context.Currencies
                    .FirstOrDefaultAsync(c => c.RowId == rowId);

                if (currency == null)
                {
                    _logger.LogWarning("Currency with ID {RowId} not found for deletion", rowId);
                    return 0;
                }

                // Note: SysCurrency doesn't have an IsActive property in the current entity definition
                // If soft delete is required, the entity needs to be updated with an IsActive property
                // For now, we'll perform a hard delete as per the stored procedure behavior
                _context.Currencies.Remove(currency);

                var rowsAffected = await _context.SaveChangesAsync();

                if (rowsAffected > 0)
                {
                    _logger.LogInformation("Deleted currency with ID: {RowId}, Name: {RowDesc}", 
                        rowId, currency.RowDesc);
                }

                return rowsAffected;
            },
            "DeleteCurrency",
            _logger);
    }
}
