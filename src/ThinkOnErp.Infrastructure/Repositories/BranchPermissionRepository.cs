using Oracle.ManagedDataAccess.Client;
using System.Data;
using ThinkOnErp.Domain.Entities;
using ThinkOnErp.Domain.Interfaces;
using ThinkOnErp.Infrastructure.Data;

namespace ThinkOnErp.Infrastructure.Repositories;

/// <summary>
/// Repository implementation for branch-level permissions using ADO.NET with Oracle stored procedures.
/// Manages SYS_BRANCH_SYSTEM and SYS_BRANCH_SCREEN tables.
/// </summary>
public class BranchPermissionRepository : IBranchPermissionRepository
{
    private readonly OracleDbContext _dbContext;

    public BranchPermissionRepository(OracleDbContext dbContext)
    {
        _dbContext = dbContext ?? throw new ArgumentNullException(nameof(dbContext));
    }

    #region Branch System Permissions

    public async Task<List<SysBranchSystem>> GetBranchSystemsAsync(long branchId)
    {
        List<SysBranchSystem> branchSystems = new();

        using (var connection = _dbContext.CreateConnection())
        {
            await connection.OpenAsync();

            using var command = connection.CreateCommand();
            command.CommandType = CommandType.StoredProcedure;
            command.CommandText = "SP_SYS_BRANCH_SYSTEM_SELECT_BY_BRANCH";

            _ = command.Parameters.Add(new OracleParameter
            {
                ParameterName = "P_BRANCH_ID",
                OracleDbType = OracleDbType.Decimal,
                Direction = ParameterDirection.Input,
                Value = branchId
            });

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
                branchSystems.Add(MapToBranchSystem(reader));
            }
        }

