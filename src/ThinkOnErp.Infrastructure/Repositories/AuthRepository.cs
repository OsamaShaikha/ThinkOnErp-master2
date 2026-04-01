using Oracle.ManagedDataAccess.Client;
using System.Data;
using ThinkOnErp.Domain.Entities;
using ThinkOnErp.Domain.Interfaces;
using ThinkOnErp.Infrastructure.Data;

namespace ThinkOnErp.Infrastructure.Repositories;

/// <summary>
/// Repository implementation for authentication operations using ADO.NET with Oracle stored procedures.
/// Implements IAuthRepository interface from the Domain layer.
/// Uses OracleDbContext to create connections and maps Oracle data types to C# types.
/// </summary>
public class AuthRepository : IAuthRepository
{
    private readonly OracleDbContext _dbContext;

    /// <summary>
    /// Initializes a new instance of the AuthRepository class.
    /// </summary>
    /// <param name="dbContext">The Oracle database context for connection management.</param>
    /// <exception cref="ArgumentNullException">Thrown when dbContext is null.</exception>
    public AuthRepository(OracleDbContext dbContext)
    {
        _dbContext = dbContext ?? throw new ArgumentNullException(nameof(dbContext));
    }

    /// <summary>
    /// Authenticates a user by username and password hash.
    /// Calls SP_SYS_USERS_LOGIN stored procedure.
    /// </summary>
    /// <param name="userName">The username to authenticate</param>
    /// <param name="passwordHash">The SHA-256 hashed password as hexadecimal string</param>
    /// <returns>The SysUser entity if credentials are valid and user is active, null otherwise</returns>
    public async Task<SysUser?> AuthenticateAsync(string userName, string passwordHash)
    {
        using (var connection = _dbContext.CreateConnection())
        {
            await connection.OpenAsync();

            using (var command = connection.CreateCommand())
            {
                command.CommandType = CommandType.StoredProcedure;
                command.CommandText = "SP_SYS_USERS_LOGIN";

                // Add input parameter for username
                command.Parameters.Add(new OracleParameter
                {
                    ParameterName = "P_USER_NAME",
                    OracleDbType = OracleDbType.Varchar2,
                    Direction = ParameterDirection.Input,
                    Value = userName
                });

                // Add input parameter for password hash
                command.Parameters.Add(new OracleParameter
                {
                    ParameterName = "P_PASSWORD",
                    OracleDbType = OracleDbType.Varchar2,
                    Direction = ParameterDirection.Input,
                    Value = passwordHash
                });

                // Add output parameter for SYS_REFCURSOR
                var cursorParam = new OracleParameter
                {
                    ParameterName = "P_RESULT_CURSOR",
                    OracleDbType = OracleDbType.RefCursor,
                    Direction = ParameterDirection.Output
                };
                command.Parameters.Add(cursorParam);

                using (var reader = await command.ExecuteReaderAsync())
                {
                    if (await reader.ReadAsync())
                    {
                        return MapToEntity(reader);
                    }
                }
            }
        }

        return null;
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
            RowId = reader.GetDecimal(reader.GetOrdinal("ROW_ID")),
            RowDesc = reader.GetString(reader.GetOrdinal("ROW_DESC")),
            RowDescE = reader.GetString(reader.GetOrdinal("ROW_DESC_E")),
            UserName = reader.GetString(reader.GetOrdinal("USER_NAME")),
            Password = reader.GetString(reader.GetOrdinal("PASSWORD")),
            Phone = reader.IsDBNull(reader.GetOrdinal("PHONE")) ? null : reader.GetString(reader.GetOrdinal("PHONE")),
            Phone2 = reader.IsDBNull(reader.GetOrdinal("PHONE2")) ? null : reader.GetString(reader.GetOrdinal("PHONE2")),
            Role = reader.IsDBNull(reader.GetOrdinal("ROLE")) ? null : reader.GetDecimal(reader.GetOrdinal("ROLE")),
            BranchId = reader.IsDBNull(reader.GetOrdinal("BRANCH_ID")) ? null : reader.GetDecimal(reader.GetOrdinal("BRANCH_ID")),
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
}
