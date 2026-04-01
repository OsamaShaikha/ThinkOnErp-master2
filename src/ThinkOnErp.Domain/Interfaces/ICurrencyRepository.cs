using ThinkOnErp.Domain.Entities;

namespace ThinkOnErp.Domain.Interfaces;

/// <summary>
/// Repository interface for SysCurrency entity data access operations.
/// Defines the contract for currency management in the Domain layer with zero external dependencies.
/// </summary>
public interface ICurrencyRepository
{
    /// <summary>
    /// Retrieves all active currencies from the database.
    /// Calls SP_SYS_CURRENCY_SELECT_ALL stored procedure.
    /// </summary>
    /// <returns>A list of all active SysCurrency entities</returns>
    Task<List<SysCurrency>> GetAllAsync();

    /// <summary>
    /// Retrieves a specific currency by its ID.
    /// Calls SP_SYS_CURRENCY_SELECT_BY_ID stored procedure.
    /// </summary>
    /// <param name="rowId">The unique identifier of the currency</param>
    /// <returns>The SysCurrency entity if found, null otherwise</returns>
    Task<SysCurrency?> GetByIdAsync(Int64 rowId);

    /// <summary>
    /// Creates a new currency in the database.
    /// Calls SP_SYS_CURRENCY_INSERT stored procedure.
    /// </summary>
    /// <param name="currency">The currency entity to create</param>
    /// <returns>The generated RowId from SEQ_SYS_CURRENCY sequence</returns>
    Task<Int64> CreateAsync(SysCurrency currency);

    /// <summary>
    /// Updates an existing currency in the database.
    /// Calls SP_SYS_CURRENCY_UPDATE stored procedure.
    /// </summary>
    /// <param name="currency">The currency entity with updated values</param>
    /// <returns>The number of rows affected</returns>
    Task<Int64> UpdateAsync(SysCurrency currency);

    /// <summary>
    /// Performs a soft delete on a currency by setting IS_ACTIVE to false.
    /// Calls SP_SYS_CURRENCY_DELETE stored procedure.
    /// </summary>
    /// <param name="rowId">The unique identifier of the currency to delete</param>
    /// <returns>The number of rows affected</returns>
    Task<Int64> DeleteAsync(Int64 rowId);
}
