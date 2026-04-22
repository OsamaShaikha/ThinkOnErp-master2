using Oracle.ManagedDataAccess.Client;
using System.Data;
using ThinkOnErp.Domain.Entities;
using ThinkOnErp.Domain.Interfaces;
using ThinkOnErp.Infrastructure.Data;

namespace ThinkOnErp.Infrastructure.Repositories;

/// <summary>
/// Repository implementation for SysUser entity using ADO.NET with Oracle stored procedures.
/// Implements IUserRepository interface from the Domain layer.
/// Uses OracleDbContext to create connections and maps Oracle data types to C# types.
/// </summary>
public class UserRepository : IUserRepository
{
    private readonly OracleDbContext _dbContext;

    /// <summary>
    /// Initializes a new instance of the UserRepository class.
    /// </summary>
    /// <param name="dbContext">The Oracle database context for connection management.</param>
    /// <exception cref="ArgumentNullException">Thrown when dbContext is null.</exception>
    public UserRepository(OracleDbContext dbContext)
    {
        _dbContext = dbContext ?? throw new ArgumentNullException(nameof(dbContext));
    }

    /// <summary>
    /// Retrieves all active users from the database.
    /// Calls SP_SYS_USERS_SELECT_ALL stored procedure.
    /// </summary>
    /// <returns>A list of all active SysUser entities</returns>
    public async Task<List<SysUser>> GetAllAsync()
    {
        List<SysUser> users = new();

        using (var connection = _dbContext.CreateConnection())
        {
            await connection.OpenAsync();

            using var command = connection.CreateCommand();
            command.CommandType = CommandType.StoredProcedure;
            command.CommandText = "SP_SYS_USERS_SELECT_ALL";

            // Add output parameter for SYS_REFCURSOR
            OracleParameter cursorParam = new()
            {
                ParameterName = "P_RESULT_CURSOR",
                OracleDbType = OracleDbType.RefCursor,
                Direction = ParameterDirection.Output
            };
            _ = command.Parameters.Add(cursorParam);

            using var reader = await command.ExecuteReaderAsync();
            while (await reader.ReadAsync())
            {
                users.Add(MapToEntity(reader));
            }
        }

        return users;
    }

    /// <summary>
    /// Retrieves a specific user by its ID.
    /// Calls SP_SYS_USERS_SELECT_BY_ID stored procedure.
    /// </summary>
    /// <param name="rowId">The unique identifier of the user</param>
    /// <returns>The SysUser entity if found, null otherwise</returns>
    public async Task<SysUser?> GetByIdAsync(long rowId)
    {
        using var connection = _dbContext.CreateConnection();
        await connection.OpenAsync();

        using var command = connection.CreateCommand();
        command.CommandType = CommandType.StoredProcedure;
        command.CommandText = "SP_SYS_USERS_SELECT_BY_ID";

        // Add input parameter for ROW_ID
        OracleParameter idParam = new()
        {
            ParameterName = "P_ROW_ID",
            OracleDbType = OracleDbType.Decimal,
            Direction = ParameterDirection.Input,
            Value = rowId
        };
        _ = command.Parameters.Add(idParam);

        // Add output parameter for SYS_REFCURSOR
        OracleParameter cursorParam = new()
        {
            ParameterName = "P_RESULT_CURSOR",
            OracleDbType = OracleDbType.RefCursor,
            Direction = ParameterDirection.Output
        };
        _ = command.Parameters.Add(cursorParam);

        using var reader = await command.ExecuteReaderAsync();
        if (await reader.ReadAsync())
        {
            return MapToEntity(reader);
        }

        return null;
    }

