using Oracle.ManagedDataAccess.Client;
using System.Data;
using ThinkOnErp.Domain.Entities;
using ThinkOnErp.Domain.Interfaces;
using ThinkOnErp.Infrastructure.Data;

namespace ThinkOnErp.Infrastructure.Repositories;

/// <summary>
/// Repository implementation for permission operations using ADO.NET with Oracle stored procedures.
/// </summary>
public class PermissionRepository : IPermissionRepository
{
    private readonly OracleDbContext _dbContext;

    public PermissionRepository(OracleDbContext dbContext)
    {
        _dbContext = dbContext ?? throw new ArgumentNullException(nameof(dbContext));
    }

    // =====================================================
    // Permission Check
    // =====================================================

    public async Task<bool> CheckUserPermissionAsync(long userId, string screenCode, string action)
    {
        using OracleConnection connection = _dbContext.CreateConnection();
        await connection.OpenAsync();

        using OracleCommand command = connection.CreateCommand();
        command.CommandType = CommandType.Text;
        command.CommandText = "SELECT FN_CHECK_USER_PERMISSION(:userId, :screenCode, :action) FROM DUAL";

        command.Parameters.Add(new OracleParameter("userId", OracleDbType.Int64, userId, ParameterDirection.Input));
        command.Parameters.Add(new OracleParameter("screenCode", OracleDbType.Varchar2, screenCode, ParameterDirection.Input));
        command.Parameters.Add(new OracleParameter("action", OracleDbType.Varchar2, action.ToUpper(), ParameterDirection.Input));

        var result = await command.ExecuteScalarAsync();
        return result?.ToString() == "1";
    }

    // =====================================================
    // User Role Management
    // =====================================================

    public async Task<List<SysUserRole>> GetUserRolesAsync(long userId)
    {
        using OracleConnection connection = _dbContext.CreateConnection();
        await connection.OpenAsync();

        using OracleCommand command = connection.CreateCommand();
        command.CommandType = CommandType.StoredProcedure;
        command.CommandText = "SP_SYS_USER_ROLE_GET";

        command.Parameters.Add(new OracleParameter("P_USER_ID", OracleDbType.Int64, userId, ParameterDirection.Input));
        
        var cursorParam = new OracleParameter("P_RESULT_CURSOR", OracleDbType.RefCursor, ParameterDirection.Output);
        command.Parameters.Add(cursorParam);

        var userRoles = new List<SysUserRole>();

        using OracleDataReader reader = await command.ExecuteReaderAsync();
        while (await reader.ReadAsync())
        {
            userRoles.Add(new SysUserRole
            {
                RowId = reader.GetInt64(reader.GetOrdinal("ROW_ID")),
                UserId = reader.GetInt64(reader.GetOrdinal("USER_ID")),
                RoleId = reader.GetInt64(reader.GetOrdinal("ROLE_ID")),
                AssignedBy = reader.IsDBNull(reader.GetOrdinal("ASSIGNED_BY")) ? null : reader.GetInt64(reader.GetOrdinal("ASSIGNED_BY")),
                AssignedDate = reader.IsDBNull(reader.GetOrdinal("ASSIGNED_DATE")) ? null : reader.GetDateTime(reader.GetOrdinal("ASSIGNED_DATE")),
                CreationUser = reader.GetString(reader.GetOrdinal("CREATION_USER")),
                CreationDate = reader.IsDBNull(reader.GetOrdinal("CREATION_DATE")) ? null : reader.GetDateTime(reader.GetOrdinal("CREATION_DATE"))
            });
        }

        return userRoles;
    }

    public async Task AssignRoleToUserAsync(long userId, long roleId, long? assignedBy, string creationUser)
    {
        using OracleConnection connection = _dbContext.CreateConnection();
        await connection.OpenAsync();

        using OracleCommand command = connection.CreateCommand();
        command.CommandType = CommandType.StoredProcedure;
        command.CommandText = "SP_SYS_USER_ROLE_ASSIGN";

        command.Parameters.Add(new OracleParameter("P_USER_ID", OracleDbType.Int64, userId, ParameterDirection.Input));
        command.Parameters.Add(new OracleParameter("P_ROLE_ID", OracleDbType.Int64, roleId, ParameterDirection.Input));
        command.Parameters.Add(new OracleParameter("P_ASSIGNED_BY", OracleDbType.Int64, assignedBy ?? (object)DBNull.Value, ParameterDirection.Input));
        command.Parameters.Add(new OracleParameter("P_CREATION_USER", OracleDbType.Varchar2, creationUser, ParameterDirection.Input));

        await command.ExecuteNonQueryAsync();
    }

