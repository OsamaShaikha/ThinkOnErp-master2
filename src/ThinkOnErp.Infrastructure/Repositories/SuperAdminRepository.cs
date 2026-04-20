using Oracle.ManagedDataAccess.Client;
using System.Data;
using ThinkOnErp.Domain.Entities;
using ThinkOnErp.Domain.Interfaces;
using ThinkOnErp.Infrastructure.Data;

namespace ThinkOnErp.Infrastructure.Repositories;

public class SuperAdminRepository : ISuperAdminRepository
{
    private readonly OracleDbContext _context;

    public SuperAdminRepository(OracleDbContext context)
    {
        _context = context;
    }

    public async Task<List<SysSuperAdmin>> GetAllAsync()
    {
        var superAdmins = new List<SysSuperAdmin>();
        
        using var connection = _context.CreateConnection();
        using var command = new OracleCommand("SP_SYS_SUPER_ADMIN_SELECT_ALL", connection)
        {
            CommandType = CommandType.StoredProcedure
        };

        var cursorParam = new OracleParameter("p_cursor", OracleDbType.RefCursor, ParameterDirection.Output);
        command.Parameters.Add(cursorParam);

        await connection.OpenAsync();
        using var reader = await command.ExecuteReaderAsync();
        
        while (await reader.ReadAsync())
        {
            superAdmins.Add(MapFromReader(reader));
        }

        return superAdmins;
    }

    public async Task<SysSuperAdmin?> GetByIdAsync(Int64 id)
    {
        using var connection = _context.CreateConnection();
        using var command = new OracleCommand("SP_SYS_SUPER_ADMIN_SELECT_BY_ID", connection)
        {
            CommandType = CommandType.StoredProcedure
        };

        command.Parameters.Add("p_row_id", OracleDbType.Int64).Value = id;
        var cursorParam = new OracleParameter("p_cursor", OracleDbType.RefCursor, ParameterDirection.Output);
        command.Parameters.Add(cursorParam);

        await connection.OpenAsync();
        using var reader = await command.ExecuteReaderAsync();
        
        if (await reader.ReadAsync())
        {
            return MapFromReader(reader);
        }

        return null;
    }

    public async Task<SysSuperAdmin?> GetByUsernameAsync(string username)
    {
        using var connection = _context.CreateConnection();
        using var command = new OracleCommand("SP_SYS_SUPER_ADMIN_SELECT_BY_USERNAME", connection)
        {
            CommandType = CommandType.StoredProcedure
        };

        command.Parameters.Add("p_username", OracleDbType.NVarchar2).Value = username;
        var cursorParam = new OracleParameter("p_cursor", OracleDbType.RefCursor, ParameterDirection.Output);
        command.Parameters.Add(cursorParam);

        await connection.OpenAsync();
        using var reader = await command.ExecuteReaderAsync();
        
        if (await reader.ReadAsync())
        {
            return MapFromReader(reader);
        }

        return null;
    }

    public async Task<SysSuperAdmin?> GetByEmailAsync(string email)
    {
        using var connection = _context.CreateConnection();
        using var command = new OracleCommand("SP_SYS_SUPER_ADMIN_SELECT_BY_EMAIL", connection)
        {
            CommandType = CommandType.StoredProcedure
        };

        command.Parameters.Add("p_email", OracleDbType.NVarchar2).Value = email;
        var cursorParam = new OracleParameter("p_cursor", OracleDbType.RefCursor, ParameterDirection.Output);
        command.Parameters.Add(cursorParam);

        await connection.OpenAsync();
        using var reader = await command.ExecuteReaderAsync();
        
        if (await reader.ReadAsync())
        {
            return MapFromReader(reader);
        }

        return null;
    }