    /// <summary>
    /// Creates a new user in the database.
    /// Calls SP_SYS_USERS_INSERT stored procedure.
    /// </summary>
    /// <param name="user">The user entity to create</param>
    /// <returns>The generated RowId from SEQ_SYS_USERS sequence</returns>
    public async Task<long> CreateAsync(SysUser user)
    {
        using var connection = _dbContext.CreateConnection();
        await connection.OpenAsync();

        using var command = connection.CreateCommand();
        command.CommandType = CommandType.StoredProcedure;
        command.CommandText = "SP_SYS_USERS_INSERT";

        // Add input parameters
        _ = command.Parameters.Add(new OracleParameter
        {
            ParameterName = "P_ROW_DESC",
            OracleDbType = OracleDbType.Varchar2,
            Direction = ParameterDirection.Input,
            Value = user.RowDesc
        });

        _ = command.Parameters.Add(new OracleParameter
        {
            ParameterName = "P_ROW_DESC_E",
            OracleDbType = OracleDbType.Varchar2,
            Direction = ParameterDirection.Input,
            Value = user.RowDescE
        });

        _ = command.Parameters.Add(new OracleParameter
        {
            ParameterName = "P_USER_NAME",
            OracleDbType = OracleDbType.Varchar2,
            Direction = ParameterDirection.Input,
            Value = user.UserName
        });

        _ = command.Parameters.Add(new OracleParameter
        {
            ParameterName = "P_PASSWORD",
            OracleDbType = OracleDbType.Varchar2,
            Direction = ParameterDirection.Input,
            Value = user.Password
        });

        _ = command.Parameters.Add(new OracleParameter
        {
            ParameterName = "P_PHONE",
            OracleDbType = OracleDbType.Varchar2,
            Direction = ParameterDirection.Input,
            Value = (object?)user.Phone ?? DBNull.Value
        });

        _ = command.Parameters.Add(new OracleParameter
        {
            ParameterName = "P_PHONE2",
            OracleDbType = OracleDbType.Varchar2,
            Direction = ParameterDirection.Input,
            Value = (object?)user.Phone2 ?? DBNull.Value
        });

        _ = command.Parameters.Add(new OracleParameter
        {
            ParameterName = "P_ROLE",
            OracleDbType = OracleDbType.Decimal,
            Direction = ParameterDirection.Input,
            Value = (object?)user.Role ?? DBNull.Value
        });

        _ = command.Parameters.Add(new OracleParameter
        {
            ParameterName = "P_BRANCH_ID",
            OracleDbType = OracleDbType.Decimal,
            Direction = ParameterDirection.Input,
            Value = (object?)user.BranchId ?? DBNull.Value
        });

        _ = command.Parameters.Add(new OracleParameter
        {
            ParameterName = "P_EMAIL",
            OracleDbType = OracleDbType.Varchar2,
            Direction = ParameterDirection.Input,
            Value = (object?)user.Email ?? DBNull.Value
        });

        _ = command.Parameters.Add(new OracleParameter
        {
            ParameterName = "P_IS_ADMIN",
            OracleDbType = OracleDbType.Char,
            Direction = ParameterDirection.Input,
            Value = user.IsAdmin ? "1" : "0"
        });

        _ = command.Parameters.Add(new OracleParameter
        {
            ParameterName = "P_CREATION_USER",
            OracleDbType = OracleDbType.Varchar2,
            Direction = ParameterDirection.Input,
            Value = user.CreationUser
        });

        // Add output parameter for new ID
        OracleParameter newIdParam = new()
        {
            ParameterName = "P_NEW_ID",
            OracleDbType = OracleDbType.Decimal,
            Direction = ParameterDirection.Output
        };
        _ = command.Parameters.Add(newIdParam);

        _ = await command.ExecuteNonQueryAsync();

        // Return the generated ID
        return long.Parse(newIdParam.Value.ToString());
    }