    public async Task RemoveRoleFromUserAsync(long userId, long roleId)
    {
        using OracleConnection connection = _dbContext.CreateConnection();
        await connection.OpenAsync();

        using OracleCommand command = connection.CreateCommand();
        command.CommandType = CommandType.StoredProcedure;
        command.CommandText = "SP_SYS_USER_ROLE_REMOVE";

        command.Parameters.Add(new OracleParameter("P_USER_ID", OracleDbType.Int64, userId, ParameterDirection.Input));
        command.Parameters.Add(new OracleParameter("P_ROLE_ID", OracleDbType.Int64, roleId, ParameterDirection.Input));

        await command.ExecuteNonQueryAsync();
    }

    // =====================================================
    // Role Screen Permissions
    // =====================================================

    public async Task<List<SysRoleScreenPermission>> GetRoleScreenPermissionsAsync(long roleId)
    {
        using OracleConnection connection = _dbContext.CreateConnection();
        await connection.OpenAsync();

        using OracleCommand command = connection.CreateCommand();
        command.CommandType = CommandType.StoredProcedure;
        command.CommandText = "SP_SYS_ROLE_SCREEN_PERM_GET";

        command.Parameters.Add(new OracleParameter("P_ROLE_ID", OracleDbType.Int64, roleId, ParameterDirection.Input));
        
        var cursorParam = new OracleParameter("P_RESULT_CURSOR", OracleDbType.RefCursor, ParameterDirection.Output);
        command.Parameters.Add(cursorParam);

        var permissions = new List<SysRoleScreenPermission>();

        using OracleDataReader reader = await command.ExecuteReaderAsync();
        while (await reader.ReadAsync())
        {
            permissions.Add(new SysRoleScreenPermission
            {
                RowId = reader.GetInt64(reader.GetOrdinal("ROW_ID")),
                RoleId = reader.GetInt64(reader.GetOrdinal("ROLE_ID")),
                ScreenId = reader.GetInt64(reader.GetOrdinal("SCREEN_ID")),
                CanView = reader.GetString(reader.GetOrdinal("CAN_VIEW")) == "1",
                CanInsert = reader.GetString(reader.GetOrdinal("CAN_INSERT")) == "1",
                CanUpdate = reader.GetString(reader.GetOrdinal("CAN_UPDATE")) == "1",
                CanDelete = reader.GetString(reader.GetOrdinal("CAN_DELETE")) == "1"
            });
        }

        return permissions;
    }

    public async Task SetRoleScreenPermissionAsync(long roleId, long screenId, bool canView, bool canInsert, bool canUpdate, bool canDelete, string creationUser)
    {
        using OracleConnection connection = _dbContext.CreateConnection();
        await connection.OpenAsync();

        using OracleCommand command = connection.CreateCommand();
        command.CommandType = CommandType.StoredProcedure;
        command.CommandText = "SP_SYS_ROLE_SCREEN_PERM_SET";

        command.Parameters.Add(new OracleParameter("P_ROLE_ID", OracleDbType.Int64, roleId, ParameterDirection.Input));
        command.Parameters.Add(new OracleParameter("P_SCREEN_ID", OracleDbType.Int64, screenId, ParameterDirection.Input));
        command.Parameters.Add(new OracleParameter("P_CAN_VIEW", OracleDbType.Char, canView ? "1" : "0", ParameterDirection.Input));
        command.Parameters.Add(new OracleParameter("P_CAN_INSERT", OracleDbType.Char, canInsert ? "1" : "0", ParameterDirection.Input));
        command.Parameters.Add(new OracleParameter("P_CAN_UPDATE", OracleDbType.Char, canUpdate ? "1" : "0", ParameterDirection.Input));
        command.Parameters.Add(new OracleParameter("P_CAN_DELETE", OracleDbType.Char, canDelete ? "1" : "0", ParameterDirection.Input));
        command.Parameters.Add(new OracleParameter("P_CREATION_USER", OracleDbType.Varchar2, creationUser, ParameterDirection.Input));

        await command.ExecuteNonQueryAsync();
    }

