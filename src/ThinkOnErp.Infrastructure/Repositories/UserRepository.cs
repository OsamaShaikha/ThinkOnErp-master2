using Oracle.ManagedDataAccess.Client;
using Oracle.ManagedDataAccess.Types;
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
        var users = new List<SysUser>();

        using (var connection = _dbContext.CreateConnection())
        {
            await connection.OpenAsync();

            using (var command = connection.CreateCommand())
            {
                command.CommandType = CommandType.StoredProcedure;
                command.CommandText = "SP_SYS_USERS_SELECT_ALL";

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
                    while (await reader.ReadAsync())
                    {
                        users.Add(MapToEntity(reader));
                    }
                }
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
    public async Task<SysUser?> GetByIdAsync(decimal rowId)
    {
        using (var connection = _dbContext.CreateConnection())
        {
            await connection.OpenAsync();

            using (var command = connection.CreateCommand())
            {
                command.CommandType = CommandType.StoredProcedure;
                command.CommandText = "SP_SYS_USERS_SELECT_BY_ID";

                // Add input parameter for ROW_ID
                var idParam = new OracleParameter
                {
                    ParameterName = "P_ROW_ID",
                    OracleDbType = OracleDbType.Decimal,
                    Direction = ParameterDirection.Input,
                    Value = rowId
                };
                command.Parameters.Add(idParam);

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
    /// Creates a new user in the database.
    /// Calls SP_SYS_USERS_INSERT stored procedure.
    /// </summary>
    /// <param name="user">The user entity to create</param>
    /// <returns>The generated RowId from SEQ_SYS_USERS sequence</returns>
    public async Task<decimal> CreateAsync(SysUser user)
    {
        using (var connection = _dbContext.CreateConnection())
        {
            await connection.OpenAsync();

            using (var command = connection.CreateCommand())
            {
                command.CommandType = CommandType.StoredProcedure;
                command.CommandText = "SP_SYS_USERS_INSERT";

                // Add input parameters
                command.Parameters.Add(new OracleParameter
                {
                    ParameterName = "P_ROW_DESC",
                    OracleDbType = OracleDbType.Varchar2,
                    Direction = ParameterDirection.Input,
                    Value = user.RowDesc
                });

                command.Parameters.Add(new OracleParameter
                {
                    ParameterName = "P_ROW_DESC_E",
                    OracleDbType = OracleDbType.Varchar2,
                    Direction = ParameterDirection.Input,
                    Value = user.RowDescE
                });

                command.Parameters.Add(new OracleParameter
                {
                    ParameterName = "P_USER_NAME",
                    OracleDbType = OracleDbType.Varchar2,
                    Direction = ParameterDirection.Input,
                    Value = user.UserName
                });

                command.Parameters.Add(new OracleParameter
                {
                    ParameterName = "P_PASSWORD",
                    OracleDbType = OracleDbType.Varchar2,
                    Direction = ParameterDirection.Input,
                    Value = user.Password
                });

                command.Parameters.Add(new OracleParameter
                {
                    ParameterName = "P_PHONE",
                    OracleDbType = OracleDbType.Varchar2,
                    Direction = ParameterDirection.Input,
                    Value = (object?)user.Phone ?? DBNull.Value
                });

                command.Parameters.Add(new OracleParameter
                {
                    ParameterName = "P_PHONE2",
                    OracleDbType = OracleDbType.Varchar2,
                    Direction = ParameterDirection.Input,
                    Value = (object?)user.Phone2 ?? DBNull.Value
                });

                command.Parameters.Add(new OracleParameter
                {
                    ParameterName = "P_ROLE",
                    OracleDbType = OracleDbType.Decimal,
                    Direction = ParameterDirection.Input,
                    Value = (object?)user.Role ?? DBNull.Value
                });

                command.Parameters.Add(new OracleParameter
                {
                    ParameterName = "P_BRANCH_ID",
                    OracleDbType = OracleDbType.Decimal,
                    Direction = ParameterDirection.Input,
                    Value = (object?)user.BranchId ?? DBNull.Value
                });

                command.Parameters.Add(new OracleParameter
                {
                    ParameterName = "P_EMAIL",
                    OracleDbType = OracleDbType.Varchar2,
                    Direction = ParameterDirection.Input,
                    Value = (object?)user.Email ?? DBNull.Value
                });

                command.Parameters.Add(new OracleParameter
                {
                    ParameterName = "P_IS_ADMIN",
                    OracleDbType = OracleDbType.Char,
                    Direction = ParameterDirection.Input,
                    Value = user.IsAdmin ? "1" : "0"
                });

                command.Parameters.Add(new OracleParameter
                {
                    ParameterName = "P_CREATION_USER",
                    OracleDbType = OracleDbType.Varchar2,
                    Direction = ParameterDirection.Input,
                    Value = user.CreationUser
                });

                // Add output parameter for new ID
                var newIdParam = new OracleParameter
                {
                    ParameterName = "P_NEW_ID",
                    OracleDbType = OracleDbType.Decimal,
                    Direction = ParameterDirection.Output
                };
                command.Parameters.Add(newIdParam);

                await command.ExecuteNonQueryAsync();

                // Return the generated ID
                return Convert.ToDecimal(newIdParam.Value.ToString());
            }
        }
    }

    /// <summary>
    /// Updates an existing user in the database.
    /// Calls SP_SYS_USERS_UPDATE stored procedure.
    /// </summary>
    /// <param name="user">The user entity with updated values</param>
    /// <returns>The number of rows affected</returns>
    public async Task<int> UpdateAsync(SysUser user)
    {
        using (var connection = _dbContext.CreateConnection())
        {
            await connection.OpenAsync();

            using (var command = connection.CreateCommand())
            {
                command.CommandType = CommandType.StoredProcedure;
                command.CommandText = "SP_SYS_USERS_UPDATE";

                // Add input parameters
                command.Parameters.Add(new OracleParameter
                {
                    ParameterName = "P_ROW_ID",
                    OracleDbType = OracleDbType.Decimal,
                    Direction = ParameterDirection.Input,
                    Value = user.RowId
                });

                command.Parameters.Add(new OracleParameter
                {
                    ParameterName = "P_ROW_DESC",
                    OracleDbType = OracleDbType.Varchar2,
                    Direction = ParameterDirection.Input,
                    Value = user.RowDesc
                });

                command.Parameters.Add(new OracleParameter
                {
                    ParameterName = "P_ROW_DESC_E",
                    OracleDbType = OracleDbType.Varchar2,
                    Direction = ParameterDirection.Input,
                    Value = user.RowDescE
                });

                command.Parameters.Add(new OracleParameter
                {
                    ParameterName = "P_USER_NAME",
                    OracleDbType = OracleDbType.Varchar2,
                    Direction = ParameterDirection.Input,
                    Value = user.UserName
                });

                command.Parameters.Add(new OracleParameter
                {
                    ParameterName = "P_PASSWORD",
                    OracleDbType = OracleDbType.Varchar2,
                    Direction = ParameterDirection.Input,
                    Value = user.Password
                });

                command.Parameters.Add(new OracleParameter
                {
                    ParameterName = "P_PHONE",
                    OracleDbType = OracleDbType.Varchar2,
                    Direction = ParameterDirection.Input,
                    Value = (object?)user.Phone ?? DBNull.Value
                });

                command.Parameters.Add(new OracleParameter
                {
                    ParameterName = "P_PHONE2",
                    OracleDbType = OracleDbType.Varchar2,
                    Direction = ParameterDirection.Input,
                    Value = (object?)user.Phone2 ?? DBNull.Value
                });

                command.Parameters.Add(new OracleParameter
                {
                    ParameterName = "P_ROLE",
                    OracleDbType = OracleDbType.Decimal,
                    Direction = ParameterDirection.Input,
                    Value = (object?)user.Role ?? DBNull.Value
                });

                command.Parameters.Add(new OracleParameter
                {
                    ParameterName = "P_BRANCH_ID",
                    OracleDbType = OracleDbType.Decimal,
                    Direction = ParameterDirection.Input,
                    Value = (object?)user.BranchId ?? DBNull.Value
                });

                command.Parameters.Add(new OracleParameter
                {
                    ParameterName = "P_EMAIL",
                    OracleDbType = OracleDbType.Varchar2,
                    Direction = ParameterDirection.Input,
                    Value = (object?)user.Email ?? DBNull.Value
                });

                command.Parameters.Add(new OracleParameter
                {
                    ParameterName = "P_IS_ADMIN",
                    OracleDbType = OracleDbType.Char,
                    Direction = ParameterDirection.Input,
                    Value = user.IsAdmin ? "1" : "0"
                });

                command.Parameters.Add(new OracleParameter
                {
                    ParameterName = "P_UPDATE_USER",
                    OracleDbType = OracleDbType.Varchar2,
                    Direction = ParameterDirection.Input,
                    Value = user.UpdateUser ?? string.Empty
                });

                return await command.ExecuteNonQueryAsync();
            }
        }
    }

    /// <summary>
    /// Performs a soft delete on a user by setting IS_ACTIVE to false.
    /// Calls SP_SYS_USERS_DELETE stored procedure.
    /// </summary>
    /// <param name="rowId">The unique identifier of the user to delete</param>
    /// <returns>The number of rows affected</returns>
    public async Task<int> DeleteAsync(decimal rowId)
    {
        using (var connection = _dbContext.CreateConnection())
        {
            await connection.OpenAsync();

            using (var command = connection.CreateCommand())
            {
                command.CommandType = CommandType.StoredProcedure;
                command.CommandText = "SP_SYS_USERS_DELETE";

                // Add input parameter for ROW_ID
                command.Parameters.Add(new OracleParameter
                {
                    ParameterName = "P_ROW_ID",
                    OracleDbType = OracleDbType.Decimal,
                    Direction = ParameterDirection.Input,
                    Value = rowId
                });

                return await command.ExecuteNonQueryAsync();
            }
        }
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
