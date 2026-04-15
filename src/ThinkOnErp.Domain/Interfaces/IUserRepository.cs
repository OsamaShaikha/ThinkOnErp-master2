using ThinkOnErp.Domain.Entities;

namespace ThinkOnErp.Domain.Interfaces;

/// <summary>
/// Repository interface for SysUser entity data access operations.
/// Defines the contract for user management in the Domain layer with zero external dependencies.
/// </summary>
public interface IUserRepository
{
    /// <summary>
    /// Retrieves all active users from the database.
    /// Calls SP_SYS_USERS_SELECT_ALL stored procedure.
    /// </summary>
    /// <returns>A list of all active SysUser entities</returns>
    Task<List<SysUser>> GetAllAsync();

    /// <summary>
    /// Retrieves a specific user by its ID.
    /// Calls SP_SYS_USERS_SELECT_BY_ID stored procedure.
    /// </summary>
    /// <param name="rowId">The unique identifier of the user</param>
    /// <returns>The SysUser entity if found, null otherwise</returns>
    Task<SysUser?> GetByIdAsync(Int64 rowId);

    /// <summary>
    /// Creates a new user in the database.
    /// Calls SP_SYS_USERS_INSERT stored procedure.
    /// </summary>
    /// <param name="user">The user entity to create</param>
    /// <returns>The generated RowId from SEQ_SYS_USERS sequence</returns>
    Task<Int64> CreateAsync(SysUser user);

    /// <summary>
    /// Updates an existing user in the database.
    /// Calls SP_SYS_USERS_UPDATE stored procedure.
    /// </summary>
    /// <param name="user">The user entity with updated values</param>
    /// <returns>The number of rows affected</returns>
    Task<Int64> UpdateAsync(SysUser user);

    /// <summary>
    /// Performs a soft delete on a user by setting IS_ACTIVE to false.
    /// Calls SP_SYS_USERS_DELETE stored procedure.
    /// </summary>
    /// <param name="rowId">The unique identifier of the user to delete</param>
    /// <returns>The number of rows affected</returns>
    Task<Int64> DeleteAsync(Int64 rowId);

    /// <summary>
    /// Retrieves all active users for a specific branch.
    /// </summary>
    /// <param name="branchId">The unique identifier of the branch</param>
    /// <returns>A list of SysUser entities belonging to the specified branch</returns>
    Task<List<SysUser>> GetByBranchIdAsync(Int64 branchId);

    /// <summary>
    /// Retrieves all active users for a specific company (through branches).
    /// </summary>
    /// <param name="companyId">The unique identifier of the company</param>
    /// <returns>A list of SysUser entities belonging to branches of the specified company</returns>
    Task<List<SysUser>> GetByCompanyIdAsync(Int64 companyId);

    /// <summary>
    /// Forces logout of a user by setting FORCE_LOGOUT_DATE and clearing refresh tokens.
    /// Calls SP_SYS_USERS_FORCE_LOGOUT stored procedure.
    /// </summary>
    /// <param name="userId">The unique identifier of the user to force logout</param>
    /// <param name="adminUser">The username of the admin performing the force logout</param>
    /// <returns>The number of rows affected</returns>
    Task<int> ForceLogoutAsync(long userId, string adminUser);
}
