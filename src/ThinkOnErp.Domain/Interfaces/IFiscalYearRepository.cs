using ThinkOnErp.Domain.Entities;

namespace ThinkOnErp.Domain.Interfaces;

/// <summary>
/// Repository interface for SysFiscalYear entity data access operations.
/// Defines the contract for fiscal year management in the Domain layer with zero external dependencies.
/// </summary>
public interface IFiscalYearRepository
{
    /// <summary>
    /// Retrieves all active fiscal years from the database.
    /// Calls SP_SYS_FISCAL_YEAR_SELECT_ALL stored procedure.
    /// </summary>
    /// <returns>A list of all active SysFiscalYear entities</returns>
    Task<List<SysFiscalYear>> GetAllAsync();

    /// <summary>
    /// Retrieves a specific fiscal year by its ID.
    /// Calls SP_SYS_FISCAL_YEAR_SELECT_BY_ID stored procedure.
    /// </summary>
    /// <param name="rowId">The unique identifier of the fiscal year</param>
    /// <returns>The SysFiscalYear entity if found, null otherwise</returns>
    Task<SysFiscalYear?> GetByIdAsync(Int64 rowId);

    /// <summary>
    /// Retrieves all fiscal years for a specific company.
    /// Calls SP_SYS_FISCAL_YEAR_SELECT_BY_COMPANY stored procedure.
    /// </summary>
    /// <param name="companyId">The company ID to retrieve fiscal years for</param>
    /// <returns>A list of SysFiscalYear entities for the specified company</returns>
    Task<List<SysFiscalYear>> GetByCompanyIdAsync(Int64 companyId);

    /// <summary>
    /// Retrieves all fiscal years for a specific branch.
    /// Calls SP_SYS_FISCAL_YEAR_SELECT_BY_BRANCH stored procedure.
    /// </summary>
    /// <param name="branchId">The branch ID to retrieve fiscal years for</param>
    /// <returns>A list of SysFiscalYear entities for the specified branch</returns>
    Task<List<SysFiscalYear>> GetByBranchIdAsync(Int64 branchId);

    /// <summary>
    /// Creates a new fiscal year in the database.
    /// Calls SP_SYS_FISCAL_YEAR_INSERT stored procedure.
    /// </summary>
    /// <param name="fiscalYear">The fiscal year entity to create</param>
    /// <returns>The generated RowId from SEQ_SYS_FISCAL_YEAR sequence</returns>
    Task<Int64> CreateAsync(SysFiscalYear fiscalYear);

    /// <summary>
    /// Updates an existing fiscal year in the database.
    /// Calls SP_SYS_FISCAL_YEAR_UPDATE stored procedure.
    /// </summary>
    /// <param name="fiscalYear">The fiscal year entity with updated values</param>
    /// <returns>The number of rows affected</returns>
    Task<Int64> UpdateAsync(SysFiscalYear fiscalYear);

    /// <summary>
    /// Performs a soft delete on a fiscal year by setting IS_ACTIVE to false.
    /// Calls SP_SYS_FISCAL_YEAR_DELETE stored procedure.
    /// </summary>
    /// <param name="rowId">The unique identifier of the fiscal year to delete</param>
    /// <returns>The number of rows affected</returns>
    Task<Int64> DeleteAsync(Int64 rowId);

    /// <summary>
    /// Closes a fiscal year by setting IS_CLOSED to true.
    /// Calls SP_SYS_FISCAL_YEAR_CLOSE stored procedure.
    /// </summary>
    /// <param name="rowId">The unique identifier of the fiscal year to close</param>
    /// <param name="userName">The username of the user closing the fiscal year</param>
    /// <returns>The number of rows affected</returns>
    Task<Int64> CloseAsync(Int64 rowId, string userName);
}
