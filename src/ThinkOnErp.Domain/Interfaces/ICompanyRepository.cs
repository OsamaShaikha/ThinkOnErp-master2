using ThinkOnErp.Domain.Entities;

namespace ThinkOnErp.Domain.Interfaces;

/// <summary>
/// Repository interface for SysCompany entity data access operations.
/// Defines the contract for company management in the Domain layer with zero external dependencies.
/// </summary>
public interface ICompanyRepository
{
    /// <summary>
    /// Retrieves all active companies from the database.
    /// Calls SP_SYS_COMPANY_SELECT_ALL stored procedure.
    /// </summary>
    /// <returns>A list of all active SysCompany entities</returns>
    Task<List<SysCompany>> GetAllAsync();

    /// <summary>
    /// Retrieves a specific company by its ID.
    /// Calls SP_SYS_COMPANY_SELECT_BY_ID stored procedure.
    /// </summary>
    /// <param name="rowId">The unique identifier of the company</param>
    /// <returns>The SysCompany entity if found, null otherwise</returns>
    Task<SysCompany?> GetByIdAsync(decimal rowId);

    /// <summary>
    /// Creates a new company in the database.
    /// Calls SP_SYS_COMPANY_INSERT stored procedure.
    /// </summary>
    /// <param name="company">The company entity to create</param>
    /// <returns>The generated RowId from SEQ_SYS_COMPANY sequence</returns>
    Task<decimal> CreateAsync(SysCompany company);

    /// <summary>
    /// Updates an existing company in the database.
    /// Calls SP_SYS_COMPANY_UPDATE stored procedure.
    /// </summary>
    /// <param name="company">The company entity with updated values</param>
    /// <returns>The number of rows affected</returns>
    Task<int> UpdateAsync(SysCompany company);

    /// <summary>
    /// Performs a soft delete on a company by setting IS_ACTIVE to false.
    /// Calls SP_SYS_COMPANY_DELETE stored procedure.
    /// </summary>
    /// <param name="rowId">The unique identifier of the company to delete</param>
    /// <returns>The number of rows affected</returns>
    Task<int> DeleteAsync(decimal rowId);
}
