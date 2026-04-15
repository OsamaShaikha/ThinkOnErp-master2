using Oracle.ManagedDataAccess.Client;
using System.Data;
using ThinkOnErp.Domain.Entities;
using ThinkOnErp.Domain.Interfaces;
using ThinkOnErp.Infrastructure.Data;

namespace ThinkOnErp.Infrastructure.Repositories;

/// <summary>
/// Repository implementation for system/module operations using ADO.NET with Oracle stored procedures.
/// </summary>
public class SystemRepository : ISystemRepository
{
    private readonly OracleDbContext _dbContext;

    public SystemRepository(OracleDbContext dbContext)
    {
        _dbContext = dbContext ?? throw new ArgumentNullException(nameof(dbContext));
    }

    public async Task<List<SysSystem>> GetAllSystemsAsync()
    {
        using OracleConnection connection = _dbContext.CreateConnection();
        await connection.OpenAsync();

        using OracleCommand command = connection.CreateCommand();
        command.CommandType = CommandType.StoredProcedure;
        command.CommandText = "SP_SYS_SYSTEM_GET_ALL";

        var cursorParam = new OracleParameter("P_RESULT_CURSOR", OracleDbType.RefCursor, ParameterDirection.Output);
        command.Parameters.Add(cursorParam);

        var systems = new List<SysSystem>();

        using OracleDataReader reader = await command.ExecuteReaderAsync();
        while (await reader.ReadAsync())
        {
            systems.Add(MapToEntity(reader));
        }

        return systems;
    }

    public async Task<SysSystem?> GetSystemByIdAsync(long systemId)
    {
        using OracleConnection connection = _dbContext.CreateConnection();
        await connection.OpenAsync();

        using OracleCommand command = connection.CreateCommand();
        command.CommandType = CommandType.StoredProcedure;
        command.CommandText = "SP_SYS_SYSTEM_GET_BY_ID";

        command.Parameters.Add(new OracleParameter("P_ROW_ID", OracleDbType.Int64, systemId, ParameterDirection.Input));
        
        var cursorParam = new OracleParameter("P_RESULT_CURSOR", OracleDbType.RefCursor, ParameterDirection.Output);
        command.Parameters.Add(cursorParam);

        using OracleDataReader reader = await command.ExecuteReaderAsync();
        if (await reader.ReadAsync())
        {
            return MapToEntity(reader);
        }

        return null;
    }

    public async Task<long> CreateSystemAsync(SysSystem system)
    {
        using OracleConnection connection = _dbContext.CreateConnection();
        await connection.OpenAsync();

        using OracleCommand command = connection.CreateCommand();
        command.CommandType = CommandType.StoredProcedure;
        command.CommandText = "SP_SYS_SYSTEM_CREATE";

        command.Parameters.Add(new OracleParameter("P_SYSTEM_CODE", OracleDbType.Varchar2, system.SystemCode, ParameterDirection.Input));
        command.Parameters.Add(new OracleParameter("P_SYSTEM_NAME", OracleDbType.NVarchar2, system.SystemName, ParameterDirection.Input));
        command.Parameters.Add(new OracleParameter("P_SYSTEM_NAME_E", OracleDbType.NVarchar2, system.SystemNameE, ParameterDirection.Input));
        command.Parameters.Add(new OracleParameter("P_DESCRIPTION", OracleDbType.NVarchar2, system.Description ?? (object)DBNull.Value, ParameterDirection.Input));
        command.Parameters.Add(new OracleParameter("P_DESCRIPTION_E", OracleDbType.NVarchar2, system.DescriptionE ?? (object)DBNull.Value, ParameterDirection.Input));
        command.Parameters.Add(new OracleParameter("P_ICON", OracleDbType.NVarchar2, system.Icon ?? (object)DBNull.Value, ParameterDirection.Input));
        command.Parameters.Add(new OracleParameter("P_DISPLAY_ORDER", OracleDbType.Int32, system.DisplayOrder, ParameterDirection.Input));
        command.Parameters.Add(new OracleParameter("P_CREATION_USER", OracleDbType.NVarchar2, system.CreationUser, ParameterDirection.Input));

        var newIdParam = new OracleParameter("P_NEW_ID", OracleDbType.Int64, ParameterDirection.Output);
        command.Parameters.Add(newIdParam);

        await command.ExecuteNonQueryAsync();

        return Convert.ToInt64(newIdParam.Value.ToString());
    }