    public async Task<Int64> CreateAsync(SysSuperAdmin superAdmin)
    {
        using var connection = _context.CreateConnection();
        using var command = new OracleCommand("SP_SYS_SUPER_ADMIN_INSERT", connection)
        {
            CommandType = CommandType.StoredProcedure
        };

        command.Parameters.Add("p_row_desc", OracleDbType.NVarchar2).Value = superAdmin.RowDesc;
        command.Parameters.Add("p_row_desc_e", OracleDbType.NVarchar2).Value = superAdmin.RowDescE;
        command.Parameters.Add("p_user_name", OracleDbType.NVarchar2).Value = superAdmin.UserName;
        command.Parameters.Add("p_password", OracleDbType.NVarchar2).Value = superAdmin.Password;
        command.Parameters.Add("p_email", OracleDbType.NVarchar2).Value = (object?)superAdmin.Email ?? DBNull.Value;
        command.Parameters.Add("p_phone", OracleDbType.NVarchar2).Value = (object?)superAdmin.Phone ?? DBNull.Value;
        command.Parameters.Add("p_creation_user", OracleDbType.NVarchar2).Value = superAdmin.CreationUser;
        
        var newIdParam = new OracleParameter("p_new_id", OracleDbType.Int64, ParameterDirection.Output);
        command.Parameters.Add(newIdParam);

        await connection.OpenAsync();
        await command.ExecuteNonQueryAsync();

        return Convert.ToInt64(newIdParam.Value.ToString());
    }

    public async Task<Int64> UpdateAsync(SysSuperAdmin superAdmin)
    {
        using var connection = _context.CreateConnection();
        using var command = new OracleCommand("SP_SYS_SUPER_ADMIN_UPDATE", connection)
        {
            CommandType = CommandType.StoredProcedure
        };

        command.Parameters.Add("p_row_id", OracleDbType.Int64).Value = superAdmin.RowId;
        command.Parameters.Add("p_row_desc", OracleDbType.NVarchar2).Value = superAdmin.RowDesc;
        command.Parameters.Add("p_row_desc_e", OracleDbType.NVarchar2).Value = superAdmin.RowDescE;
        command.Parameters.Add("p_email", OracleDbType.NVarchar2).Value = (object?)superAdmin.Email ?? DBNull.Value;
        command.Parameters.Add("p_phone", OracleDbType.NVarchar2).Value = (object?)superAdmin.Phone ?? DBNull.Value;
        command.Parameters.Add("p_update_user", OracleDbType.NVarchar2).Value = superAdmin.UpdateUser;
        
        var rowsAffectedParam = new OracleParameter("p_rows_affected", OracleDbType.Int64, ParameterDirection.Output);
        command.Parameters.Add(rowsAffectedParam);

        await connection.OpenAsync();
        await command.ExecuteNonQueryAsync();

        return Convert.ToInt64(rowsAffectedParam.Value.ToString());
    }

    public async Task<Int64> DeleteAsync(Int64 id)
    {
        using var connection = _context.CreateConnection();
        using var command = new OracleCommand("SP_SYS_SUPER_ADMIN_DELETE", connection)
        {
            CommandType = CommandType.StoredProcedure
        };

        command.Parameters.Add("p_row_id", OracleDbType.Int64).Value = id;
        
        var rowsAffectedParam = new OracleParameter("p_rows_affected", OracleDbType.Int64, ParameterDirection.Output);
        command.Parameters.Add(rowsAffectedParam);

        await connection.OpenAsync();
        await command.ExecuteNonQueryAsync();

        return Convert.ToInt64(rowsAffectedParam.Value.ToString());
    }

    public async Task<Int64> ChangePasswordAsync(Int64 id, string newPasswordHash, string updateUser)
    {
        using var connection = _context.CreateConnection();
        using var command = new OracleCommand("SP_SYS_SUPER_ADMIN_CHANGE_PASSWORD", connection)
        {
            CommandType = CommandType.StoredProcedure
        };

        command.Parameters.Add("p_row_id", OracleDbType.Int64).Value = id;
        command.Parameters.Add("p_new_password", OracleDbType.NVarchar2).Value = newPasswordHash;
        command.Parameters.Add("p_update_user", OracleDbType.NVarchar2).Value = updateUser;
        
        var rowsAffectedParam = new OracleParameter("p_rows_affected", OracleDbType.Int64, ParameterDirection.Output);
        command.Parameters.Add(rowsAffectedParam);

        await connection.OpenAsync();
        await command.ExecuteNonQueryAsync();

        return Convert.ToInt64(rowsAffectedParam.Value.ToString());
    }

