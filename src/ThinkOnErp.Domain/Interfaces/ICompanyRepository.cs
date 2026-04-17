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
    Task<SysCompany?> GetByIdAsync(Int64 rowId);

    /// <summary>
    /// Creates a new company in the database.
    /// Calls SP_SYS_COMPANY_INSERT stored procedure.
    /// </summary>
    /// <param name="company">The company entity to create</param>
    /// <returns>The generated RowId from SEQ_SYS_COMPANY sequence</returns>
    Task<Int64> CreateAsync(SysCompany company);

    /// <summary>
    /// Updates an existing company in the database.
    /// Calls SP_SYS_COMPANY_UPDATE stored procedure.
    /// </summary>
    /// <param name="company">The company entity with updated values</param>
    /// <returns>The number of rows affected</returns>
    Task<Int64> UpdateAsync(SysCompany company);

    /// <summary>
    /// Performs a soft delete on a company by setting IS_ACTIVE to false.
    /// Calls SP_SYS_COMPANY_DELETE stored procedure.
    /// </summary>
    /// <param name="rowId">The unique identifier of the company to delete</param>
    /// <returns>The number of rows affected</returns>
    Task<Int64> DeleteAsync(Int64 rowId);

    /// <summary>
    /// Updates the company logo.
    /// Calls SP_SYS_COMPANY_UPDATE_LOGO stored procedure.
    /// </summary>
    /// <param name="rowId">The unique identifier of the company</param>
    /// <param name="logo">The logo image as byte array</param>
    /// <param name="userName">The username of the user updating the logo</param>
    /// <returns>The number of rows affected</returns>
    Task<Int64> UpdateLogoAsync(Int64 rowId, byte[] logo, string userName);

    /// <summary>
    /// Retrieves the company logo.
    /// Calls SP_SYS_COMPANY_GET_LOGO stored procedure.
    /// </summary>
    /// <param name="rowId">The unique identifier of the company</param>
    /// <returns>The logo image as byte array, null if not found</returns>
    Task<byte[]?> GetLogoAsync(Int64 rowId);
}
