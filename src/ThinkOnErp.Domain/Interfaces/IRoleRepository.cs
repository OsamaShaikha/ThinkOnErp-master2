using ThinkOnErp.Domain.Entities;

namespace ThinkOnErp.Domain.Interfaces;

/// <summary>
/// Repository interface for SysRole entity data access operations.
/// Defines the contract for role management in the Domain layer with zero external dependencies.
/// </summary>
public interface IRoleRepository
{
    /// <summary>
    /// Retrieves all active roles from the database.
    /// Calls SP_SYS_ROLE_SELECT_ALL stored procedure.
    /// </summary>
    /// <returns>A list of all active SysRole entities</returns>
    Task<List<SysRole>> GetAllAsync();

    /// <summary>
    /// Retrieves a specific role by its ID.
    /// Calls SP_SYS_ROLE_SELECT_BY_ID stored procedure.
    /// </summary>
    /// <param name="rowId">The unique identifier of the role</param>
    /// <returns>The SysRole entity if found, null otherwise</returns>
    Task<SysRole?> GetByIdAsync(Int64 rowId);

    /// <summary>
    /// Creates a new role in the database.
    /// Calls SP_SYS_ROLE_INSERT stored procedure.
    /// </summary>
    /// <param name="role">The role entity to create</param>
    /// <returns>The generated RowId from SEQ_SYS_ROLE sequence</returns>
    Task<Int64> CreateAsync(SysRole role);

    /// <summary>
    /// Updates an existing role in the database.
    /// Calls SP_SYS_ROLE_UPDATE stored procedure.
    /// </summary>
    /// <param name="role">The role entity with updated values</param>
    /// <returns>The number of rows affected</returns>
    Task<Int64> UpdateAsync(SysRole role);

    /// <summary>
    /// Performs a soft delete on a role by setting IS_ACTIVE to false.
    /// Calls SP_SYS_ROLE_DELETE stored procedure.
    /// </summary>
    /// <param name="rowId">The unique identifier of the role to delete</param>
    /// <returns>The number of rows affected</returns>
    Task<Int64> DeleteAsync(Int64 rowId);
}