    public async Task<Int64> Enable2FAAsync(Int64 id, string twoFaSecret, string updateUser)
    {
        using var connection = _context.CreateConnection();
        using var command = new OracleCommand("SP_SYS_SUPER_ADMIN_ENABLE_2FA", connection)
        {
            CommandType = CommandType.StoredProcedure
        };

        command.Parameters.Add("p_row_id", OracleDbType.Int64).Value = id;
        command.Parameters.Add("p_two_fa_secret", OracleDbType.NVarchar2).Value = twoFaSecret;
        command.Parameters.Add("p_update_user", OracleDbType.NVarchar2).Value = updateUser;
        
        var rowsAffectedParam = new OracleParameter("p_rows_affected", OracleDbType.Int64, ParameterDirection.Output);
        command.Parameters.Add(rowsAffectedParam);

        await connection.OpenAsync();
        await command.ExecuteNonQueryAsync();

        return Convert.ToInt64(rowsAffectedParam.Value.ToString());
    }

    public async Task<Int64> Disable2FAAsync(Int64 id, string updateUser)
    {
        using var connection = _context.CreateConnection();
        using var command = new OracleCommand("SP_SYS_SUPER_ADMIN_DISABLE_2FA", connection)
        {
            CommandType = CommandType.StoredProcedure
        };

        command.Parameters.Add("p_row_id", OracleDbType.Int64).Value = id;
        command.Parameters.Add("p_update_user", OracleDbType.NVarchar2).Value = updateUser;
        
        var rowsAffectedParam = new OracleParameter("p_rows_affected", OracleDbType.Int64, ParameterDirection.Output);
        command.Parameters.Add(rowsAffectedParam);

        await connection.OpenAsync();
        await command.ExecuteNonQueryAsync();

        return Convert.ToInt64(rowsAffectedParam.Value.ToString());
    }

    public async Task<Int64> UpdateLastLoginAsync(Int64 id)
    {
        using var connection = _context.CreateConnection();
        using var command = new OracleCommand("SP_SYS_SUPER_ADMIN_UPDATE_LAST_LOGIN", connection)
        {
            CommandType = CommandType.StoredProcedure
        };

        command.Parameters.Add("p_row_id", OracleDbType.Int64).Value = id;
        
        var rowsAffectedParam = new OracleParameter("p_rows_affected", OracleDbType.Int64, ParameterDirection.Output);
        command.Parameters.Add(rowsAffectedParam);

        await connection.OpenAsync();
        await command.ExecuteNonQueryAsync();

        return Convert.ToInt64(rowsAffectedParam.Value.ToString());
    }

    public async Task<SysSuperAdmin?> AuthenticateAsync(string userName, string passwordHash)
    {
        using var connection = _context.CreateConnection();
        using var command = new OracleCommand("SP_SYS_SUPER_ADMIN_LOGIN", connection)
        {
            CommandType = CommandType.StoredProcedure
        };

        command.Parameters.Add("P_USER_NAME", OracleDbType.NVarchar2).Value = userName;
        command.Parameters.Add("P_PASSWORD", OracleDbType.NVarchar2).Value = passwordHash;
        
        var cursorParam = new OracleParameter("P_RESULT_CURSOR", OracleDbType.RefCursor, ParameterDirection.Output);
        command.Parameters.Add(cursorParam);

        await connection.OpenAsync();
        using var reader = await command.ExecuteReaderAsync();
        
        if (await reader.ReadAsync())
        {
            // Update last login date
            await UpdateLastLoginAsync(reader.GetInt64(reader.GetOrdinal("ROW_ID")));
            
            return MapFromReader(reader);
        }

        return null;
    }