    public async Task DeleteRoleScreenPermissionAsync(long roleId, long screenId)
    {
        using OracleConnection connection = _dbContext.CreateConnection();
        await connection.OpenAsync();

        using OracleCommand command = connection.CreateCommand();
        command.CommandType = CommandType.StoredProcedure;
        command.CommandText = "SP_SYS_ROLE_SCREEN_PERM_DEL";

        command.Parameters.Add(new OracleParameter("P_ROLE_ID", OracleDbType.Int64, roleId, ParameterDirection.Input));
        command.Parameters.Add(new OracleParameter("P_SCREEN_ID", OracleDbType.Int64, screenId, ParameterDirection.Input));

        await command.ExecuteNonQueryAsync();
    }

    // =====================================================
    // User Screen Permission Overrides
    // =====================================================

    public async Task<List<SysUserScreenPermission>> GetUserScreenPermissionsAsync(long userId)
    {
        using OracleConnection connection = _dbContext.CreateConnection();
        await connection.OpenAsync();

        using OracleCommand command = connection.CreateCommand();
        command.CommandType = CommandType.StoredProcedure;
        command.CommandText = "SP_SYS_USER_SCREEN_PERM_GET";

        command.Parameters.Add(new OracleParameter("P_USER_ID", OracleDbType.Int64, userId, ParameterDirection.Input));
        
        var cursorParam = new OracleParameter("P_RESULT_CURSOR", OracleDbType.RefCursor, ParameterDirection.Output);
        command.Parameters.Add(cursorParam);

        var permissions = new List<SysUserScreenPermission>();

        using OracleDataReader reader = await command.ExecuteReaderAsync();
        while (await reader.ReadAsync())
        {
            permissions.Add(new SysUserScreenPermission
            {
                RowId = reader.GetInt64(reader.GetOrdinal("ROW_ID")),
                UserId = reader.GetInt64(reader.GetOrdinal("USER_ID")),
                ScreenId = reader.GetInt64(reader.GetOrdinal("SCREEN_ID")),
                CanView = reader.GetString(reader.GetOrdinal("CAN_VIEW")) == "1",
                CanInsert = reader.GetString(reader.GetOrdinal("CAN_INSERT")) == "1",
                CanUpdate = reader.GetString(reader.GetOrdinal("CAN_UPDATE")) == "1",
                CanDelete = reader.GetString(reader.GetOrdinal("CAN_DELETE")) == "1",
                AssignedBy = reader.IsDBNull(reader.GetOrdinal("ASSIGNED_BY")) ? null : reader.GetInt64(reader.GetOrdinal("ASSIGNED_BY")),
                AssignedDate = reader.IsDBNull(reader.GetOrdinal("ASSIGNED_DATE")) ? null : reader.GetDateTime(reader.GetOrdinal("ASSIGNED_DATE")),
                Notes = reader.IsDBNull(reader.GetOrdinal("NOTES")) ? null : reader.GetString(reader.GetOrdinal("NOTES"))
            });
        }

        return permissions;
    }

    public async Task SetUserScreenPermissionAsync(long userId, long screenId, bool canView, bool canInsert, bool canUpdate, bool canDelete, long? assignedBy, string? notes, string creationUser)
    {
        using OracleConnection connection = _dbContext.CreateConnection();
        await connection.OpenAsync();

        using OracleCommand command = connection.CreateCommand();
        command.CommandType = CommandType.StoredProcedure;
        command.CommandText = "SP_SYS_USER_SCREEN_PERM_SET";

        command.Parameters.Add(new OracleParameter("P_USER_ID", OracleDbType.Int64, userId, ParameterDirection.Input));
        command.Parameters.Add(new OracleParameter("P_SCREEN_ID", OracleDbType.Int64, screenId, ParameterDirection.Input));
        command.Parameters.Add(new OracleParameter("P_CAN_VIEW", OracleDbType.Char, canView ? "1" : "0", ParameterDirection.Input));
        command.Parameters.Add(new OracleParameter("P_CAN_INSERT", OracleDbType.Char, canInsert ? "1" : "0", ParameterDirection.Input));
        command.Parameters.Add(new OracleParameter("P_CAN_UPDATE", OracleDbType.Char, canUpdate ? "1" : "0", ParameterDirection.Input));
        command.Parameters.Add(new OracleParameter("P_CAN_DELETE", OracleDbType.Char, canDelete ? "1" : "0", ParameterDirection.Input));
        command.Parameters.Add(new OracleParameter("P_ASSIGNED_BY", OracleDbType.Int64, assignedBy ?? (object)DBNull.Value, ParameterDirection.Input));
        command.Parameters.Add(new OracleParameter("P_NOTES", OracleDbType.Varchar2, notes ?? (object)DBNull.Value, ParameterDirection.Input));
        command.Parameters.Add(new OracleParameter("P_CREATION_USER", OracleDbType.Varchar2, creationUser, ParameterDirection.Input));

        await command.ExecuteNonQueryAsync();
    }