    /// <summary>
    /// Updates an existing user in the database.
    /// Calls SP_SYS_USERS_UPDATE stored procedure.
    /// </summary>
    /// <param name="user">The user entity with updated values</param>
    /// <returns>The number of rows affected</returns>
    public async Task<long> UpdateAsync(SysUser user)
    {
        using var connection = _dbContext.CreateConnection();
        await connection.OpenAsync();

        using var command = connection.CreateCommand();
        command.CommandType = CommandType.StoredProcedure;
        command.CommandText = "SP_SYS_USERS_UPDATE";

        // Add input parameters
        _ = command.Parameters.Add(new OracleParameter
        {
            ParameterName = "P_ROW_ID",
            OracleDbType = OracleDbType.Decimal,
            Direction = ParameterDirection.Input,
            Value = user.RowId
        });

        _ = command.Parameters.Add(new OracleParameter
        {
            ParameterName = "P_ROW_DESC",
            OracleDbType = OracleDbType.Varchar2,
            Direction = ParameterDirection.Input,
            Value = user.RowDesc
        });

        _ = command.Parameters.Add(new OracleParameter
        {
            ParameterName = "P_ROW_DESC_E",
            OracleDbType = OracleDbType.Varchar2,
            Direction = ParameterDirection.Input,
            Value = user.RowDescE
        });

        _ = command.Parameters.Add(new OracleParameter
        {
            ParameterName = "P_USER_NAME",
            OracleDbType = OracleDbType.Varchar2,
            Direction = ParameterDirection.Input,
            Value = user.UserName
        });

        _ = command.Parameters.Add(new OracleParameter
        {
            ParameterName = "P_PASSWORD",
            OracleDbType = OracleDbType.Varchar2,
            Direction = ParameterDirection.Input,
            Value = user.Password
        });

        _ = command.Parameters.Add(new OracleParameter
        {
            ParameterName = "P_PHONE",
            OracleDbType = OracleDbType.Varchar2,
            Direction = ParameterDirection.Input,
            Value = (object?)user.Phone ?? DBNull.Value
        });

        _ = command.Parameters.Add(new OracleParameter
        {
            ParameterName = "P_PHONE2",
            OracleDbType = OracleDbType.Varchar2,
            Direction = ParameterDirection.Input,
            Value = (object?)user.Phone2 ?? DBNull.Value
        });

        _ = command.Parameters.Add(new OracleParameter
        {
            ParameterName = "P_ROLE",
            OracleDbType = OracleDbType.Decimal,
            Direction = ParameterDirection.Input,
            Value = (object?)user.Role ?? DBNull.Value
        });

        _ = command.Parameters.Add(new OracleParameter
        {
            ParameterName = "P_BRANCH_ID",
            OracleDbType = OracleDbType.Decimal,
            Direction = ParameterDirection.Input,
            Value = (object?)user.BranchId ?? DBNull.Value
        });

        _ = command.Parameters.Add(new OracleParameter
        {
            ParameterName = "P_EMAIL",
            OracleDbType = OracleDbType.Varchar2,
            Direction = ParameterDirection.Input,
            Value = (object?)user.Email ?? DBNull.Value
        });

        _ = command.Parameters.Add(new OracleParameter
        {
            ParameterName = "P_IS_ADMIN",
            OracleDbType = OracleDbType.Char,
            Direction = ParameterDirection.Input,
            Value = user.IsAdmin ? "1" : "0"
        });

        _ = command.Parameters.Add(new OracleParameter
        {
            ParameterName = "P_UPDATE_USER",
            OracleDbType = OracleDbType.Varchar2,
            Direction = ParameterDirection.Input,
            Value = user.UpdateUser ?? string.Empty
        });

        return await command.ExecuteNonQueryAsync();
    }

    /// <summary>
    /// Performs a soft delete on a user by setting IS_ACTIVE to false.
    /// Calls SP_SYS_USERS_DELETE stored procedure.
    /// </summary>
    /// <param name="rowId">The unique identifier of the user to delete</param>
    /// <returns>The number of rows affected</returns>
    public async Task<long> DeleteAsync(long rowId)
    {
        using var connection = _dbContext.CreateConnection();
        await connection.OpenAsync();

        using var command = connection.CreateCommand();
        command.CommandType = CommandType.StoredProcedure;
        command.CommandText = "SP_SYS_USERS_DELETE";

        // Add input parameter for ROW_ID
        _ = command.Parameters.Add(new OracleParameter
        {
            ParameterName = "P_ROW_ID",
            OracleDbType = OracleDbType.Decimal,
            Direction = ParameterDirection.Input,
            Value = rowId
        });

        return await command.ExecuteNonQueryAsync();
    }

    /// <summary>
    /// Retrieves all active users for a specific branch.
    /// </summary>
    /// <param name="branchId">The unique identifier of the branch</param>
    /// <returns>A list of SysUser entities belonging to the specified branch</returns>
    public async Task<List<SysUser>> GetByBranchIdAsync(long branchId)
    {
        List<SysUser> users = new();

        using var connection = _dbContext.CreateConnection();
        await connection.OpenAsync();

        using var command = connection.CreateCommand();
        command.CommandType = CommandType.Text;
        command.CommandText = @"
            SELECT ROW_ID, ROW_DESC, ROW_DESC_E, USER_NAME, PASSWORD, PHONE, PHONE2,
                   ROLE, BRANCH_ID, EMAIL, LAST_LOGIN_DATE, IS_ACTIVE, IS_ADMIN,
                   CREATION_USER, CREATION_DATE, UPDATE_USER, UPDATE_DATE
            FROM SYS_USERS
            WHERE BRANCH_ID = :branchId AND IS_ACTIVE = '1'
            ORDER BY USER_NAME";

        _ = command.Parameters.Add(new OracleParameter
        {
            ParameterName = "branchId",
            OracleDbType = OracleDbType.Decimal,
            Direction = ParameterDirection.Input,
            Value = branchId
        });

        using var reader = await command.ExecuteReaderAsync();
        while (await reader.ReadAsync())
        {
            users.Add(MapToEntity(reader));
        }

        return users;
    }