    public async Task UpdateSystemAsync(SysSystem system)
    {
        using OracleConnection connection = _dbContext.CreateConnection();
        await connection.OpenAsync();

        using OracleCommand command = connection.CreateCommand();
        command.CommandType = CommandType.StoredProcedure;
        command.CommandText = "SP_SYS_SYSTEM_UPDATE";

        command.Parameters.Add(new OracleParameter("P_ROW_ID", OracleDbType.Int64, system.RowId, ParameterDirection.Input));
        command.Parameters.Add(new OracleParameter("P_SYSTEM_CODE", OracleDbType.Varchar2, system.SystemCode, ParameterDirection.Input));
        command.Parameters.Add(new OracleParameter("P_SYSTEM_NAME", OracleDbType.NVarchar2, system.SystemName, ParameterDirection.Input));
        command.Parameters.Add(new OracleParameter("P_SYSTEM_NAME_E", OracleDbType.NVarchar2, system.SystemNameE, ParameterDirection.Input));
        command.Parameters.Add(new OracleParameter("P_DESCRIPTION", OracleDbType.NVarchar2, system.Description ?? (object)DBNull.Value, ParameterDirection.Input));
        command.Parameters.Add(new OracleParameter("P_DESCRIPTION_E", OracleDbType.NVarchar2, system.DescriptionE ?? (object)DBNull.Value, ParameterDirection.Input));
        command.Parameters.Add(new OracleParameter("P_ICON", OracleDbType.NVarchar2, system.Icon ?? (object)DBNull.Value, ParameterDirection.Input));
        command.Parameters.Add(new OracleParameter("P_DISPLAY_ORDER", OracleDbType.Int32, system.DisplayOrder, ParameterDirection.Input));
        command.Parameters.Add(new OracleParameter("P_UPDATE_USER", OracleDbType.NVarchar2, system.UpdateUser ?? system.CreationUser, ParameterDirection.Input));

        await command.ExecuteNonQueryAsync();
    }

    public async Task DeleteSystemAsync(long systemId, string updateUser)
    {
        using OracleConnection connection = _dbContext.CreateConnection();
        await connection.OpenAsync();

        using OracleCommand command = connection.CreateCommand();
        command.CommandType = CommandType.StoredProcedure;
        command.CommandText = "SP_SYS_SYSTEM_DELETE";

        command.Parameters.Add(new OracleParameter("P_ROW_ID", OracleDbType.Int64, systemId, ParameterDirection.Input));
        command.Parameters.Add(new OracleParameter("P_UPDATE_USER", OracleDbType.NVarchar2, updateUser, ParameterDirection.Input));

        await command.ExecuteNonQueryAsync();
    }

    private SysSystem MapToEntity(OracleDataReader reader)
    {
        return new SysSystem
        {
            RowId = reader.GetInt64(reader.GetOrdinal("ROW_ID")),
            SystemCode = reader.GetString(reader.GetOrdinal("SYSTEM_CODE")),
            SystemName = reader.GetString(reader.GetOrdinal("SYSTEM_NAME")),
            SystemNameE = reader.GetString(reader.GetOrdinal("SYSTEM_NAME_E")),
            Description = reader.IsDBNull(reader.GetOrdinal("DESCRIPTION")) ? null : reader.GetString(reader.GetOrdinal("DESCRIPTION")),
            DescriptionE = reader.IsDBNull(reader.GetOrdinal("DESCRIPTION_E")) ? null : reader.GetString(reader.GetOrdinal("DESCRIPTION_E")),
            Icon = reader.IsDBNull(reader.GetOrdinal("ICON")) ? null : reader.GetString(reader.GetOrdinal("ICON")),
            DisplayOrder = reader.GetInt32(reader.GetOrdinal("DISPLAY_ORDER")),
            IsActive = reader.GetString(reader.GetOrdinal("IS_ACTIVE")) == "1",
            CreationUser = reader.GetString(reader.GetOrdinal("CREATION_USER")),
            CreationDate = reader.IsDBNull(reader.GetOrdinal("CREATION_DATE")) ? null : reader.GetDateTime(reader.GetOrdinal("CREATION_DATE")),
            UpdateUser = reader.IsDBNull(reader.GetOrdinal("UPDATE_USER")) ? null : reader.GetString(reader.GetOrdinal("UPDATE_USER")),
            UpdateDate = reader.IsDBNull(reader.GetOrdinal("UPDATE_DATE")) ? null : reader.GetDateTime(reader.GetOrdinal("UPDATE_DATE"))
        };
    }
}