    public async Task DeleteUserScreenPermissionAsync(long userId, long screenId)
    {
        using OracleConnection connection = _dbContext.CreateConnection();
        await connection.OpenAsync();

        using OracleCommand command = connection.CreateCommand();
        command.CommandType = CommandType.StoredProcedure;
        command.CommandText = "SP_SYS_USER_SCREEN_PERM_DEL";

        command.Parameters.Add(new OracleParameter("P_USER_ID", OracleDbType.Int64, userId, ParameterDirection.Input));
        command.Parameters.Add(new OracleParameter("P_SCREEN_ID", OracleDbType.Int64, screenId, ParameterDirection.Input));

        await command.ExecuteNonQueryAsync();
    }

    // =====================================================
    // Company System Assignments
    // =====================================================

    public async Task<List<SysCompanySystem>> GetCompanySystemsAsync(long companyId)
    {
        using OracleConnection connection = _dbContext.CreateConnection();
        await connection.OpenAsync();

        using OracleCommand command = connection.CreateCommand();
        command.CommandType = CommandType.StoredProcedure;
        command.CommandText = "SP_SYS_COMPANY_SYSTEM_GET";

        command.Parameters.Add(new OracleParameter("P_COMPANY_ID", OracleDbType.Int64, companyId, ParameterDirection.Input));
        
        var cursorParam = new OracleParameter("P_RESULT_CURSOR", OracleDbType.RefCursor, ParameterDirection.Output);
        command.Parameters.Add(cursorParam);

        var companySystems = new List<SysCompanySystem>();

        using OracleDataReader reader = await command.ExecuteReaderAsync();
        while (await reader.ReadAsync())
        {
            companySystems.Add(new SysCompanySystem
            {
                RowId = reader.GetInt64(reader.GetOrdinal("ROW_ID")),
                CompanyId = reader.GetInt64(reader.GetOrdinal("COMPANY_ID")),
                SystemId = reader.GetInt64(reader.GetOrdinal("SYSTEM_ID")),
                IsAllowed = reader.GetString(reader.GetOrdinal("IS_ALLOWED")) == "1",
                GrantedBy = reader.IsDBNull(reader.GetOrdinal("GRANTED_BY")) ? null : reader.GetInt64(reader.GetOrdinal("GRANTED_BY")),
                GrantedDate = reader.IsDBNull(reader.GetOrdinal("GRANTED_DATE")) ? null : reader.GetDateTime(reader.GetOrdinal("GRANTED_DATE")),
                RevokedDate = reader.IsDBNull(reader.GetOrdinal("REVOKED_DATE")) ? null : reader.GetDateTime(reader.GetOrdinal("REVOKED_DATE")),
                Notes = reader.IsDBNull(reader.GetOrdinal("NOTES")) ? null : reader.GetString(reader.GetOrdinal("NOTES"))
            });
        }

        return companySystems;
    }

    public async Task SetCompanySystemAsync(long companyId, long systemId, bool isAllowed, long? grantedBy, string? notes, string creationUser)
    {
        using OracleConnection connection = _dbContext.CreateConnection();
        await connection.OpenAsync();

        using OracleCommand command = connection.CreateCommand();
        command.CommandType = CommandType.StoredProcedure;
        command.CommandText = "SP_SYS_COMPANY_SYSTEM_SET";

        command.Parameters.Add(new OracleParameter("P_COMPANY_ID", OracleDbType.Int64, companyId, ParameterDirection.Input));
        command.Parameters.Add(new OracleParameter("P_SYSTEM_ID", OracleDbType.Int64, systemId, ParameterDirection.Input));
        command.Parameters.Add(new OracleParameter("P_IS_ALLOWED", OracleDbType.Char, isAllowed ? "1" : "0", ParameterDirection.Input));
        command.Parameters.Add(new OracleParameter("P_GRANTED_BY", OracleDbType.Int64, grantedBy ?? (object)DBNull.Value, ParameterDirection.Input));
        command.Parameters.Add(new OracleParameter("P_NOTES", OracleDbType.Varchar2, notes ?? (object)DBNull.Value, ParameterDirection.Input));
        command.Parameters.Add(new OracleParameter("P_CREATION_USER", OracleDbType.Varchar2, creationUser, ParameterDirection.Input));

        await command.ExecuteNonQueryAsync();
    }
}