    /// <summary>
    /// Retrieves all active users for a specific company (through branches).
    /// </summary>
    /// <param name="companyId">The unique identifier of the company</param>
    /// <returns>A list of SysUser entities belonging to branches of the specified company</returns>
    public async Task<List<SysUser>> GetByCompanyIdAsync(long companyId)
    {
        List<SysUser> users = new();

        using var connection = _dbContext.CreateConnection();
        await connection.OpenAsync();

        using var command = connection.CreateCommand();
        command.CommandType = CommandType.Text;
        command.CommandText = @"
            SELECT u.ROW_ID, u.ROW_DESC, u.ROW_DESC_E, u.USER_NAME, u.PASSWORD, u.PHONE, u.PHONE2,
                   u.ROLE, u.BRANCH_ID, u.EMAIL, u.LAST_LOGIN_DATE, u.IS_ACTIVE, u.IS_ADMIN,
                   u.CREATION_USER, u.CREATION_DATE, u.UPDATE_USER, u.UPDATE_DATE
            FROM SYS_USERS u
            INNER JOIN SYS_BRANCH b ON u.BRANCH_ID = b.ROW_ID
            WHERE b.PAR_ROW_ID = :companyId AND u.IS_ACTIVE = '1' AND b.IS_ACTIVE = '1'
            ORDER BY u.USER_NAME";

        _ = command.Parameters.Add(new OracleParameter
        {
            ParameterName = "companyId",
            OracleDbType = OracleDbType.Decimal,
            Direction = ParameterDirection.Input,
            Value = companyId
        });

        using var reader = await command.ExecuteReaderAsync();
        while (await reader.ReadAsync())
        {
            users.Add(MapToEntity(reader));
        }

        return users;
    }

    /// <summary>
    /// Maps an OracleDataReader row to a SysUser entity.
    /// Handles Oracle data type conversions to C# types.
    /// </summary>
    /// <param name="reader">The OracleDataReader positioned at a row</param>
    /// <returns>A SysUser entity populated with data from the reader</returns>
    private SysUser MapToEntity(OracleDataReader reader)
    {
        return new SysUser
        {
            RowId = reader.GetInt64(reader.GetOrdinal("ROW_ID")),
            RowDesc = reader.GetString(reader.GetOrdinal("ROW_DESC")),
            RowDescE = reader.GetString(reader.GetOrdinal("ROW_DESC_E")),
            UserName = reader.GetString(reader.GetOrdinal("USER_NAME")),
            Password = reader.GetString(reader.GetOrdinal("PASSWORD")),
            Phone = reader.IsDBNull(reader.GetOrdinal("PHONE")) ? null : reader.GetString(reader.GetOrdinal("PHONE")),
            Phone2 = reader.IsDBNull(reader.GetOrdinal("PHONE2")) ? null : reader.GetString(reader.GetOrdinal("PHONE2")),
            Role = reader.IsDBNull(reader.GetOrdinal("ROLE")) ? null : reader.GetInt64(reader.GetOrdinal("ROLE")),
            BranchId = reader.IsDBNull(reader.GetOrdinal("BRANCH_ID")) ? null : reader.GetInt64(reader.GetOrdinal("BRANCH_ID")),
            Email = reader.IsDBNull(reader.GetOrdinal("EMAIL")) ? null : reader.GetString(reader.GetOrdinal("EMAIL")),
            LastLoginDate = reader.IsDBNull(reader.GetOrdinal("LAST_LOGIN_DATE")) ? null : reader.GetDateTime(reader.GetOrdinal("LAST_LOGIN_DATE")),
            IsActive = MapIsActiveToBoolean(reader.GetString(reader.GetOrdinal("IS_ACTIVE"))),
            IsAdmin = MapIsAdminToBoolean(reader.GetString(reader.GetOrdinal("IS_ADMIN"))),
            CreationUser = reader.GetString(reader.GetOrdinal("CREATION_USER")),
            CreationDate = reader.IsDBNull(reader.GetOrdinal("CREATION_DATE")) ? null : reader.GetDateTime(reader.GetOrdinal("CREATION_DATE")),
            UpdateUser = reader.IsDBNull(reader.GetOrdinal("UPDATE_USER")) ? null : reader.GetString(reader.GetOrdinal("UPDATE_USER")),
            UpdateDate = reader.IsDBNull(reader.GetOrdinal("UPDATE_DATE")) ? null : reader.GetDateTime(reader.GetOrdinal("UPDATE_DATE"))
        };
    }

