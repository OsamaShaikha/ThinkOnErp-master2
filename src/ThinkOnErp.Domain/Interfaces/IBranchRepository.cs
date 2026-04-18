using ThinkOnErp.Domain.Entities;

namespace ThinkOnErp.Domain.Interfaces;

/// <summary>
/// Repository interface for SysBranch entity data access operations.
/// Defines the contract for branch management in the Domain layer with zero external dependencies.
/// </summary>
public interface IBranchRepository
{
    /// <summary>
    /// Retrieves all active branches from the database.
    /// Calls SP_SYS_BRANCH_SELECT_ALL stored procedure.
    /// </summary>
    /// <returns>A list of all active SysBranch entities</returns>
    Task<List<SysBranch>> GetAllAsync();

    /// <summary>
    /// Retrieves a specific branch by its ID.
    /// Calls SP_SYS_BRANCH_SELECT_BY_ID stored procedure.
    /// </summary>
    /// <param name="rowId">The unique identifier of the branch</param>
    /// <returns>The SysBranch entity if found, null otherwise</returns>
    Task<SysBranch?> GetByIdAsync(Int64 rowId);

    /// <summary>
    /// Creates a new branch in the database.
    /// Calls SP_SYS_BRANCH_INSERT stored procedure.
    /// </summary>
    /// <param name="branch">The branch entity to create</param>
    /// <returns>The generated RowId from SEQ_SYS_BRANCH sequence</returns>
    Task<Int64> CreateAsync(SysBranch branch);

    /// <summary>
    /// Updates an existing branch in the database.
    /// Calls SP_SYS_BRANCH_UPDATE stored procedure.
    /// </summary>
    /// <param name="branch">The branch entity with updated values</param>
    /// <returns>The number of rows affected</returns>
    Task<Int64> UpdateAsync(SysBranch branch);

    /// <summary>
    /// Performs a soft delete on a branch by setting IS_ACTIVE to false.
    /// Calls SP_SYS_BRANCH_DELETE stored procedure.
    /// </summary>
    /// <param name="rowId">The unique identifier of the branch to delete</param>
    /// <returns>The number of rows affected</returns>
    Task<Int64> DeleteAsync(Int64 rowId);

    /// <summary>
    /// Retrieves all active branches for a specific company.
    /// </summary>
    /// <param name="companyId">The unique identifier of the company</param>
    /// <returns>A list of SysBranch entities belonging to the specified company</returns>
    Task<List<SysBranch>> GetByCompanyIdAsync(Int64 companyId);

    /// <summary>
    /// Updates the branch logo.
    /// Calls SP_SYS_BRANCH_UPDATE_LOGO stored procedure.
    /// </summary>
    /// <param name="rowId">The unique identifier of the branch</param>
    /// <param name="logo">The logo image as byte array</param>
    /// <param name="userName">The username of the user updating the logo</param>
    /// <returns>The number of rows affected</returns>
    Task<Int64> UpdateLogoAsync(Int64 rowId, byte[] logo, string userName);

    /// <summary>
    /// Retrieves the branch logo.
    /// Calls SP_SYS_BRANCH_GET_LOGO stored procedure.
    /// </summary>
    /// <param name="rowId">The unique identifier of the branch</param>
    /// <returns>The logo image as byte array, null if not found</returns>
    Task<byte[]?> GetLogoAsync(Int64 rowId);
}