    public async Task SaveRefreshTokenAsync(long superAdminId, string refreshToken, DateTime expiryDate)
    {
        using var connection = _context.CreateConnection();
        using var command = new OracleCommand
        {
            Connection = connection,
            CommandType = CommandType.Text,
            CommandText = @"
                UPDATE SYS_SUPER_ADMIN 
                SET REFRESH_TOKEN = :refreshToken, 
                    REFRESH_TOKEN_EXPIRY = :expiryDate,
                    UPDATE_DATE = SYSDATE
                WHERE ROW_ID = :superAdminId"
        };

        command.Parameters.Add("refreshToken", OracleDbType.NVarchar2).Value = refreshToken;
        command.Parameters.Add("expiryDate", OracleDbType.Date).Value = expiryDate;
        command.Parameters.Add("superAdminId", OracleDbType.Int64).Value = superAdminId;

        await connection.OpenAsync();
        await command.ExecuteNonQueryAsync();
    }

    public async Task<SysSuperAdmin?> ValidateRefreshTokenAsync(string refreshToken)
    {
        using var connection = _context.CreateConnection();
        using var command = new OracleCommand
        {
            Connection = connection,
            CommandType = CommandType.Text,
            CommandText = @"
                SELECT 
                    ROW_ID,
                    ROW_DESC,
                    ROW_DESC_E,
                    USER_NAME,
                    PASSWORD,
                    EMAIL,
                    PHONE,
                    TWO_FA_SECRET,
                    CASE WHEN TWO_FA_ENABLED = '1' THEN 1 ELSE 0 END AS TWO_FA_ENABLED,
                    CASE WHEN IS_ACTIVE = '1' THEN 1 ELSE 0 END AS IS_ACTIVE,
                    LAST_LOGIN_DATE,
                    CREATION_USER,
                    CREATION_DATE,
                    UPDATE_USER,
                    UPDATE_DATE
                FROM SYS_SUPER_ADMIN 
                WHERE REFRESH_TOKEN = :refreshToken 
                  AND REFRESH_TOKEN_EXPIRY > SYSDATE
                  AND IS_ACTIVE = '1'"
        };

        command.Parameters.Add("refreshToken", OracleDbType.NVarchar2).Value = refreshToken;

        await connection.OpenAsync();
        using var reader = await command.ExecuteReaderAsync();
        
        if (await reader.ReadAsync())
        {
            return MapFromReader(reader);
        }

        return null;
    }

    private static SysSuperAdmin MapFromReader(IDataReader reader)
    {
        return new SysSuperAdmin
        {
            RowId = reader.GetInt64(reader.GetOrdinal("ROW_ID")),
            RowDesc = reader.GetString(reader.GetOrdinal("ROW_DESC")),
            RowDescE = reader.GetString(reader.GetOrdinal("ROW_DESC_E")),
            UserName = reader.GetString(reader.GetOrdinal("USER_NAME")),
            Password = reader.GetString(reader.GetOrdinal("PASSWORD")),
            Email = reader.IsDBNull(reader.GetOrdinal("EMAIL")) ? null : reader.GetString(reader.GetOrdinal("EMAIL")),
            Phone = reader.IsDBNull(reader.GetOrdinal("PHONE")) ? null : reader.GetString(reader.GetOrdinal("PHONE")),
            TwoFaSecret = reader.IsDBNull(reader.GetOrdinal("TWO_FA_SECRET")) ? null : reader.GetString(reader.GetOrdinal("TWO_FA_SECRET")),
            TwoFaEnabled = reader.GetInt32(reader.GetOrdinal("TWO_FA_ENABLED")) == 1,
            IsActive = reader.GetInt32(reader.GetOrdinal("IS_ACTIVE")) == 1,
            LastLoginDate = reader.IsDBNull(reader.GetOrdinal("LAST_LOGIN_DATE")) ? null : reader.GetDateTime(reader.GetOrdinal("LAST_LOGIN_DATE")),
            CreationUser = reader.GetString(reader.GetOrdinal("CREATION_USER")),
            CreationDate = reader.IsDBNull(reader.GetOrdinal("CREATION_DATE")) ? null : reader.GetDateTime(reader.GetOrdinal("CREATION_DATE")),
            UpdateUser = reader.IsDBNull(reader.GetOrdinal("UPDATE_USER")) ? null : reader.GetString(reader.GetOrdinal("UPDATE_USER")),
            UpdateDate = reader.IsDBNull(reader.GetOrdinal("UPDATE_DATE")) ? null : reader.GetDateTime(reader.GetOrdinal("UPDATE_DATE"))
        };
    }
}