    /// <summary>
    /// Maps Oracle IS_ACTIVE values to C# boolean.
    /// Converts '1' to true, '0' to false.
    /// </summary>
    /// <param name="value">The Oracle IS_ACTIVE value</param>
    /// <returns>True if value is '1', false otherwise</returns>
    private bool MapIsActiveToBoolean(string value)
    {
        return value == "1";
    }

    /// <summary>
    /// Maps Oracle IS_ADMIN values to C# boolean.
    /// Converts '1' to true, '0' to false.
    /// </summary>
    /// <param name="value">The Oracle IS_ADMIN value</param>
    /// <returns>True if value is '1', false otherwise</returns>
    private bool MapIsAdminToBoolean(string value)
    {
        return value == "1";
    }

    /// <summary>
    /// Forces logout of a user by setting FORCE_LOGOUT_DATE and clearing refresh tokens.
    /// Calls SP_SYS_USERS_FORCE_LOGOUT stored procedure.
    /// </summary>
    /// <param name="userId">The unique identifier of the user to force logout</param>
    /// <param name="adminUser">The username of the admin performing the force logout</param>
    /// <returns>The number of rows affected</returns>
    public async Task<int> ForceLogoutAsync(long userId, string adminUser)
    {
        using var connection = _dbContext.CreateConnection();
        await connection.OpenAsync();

        using var command = connection.CreateCommand();
        command.CommandType = CommandType.StoredProcedure;
        command.CommandText = "SP_SYS_USERS_FORCE_LOGOUT";

        // Add input parameter for user ID
        _ = command.Parameters.Add(new OracleParameter
        {
            ParameterName = "P_USER_ID",
            OracleDbType = OracleDbType.Decimal,
            Direction = ParameterDirection.Input,
            Value = userId
        });

        // Add input parameter for admin user
        _ = command.Parameters.Add(new OracleParameter
        {
            ParameterName = "P_ADMIN_USER",
            OracleDbType = OracleDbType.Varchar2,
            Direction = ParameterDirection.Input,
            Value = adminUser
        });

        // Add output parameter for rows affected
        OracleParameter rowsAffectedParam = new()
        {
            ParameterName = "P_ROWS_AFFECTED",
            OracleDbType = OracleDbType.Decimal,
            Direction = ParameterDirection.Output
        };
        _ = command.Parameters.Add(rowsAffectedParam);

        await command.ExecuteNonQueryAsync();

        return Convert.ToInt32(rowsAffectedParam.Value.ToString());
    }

    /// <summary>
    /// Changes the password for a user.
    /// Calls SP_SYS_USERS_CHANGE_PASSWORD stored procedure.
    /// </summary>
    /// <param name="userId">The unique identifier of the user</param>
    /// <param name="newPasswordHash">The new SHA-256 hashed password</param>
    /// <param name="updateUser">The username of the user performing the change</param>
    /// <returns>The number of rows affected</returns>
    public async Task<int> ChangePasswordAsync(long userId, string newPasswordHash, string updateUser)
    {
        using var connection = _dbContext.CreateConnection();
        await connection.OpenAsync();

        using var command = connection.CreateCommand();
        command.CommandType = CommandType.StoredProcedure;
        command.CommandText = "SP_SYS_USERS_CHANGE_PASSWORD";

        // Add input parameter for user ID
        _ = command.Parameters.Add(new OracleParameter
        {
            ParameterName = "P_ROW_ID",
            OracleDbType = OracleDbType.Decimal,
            Direction = ParameterDirection.Input,
            Value = userId
        });

        // Add input parameter for new password
        _ = command.Parameters.Add(new OracleParameter
        {
            ParameterName = "P_NEW_PASSWORD",
            OracleDbType = OracleDbType.NVarchar2,
            Direction = ParameterDirection.Input,
            Value = newPasswordHash
        });

        // Add input parameter for update user
        _ = command.Parameters.Add(new OracleParameter
        {
            ParameterName = "P_UPDATE_USER",
            OracleDbType = OracleDbType.NVarchar2,
            Direction = ParameterDirection.Input,
            Value = updateUser
        });

        // Add output parameter for rows affected
        OracleParameter rowsAffectedParam = new()
        {
            ParameterName = "P_ROWS_AFFECTED",
            OracleDbType = OracleDbType.Decimal,
            Direction = ParameterDirection.Output
        };
        _ = command.Parameters.Add(rowsAffectedParam);

        await command.ExecuteNonQueryAsync();

        return Convert.ToInt32(rowsAffectedParam.Value.ToString());
    }
}