        return branchSystems;
    }

    public async Task<SysBranchSystem?> GetBranchSystemAsync(long branchId, long systemId)
    {
        using var connection = _dbContext.CreateConnection();
        await connection.OpenAsync();

        using var command = connection.CreateCommand();
        command.CommandType = CommandType.StoredProcedure;
        command.CommandText = "SP_SYS_BRANCH_SYSTEM_SELECT_BY_ID";

        _ = command.Parameters.Add(new OracleParameter
        {
            ParameterName = "P_BRANCH_ID",
            OracleDbType = OracleDbType.Decimal,
            Direction = ParameterDirection.Input,
            Value = branchId
        });

        _ = command.Parameters.Add(new OracleParameter
        {
            ParameterName = "P_SYSTEM_ID",
            OracleDbType = OracleDbType.Decimal,
            Direction = ParameterDirection.Input,
            Value = systemId
        });

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
            return MapToBranchSystem(reader);
        }

        return null;
    }

    public async Task<long> GrantSystemAccessAsync(long branchId, long systemId, string grantedBy, string? notes, string creationUser)
    {
        using var connection = _dbContext.CreateConnection();
        await connection.OpenAsync();

        using var command = connection.CreateCommand();
        command.CommandType = CommandType.StoredProcedure;
        command.CommandText = "SP_SYS_BRANCH_SYSTEM_GRANT";

        _ = command.Parameters.Add(new OracleParameter
        {
            ParameterName = "P_BRANCH_ID",
            OracleDbType = OracleDbType.Decimal,
            Direction = ParameterDirection.Input,
            Value = branchId
        });

        _ = command.Parameters.Add(new OracleParameter
        {
            ParameterName = "P_SYSTEM_ID",
            OracleDbType = OracleDbType.Decimal,
            Direction = ParameterDirection.Input,
            Value = systemId
        });

        _ = command.Parameters.Add(new OracleParameter
        {
            ParameterName = "P_GRANTED_BY",
            OracleDbType = OracleDbType.Varchar2,
            Direction = ParameterDirection.Input,
            Value = grantedBy
        });

        _ = command.Parameters.Add(new OracleParameter
        {
            ParameterName = "P_NOTES",
            OracleDbType = OracleDbType.Varchar2,
            Direction = ParameterDirection.Input,
            Value = notes ?? (object)DBNull.Value
        });

        _ = command.Parameters.Add(new OracleParameter
        {
            ParameterName = "P_CREATION_USER",
            OracleDbType = OracleDbType.Varchar2,
            Direction = ParameterDirection.Input,
            Value = creationUser
        });

        OracleParameter newIdParam = new()
        {
            ParameterName = "P_NEW_ID",
            OracleDbType = OracleDbType.Decimal,
            Direction = ParameterDirection.Output
        };
        _ = command.Parameters.Add(newIdParam);

        await command.ExecuteNonQueryAsync();

        return long.Parse(newIdParam.Value.ToString()!);
    }

    public async Task<long> RevokeSystemAccessAsync(long branchId, long systemId, string updateUser)
    {
        using var connection = _dbContext.CreateConnection();
        await connection.OpenAsync();

        using var command = connection.CreateCommand();
        command.CommandType = CommandType.StoredProcedure;
        command.CommandText = "SP_SYS_BRANCH_SYSTEM_REVOKE";

        _ = command.Parameters.Add(new OracleParameter
        {
            ParameterName = "P_BRANCH_ID",
            OracleDbType = OracleDbType.Decimal,
            Direction = ParameterDirection.Input,
            Value = branchId
        });

        _ = command.Parameters.Add(new OracleParameter
        {
            ParameterName = "P_SYSTEM_ID",
            OracleDbType = OracleDbType.Decimal,
            Direction = ParameterDirection.Input,
            Value = systemId
        });

        _ = command.Parameters.Add(new OracleParameter
        {
            ParameterName = "P_UPDATE_USER",
            OracleDbType = OracleDbType.Varchar2,
            Direction = ParameterDirection.Input,
            Value = updateUser
        });

        return await command.ExecuteNonQueryAsync();
    }

    public async Task<bool> IsBranchSystemAllowedAsync(long branchId, long systemId)
    {
        using var connection = _dbContext.CreateConnection();
        await connection.OpenAsync();

        using var command = connection.CreateCommand();
        command.CommandType = CommandType.StoredProcedure;
        command.CommandText = "SP_SYS_BRANCH_SYSTEM_IS_ALLOWED";

        _ = command.Parameters.Add(new OracleParameter
        {
            ParameterName = "P_BRANCH_ID",
            OracleDbType = OracleDbType.Decimal,
            Direction = ParameterDirection.Input,
            Value = branchId
        });

        _ = command.Parameters.Add(new OracleParameter
        {
            ParameterName = "P_SYSTEM_ID",
            OracleDbType = OracleDbType.Decimal,
            Direction = ParameterDirection.Input,
            Value = systemId
        });

        OracleParameter isAllowedParam = new()
        {
            ParameterName = "P_IS_ALLOWED",
            OracleDbType = OracleDbType.Decimal,
            Direction = ParameterDirection.Output
        };
        _ = command.Parameters.Add(isAllowedParam);

        await command.ExecuteNonQueryAsync();

        return Convert.ToInt32(isAllowedParam.Value) == 1;
    }

    #endregion

    #region Branch Screen Permissions

    public async Task<List<SysBranchScreen>> GetBranchScreensAsync(long branchId)
    {
        List<SysBranchScreen> branchScreens = new();

        using (var connection = _dbContext.CreateConnection())
        {
            await connection.OpenAsync();

            using var command = connection.CreateCommand();
            command.CommandType = CommandType.StoredProcedure;
            command.CommandText = "SP_SYS_BRANCH_SCREEN_SELECT_BY_BRANCH";

            _ = command.Parameters.Add(new OracleParameter
            {
                ParameterName = "P_BRANCH_ID",
                OracleDbType = OracleDbType.Decimal,
                Direction = ParameterDirection.Input,
                Value = branchId
            });

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
                branchScreens.Add(MapToBranchScreen(reader));
            }
        }

        return branchScreens;
    }

    public async Task<SysBranchScreen?> GetBranchScreenAsync(long branchId, long screenId)
    {
        using var connection = _dbContext.CreateConnection();
        await connection.OpenAsync();

        using var command = connection.CreateCommand();
        command.CommandType = CommandType.StoredProcedure;
        command.CommandText = "SP_SYS_BRANCH_SCREEN_SELECT_BY_ID";

        _ = command.Parameters.Add(new OracleParameter
        {
            ParameterName = "P_BRANCH_ID",
            OracleDbType = OracleDbType.Decimal,
            Direction = ParameterDirection.Input,
            Value = branchId
        });

        _ = command.Parameters.Add(new OracleParameter
        {
            ParameterName = "P_SCREEN_ID",
            OracleDbType = OracleDbType.Decimal,
            Direction = ParameterDirection.Input,
            Value = screenId
        });

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
            return MapToBranchScreen(reader);
        }

        return null;
    }

    public async Task<long> GrantScreenAccessAsync(long branchId, long screenId, string grantedBy, string? notes, string creationUser)
    {
        using var connection = _dbContext.CreateConnection();
        await connection.OpenAsync();

        using var command = connection.CreateCommand();
        command.CommandType = CommandType.StoredProcedure;
        command.CommandText = "SP_SYS_BRANCH_SCREEN_GRANT";

        _ = command.Parameters.Add(new OracleParameter
        {
            ParameterName = "P_BRANCH_ID",
            OracleDbType = OracleDbType.Decimal,
            Direction = ParameterDirection.Input,
            Value = branchId
        });

        _ = command.Parameters.Add(new OracleParameter
        {
            ParameterName = "P_SCREEN_ID",
            OracleDbType = OracleDbType.Decimal,
            Direction = ParameterDirection.Input,
            Value = screenId
        });

        _ = command.Parameters.Add(new OracleParameter
        {
            ParameterName = "P_GRANTED_BY",
            OracleDbType = OracleDbType.Varchar2,
            Direction = ParameterDirection.Input,
            Value = grantedBy
        });

        _ = command.Parameters.Add(new OracleParameter
        {
            ParameterName = "P_NOTES",
            OracleDbType = OracleDbType.Varchar2,
            Direction = ParameterDirection.Input,
            Value = notes ?? (object)DBNull.Value
        });

        _ = command.Parameters.Add(new OracleParameter
        {
            ParameterName = "P_CREATION_USER",
            OracleDbType = OracleDbType.Varchar2,
            Direction = ParameterDirection.Input,
            Value = creationUser
        });

        OracleParameter newIdParam = new()
        {
            ParameterName = "P_NEW_ID",
            OracleDbType = OracleDbType.Decimal,
            Direction = ParameterDirection.Output
        };
        _ = command.Parameters.Add(newIdParam);

        await command.ExecuteNonQueryAsync();

        return long.Parse(newIdParam.Value.ToString()!);
    }

    public async Task<long> RevokeScreenAccessAsync(long branchId, long screenId, string updateUser)
    {
        using var connection = _dbContext.CreateConnection();
        await connection.OpenAsync();

        using var command = connection.CreateCommand();
        command.CommandType = CommandType.StoredProcedure;
        command.CommandText = "SP_SYS_BRANCH_SCREEN_REVOKE";

        _ = command.Parameters.Add(new OracleParameter
        {
            ParameterName = "P_BRANCH_ID",
            OracleDbType = OracleDbType.Decimal,
            Direction = ParameterDirection.Input,
            Value = branchId
        });

        _ = command.Parameters.Add(new OracleParameter
        {
            ParameterName = "P_SCREEN_ID",
            OracleDbType = OracleDbType.Decimal,
            Direction = ParameterDirection.Input,
            Value = screenId
        });

        _ = command.Parameters.Add(new OracleParameter
        {
            ParameterName = "P_UPDATE_USER",
            OracleDbType = OracleDbType.Varchar2,
            Direction = ParameterDirection.Input,
            Value = updateUser
        });

        return await command.ExecuteNonQueryAsync();
    }

    public async Task<bool> IsBranchScreenAllowedAsync(long branchId, long screenId)
    {
        using var connection = _dbContext.CreateConnection();
        await connection.OpenAsync();

        using var command = connection.CreateCommand();
        command.CommandType = CommandType.StoredProcedure;
        command.CommandText = "SP_SYS_BRANCH_SCREEN_IS_ALLOWED";

        _ = command.Parameters.Add(new OracleParameter
        {
            ParameterName = "P_BRANCH_ID",
            OracleDbType = OracleDbType.Decimal,
            Direction = ParameterDirection.Input,
            Value = branchId
        });

        _ = command.Parameters.Add(new OracleParameter
        {
            ParameterName = "P_SCREEN_ID",
            OracleDbType = OracleDbType.Decimal,
            Direction = ParameterDirection.Input,
            Value = screenId
        });

        OracleParameter isAllowedParam = new()
        {
            ParameterName = "P_IS_ALLOWED",
            OracleDbType = OracleDbType.Decimal,
            Direction = ParameterDirection.Output
        };
        _ = command.Parameters.Add(isAllowedParam);

        await command.ExecuteNonQueryAsync();

        return Convert.ToInt32(isAllowedParam.Value) == 1;
    }

    #endregion

    #region Bulk Operations

    public async Task<int> GrantMultipleSystemsAsync(long branchId, List<long> systemIds, string grantedBy, string creationUser)
    {
        int count = 0;
        foreach (var systemId in systemIds)
        {
            await GrantSystemAccessAsync(branchId, systemId, grantedBy, null, creationUser);
            count++;
        }
        return count;
    }

    public async Task<int> GrantMultipleScreensAsync(long branchId, List<long> screenIds, string grantedBy, string creationUser)
    {
        int count = 0;
        foreach (var screenId in screenIds)
        {
            await GrantScreenAccessAsync(branchId, screenId, grantedBy, null, creationUser);
            count++;
        }
        return count;
    }

    #endregion

    #region Get All Systems/Screens

    public async Task<List<SysSystem>> GetAllSystemsAsync()
    {
        List<SysSystem> systems = new();

        using (var connection = _dbContext.CreateConnection())
        {
            await connection.OpenAsync();

            using var command = connection.CreateCommand();
            command.CommandType = CommandType.StoredProcedure;
            command.CommandText = "SP_SYS_SYSTEM_SELECT_ALL";

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
                systems.Add(MapToSystem(reader));
            }
        }

        return systems;
    }

    public async Task<List<SysScreen>> GetAllScreensAsync()
    {
        List<SysScreen> screens = new();

        using (var connection = _dbContext.CreateConnection())
        {
            await connection.OpenAsync();

            using var command = connection.CreateCommand();
            command.CommandType = CommandType.StoredProcedure;
            command.CommandText = "SP_SYS_SCREEN_SELECT_ALL";

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
                screens.Add(MapToScreen(reader));
            }
        }

        return screens;
    }

    public async Task<List<SysScreen>> GetScreensBySystemAsync(long systemId)
    {
        List<SysScreen> screens = new();

        using (var connection = _dbContext.CreateConnection())
        {
            await connection.OpenAsync();

            using var command = connection.CreateCommand();
            command.CommandType = CommandType.StoredProcedure;
            command.CommandText = "SP_SYS_SCREEN_SELECT_BY_SYSTEM";

            _ = command.Parameters.Add(new OracleParameter
            {
                ParameterName = "P_SYSTEM_ID",
                OracleDbType = OracleDbType.Decimal,
                Direction = ParameterDirection.Input,
                Value = systemId
            });

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
                screens.Add(MapToScreen(reader));
            }
        }

        return screens;
    }

    #endregion

    #region Mapping Methods

    private SysBranchSystem MapToBranchSystem(OracleDataReader reader)
    {
        return new SysBranchSystem
        {
            RowId = reader.GetInt64(reader.GetOrdinal("ROW_ID")),
            BranchId = reader.GetInt64(reader.GetOrdinal("BRANCH_ID")),
            SystemId = reader.GetInt64(reader.GetOrdinal("SYSTEM_ID")),
            IsAllowed = reader.GetString(reader.GetOrdinal("IS_ALLOWED")) == "1",
            GrantedBy = reader.IsDBNull(reader.GetOrdinal("GRANTED_BY")) ? null : reader.GetInt64(reader.GetOrdinal("GRANTED_BY")),
            GrantedDate = reader.IsDBNull(reader.GetOrdinal("GRANTED_DATE")) ? null : reader.GetDateTime(reader.GetOrdinal("GRANTED_DATE")),
            RevokedDate = reader.IsDBNull(reader.GetOrdinal("REVOKED_DATE")) ? null : reader.GetDateTime(reader.GetOrdinal("REVOKED_DATE")),
            Notes = reader.IsDBNull(reader.GetOrdinal("NOTES")) ? null : reader.GetString(reader.GetOrdinal("NOTES")),
            CreationUser = reader.GetString(reader.GetOrdinal("CREATION_USER")),
            CreationDate = reader.IsDBNull(reader.GetOrdinal("CREATION_DATE")) ? null : reader.GetDateTime(reader.GetOrdinal("CREATION_DATE")),
            UpdateUser = reader.IsDBNull(reader.GetOrdinal("UPDATE_USER")) ? null : reader.GetString(reader.GetOrdinal("UPDATE_USER")),
            UpdateDate = reader.IsDBNull(reader.GetOrdinal("UPDATE_DATE")) ? null : reader.GetDateTime(reader.GetOrdinal("UPDATE_DATE"))
        };
    }

    private SysBranchScreen MapToBranchScreen(OracleDataReader reader)
    {
        return new SysBranchScreen
        {
            RowId = reader.GetInt64(reader.GetOrdinal("ROW_ID")),
            BranchId = reader.GetInt64(reader.GetOrdinal("BRANCH_ID")),
            ScreenId = reader.GetInt64(reader.GetOrdinal("SCREEN_ID")),
            IsAllowed = reader.GetString(reader.GetOrdinal("IS_ALLOWED")) == "1",
            GrantedBy = reader.IsDBNull(reader.GetOrdinal("GRANTED_BY")) ? null : reader.GetInt64(reader.GetOrdinal("GRANTED_BY")),
            GrantedDate = reader.IsDBNull(reader.GetOrdinal("GRANTED_DATE")) ? null : reader.GetDateTime(reader.GetOrdinal("GRANTED_DATE")),
            RevokedDate = reader.IsDBNull(reader.GetOrdinal("REVOKED_DATE")) ? null : reader.GetDateTime(reader.GetOrdinal("REVOKED_DATE")),
            Notes = reader.IsDBNull(reader.GetOrdinal("NOTES")) ? null : reader.GetString(reader.GetOrdinal("NOTES")),
            CreationUser = reader.GetString(reader.GetOrdinal("CREATION_USER")),
            CreationDate = reader.IsDBNull(reader.GetOrdinal("CREATION_DATE")) ? null : reader.GetDateTime(reader.GetOrdinal("CREATION_DATE")),
            UpdateUser = reader.IsDBNull(reader.GetOrdinal("UPDATE_USER")) ? null : reader.GetString(reader.GetOrdinal("UPDATE_USER")),
            UpdateDate = reader.IsDBNull(reader.GetOrdinal("UPDATE_DATE")) ? null : reader.GetDateTime(reader.GetOrdinal("UPDATE_DATE"))
        };
    }

    private SysSystem MapToSystem(OracleDataReader reader)
    {
        return new SysSystem
        {
            RowId = reader.GetInt64(reader.GetOrdinal("ROW_ID")),
            SystemCode = reader.IsDBNull(reader.GetOrdinal("SYSTEM_CODE")) ? null : reader.GetString(reader.GetOrdinal("SYSTEM_CODE")),
            SystemName = reader.IsDBNull(reader.GetOrdinal("SYSTEM_NAME")) ? null : reader.GetString(reader.GetOrdinal("SYSTEM_NAME")),
            SystemNameE = reader.IsDBNull(reader.GetOrdinal("SYSTEM_NAME_E")) ? null : reader.GetString(reader.GetOrdinal("SYSTEM_NAME_E")),
            Icon = reader.IsDBNull(reader.GetOrdinal("SYSTEM_ICON")) ? null : reader.GetString(reader.GetOrdinal("SYSTEM_ICON")),
            DisplayOrder = reader.IsDBNull(reader.GetOrdinal("SYSTEM_ORDER")) ? 0 : reader.GetInt32(reader.GetOrdinal("SYSTEM_ORDER")),
            IsActive = reader.GetString(reader.GetOrdinal("IS_ACTIVE")) == "1"
        };
    }

    private SysScreen MapToScreen(OracleDataReader reader)
    {
        return new SysScreen
        {
            RowId = reader.GetInt64(reader.GetOrdinal("ROW_ID")),
            SystemId = reader.IsDBNull(reader.GetOrdinal("SYSTEM_ID")) ? 0 : reader.GetInt64(reader.GetOrdinal("SYSTEM_ID")),
            ScreenCode = reader.IsDBNull(reader.GetOrdinal("SCREEN_CODE")) ? null : reader.GetString(reader.GetOrdinal("SCREEN_CODE")),
            ScreenName = reader.IsDBNull(reader.GetOrdinal("SCREEN_NAME")) ? null : reader.GetString(reader.GetOrdinal("SCREEN_NAME")),
            ScreenNameE = reader.IsDBNull(reader.GetOrdinal("SCREEN_NAME_E")) ? null : reader.GetString(reader.GetOrdinal("SCREEN_NAME_E")),
            Route = reader.IsDBNull(reader.GetOrdinal("SCREEN_URL")) ? null : reader.GetString(reader.GetOrdinal("SCREEN_URL")),
            DisplayOrder = reader.IsDBNull(reader.GetOrdinal("SCREEN_ORDER")) ? 0 : reader.GetInt32(reader.GetOrdinal("SCREEN_ORDER")),
            IsActive = reader.GetString(reader.GetOrdinal("IS_ACTIVE")) == "1"
        };
    }

    #endregion
}
