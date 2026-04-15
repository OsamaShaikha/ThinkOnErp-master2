using Oracle.ManagedDataAccess.Client;
using System.Data;
using ThinkOnErp.Domain.Entities;
using ThinkOnErp.Domain.Interfaces;
using ThinkOnErp.Infrastructure.Data;

namespace ThinkOnErp.Infrastructure.Repositories;

/// <summary>
/// Repository implementation for screen/page operations using ADO.NET with Oracle stored procedures.
/// </summary>
public class ScreenRepository : IScreenRepository
{
    private readonly OracleDbContext _dbContext;

    public ScreenRepository(OracleDbContext dbContext)
    {
        _dbContext = dbContext ?? throw new ArgumentNullException(nameof(dbContext));
    }

    public async Task<List<SysScreen>> GetAllScreensAsync()
    {
        using OracleConnection connection = _dbContext.CreateConnection();
        await connection.OpenAsync();

        using OracleCommand command = connection.CreateCommand();
        command.CommandType = CommandType.StoredProcedure;
        command.CommandText = "SP_SYS_SCREEN_GET_ALL";

        var cursorParam = new OracleParameter("P_RESULT_CURSOR", OracleDbType.RefCursor, ParameterDirection.Output);
        command.Parameters.Add(cursorParam);

        var screens = new List<SysScreen>();

        using OracleDataReader reader = await command.ExecuteReaderAsync();
        while (await reader.ReadAsync())
        {
            screens.Add(MapToEntity(reader));
        }

        return screens;
    }

    public async Task<List<SysScreen>> GetScreensBySystemIdAsync(long systemId)
    {
        using OracleConnection connection = _dbContext.CreateConnection();
        await connection.OpenAsync();

        using OracleCommand command = connection.CreateCommand();
        command.CommandType = CommandType.StoredProcedure;
        command.CommandText = "SP_SYS_SCREEN_GET_BY_SYSTEM";

        command.Parameters.Add(new OracleParameter("P_SYSTEM_ID", OracleDbType.Int64, systemId, ParameterDirection.Input));
        
        var cursorParam = new OracleParameter("P_RESULT_CURSOR", OracleDbType.RefCursor, ParameterDirection.Output);
        command.Parameters.Add(cursorParam);

        var screens = new List<SysScreen>();

        using OracleDataReader reader = await command.ExecuteReaderAsync();
        while (await reader.ReadAsync())
        {
            screens.Add(MapToEntity(reader));
        }

        return screens;
    }

    public async Task<SysScreen?> GetScreenByIdAsync(long screenId)
    {
        using OracleConnection connection = _dbContext.CreateConnection();
        await connection.OpenAsync();

        using OracleCommand command = connection.CreateCommand();
        command.CommandType = CommandType.StoredProcedure;
        command.CommandText = "SP_SYS_SCREEN_GET_BY_ID";

        command.Parameters.Add(new OracleParameter("P_ROW_ID", OracleDbType.Int64, screenId, ParameterDirection.Input));
        
        var cursorParam = new OracleParameter("P_RESULT_CURSOR", OracleDbType.RefCursor, ParameterDirection.Output);
        command.Parameters.Add(cursorParam);

        using OracleDataReader reader = await command.ExecuteReaderAsync();
        if (await reader.ReadAsync())
        {
            return MapToEntity(reader);
        }

        return null;
    }

    public async Task<long> CreateScreenAsync(SysScreen screen)
    {
        using OracleConnection connection = _dbContext.CreateConnection();
        await connection.OpenAsync();

        using OracleCommand command = connection.CreateCommand();
        command.CommandType = CommandType.StoredProcedure;
        command.CommandText = "SP_SYS_SCREEN_CREATE";

        command.Parameters.Add(new OracleParameter("P_SYSTEM_ID", OracleDbType.Int64, screen.SystemId, ParameterDirection.Input));
        command.Parameters.Add(new OracleParameter("P_PARENT_SCREEN_ID", OracleDbType.Int64, screen.ParentScreenId ?? (object)DBNull.Value, ParameterDirection.Input));
        command.Parameters.Add(new OracleParameter("P_SCREEN_CODE", OracleDbType.NVarchar2, screen.ScreenCode, ParameterDirection.Input));
        command.Parameters.Add(new OracleParameter("P_SCREEN_NAME", OracleDbType.NVarchar2, screen.ScreenName, ParameterDirection.Input));
        command.Parameters.Add(new OracleParameter("P_SCREEN_NAME_E", OracleDbType.NVarchar2, screen.ScreenNameE, ParameterDirection.Input));
        command.Parameters.Add(new OracleParameter("P_ROUTE", OracleDbType.NVarchar2, screen.Route ?? (object)DBNull.Value, ParameterDirection.Input));
        command.Parameters.Add(new OracleParameter("P_DESCRIPTION", OracleDbType.NVarchar2, screen.Description ?? (object)DBNull.Value, ParameterDirection.Input));
        command.Parameters.Add(new OracleParameter("P_DESCRIPTION_E", OracleDbType.NVarchar2, screen.DescriptionE ?? (object)DBNull.Value, ParameterDirection.Input));
        command.Parameters.Add(new OracleParameter("P_ICON", OracleDbType.NVarchar2, screen.Icon ?? (object)DBNull.Value, ParameterDirection.Input));
        command.Parameters.Add(new OracleParameter("P_DISPLAY_ORDER", OracleDbType.Int32, screen.DisplayOrder, ParameterDirection.Input));
        command.Parameters.Add(new OracleParameter("P_CREATION_USER", OracleDbType.NVarchar2, screen.CreationUser, ParameterDirection.Input));

        var newIdParam = new OracleParameter("P_NEW_ID", OracleDbType.Int64, ParameterDirection.Output);
        command.Parameters.Add(newIdParam);

        await command.ExecuteNonQueryAsync();

        return Convert.ToInt64(newIdParam.Value.ToString());
    }

    public async Task UpdateScreenAsync(SysScreen screen)
    {
        using OracleConnection connection = _dbContext.CreateConnection();
        await connection.OpenAsync();

        using OracleCommand command = connection.CreateCommand();
        command.CommandType = CommandType.StoredProcedure;
        command.CommandText = "SP_SYS_SCREEN_UPDATE";

        command.Parameters.Add(new OracleParameter("P_ROW_ID", OracleDbType.Int64, screen.RowId, ParameterDirection.Input));
        command.Parameters.Add(new OracleParameter("P_SYSTEM_ID", OracleDbType.Int64, screen.SystemId, ParameterDirection.Input));
        command.Parameters.Add(new OracleParameter("P_PARENT_SCREEN_ID", OracleDbType.Int64, screen.ParentScreenId ?? (object)DBNull.Value, ParameterDirection.Input));
        command.Parameters.Add(new OracleParameter("P_SCREEN_CODE", OracleDbType.NVarchar2, screen.ScreenCode, ParameterDirection.Input));
        command.Parameters.Add(new OracleParameter("P_SCREEN_NAME", OracleDbType.NVarchar2, screen.ScreenName, ParameterDirection.Input));
        command.Parameters.Add(new OracleParameter("P_SCREEN_NAME_E", OracleDbType.NVarchar2, screen.ScreenNameE, ParameterDirection.Input));
        command.Parameters.Add(new OracleParameter("P_ROUTE", OracleDbType.NVarchar2, screen.Route ?? (object)DBNull.Value, ParameterDirection.Input));
        command.Parameters.Add(new OracleParameter("P_DESCRIPTION", OracleDbType.NVarchar2, screen.Description ?? (object)DBNull.Value, ParameterDirection.Input));
        command.Parameters.Add(new OracleParameter("P_DESCRIPTION_E", OracleDbType.NVarchar2, screen.DescriptionE ?? (object)DBNull.Value, ParameterDirection.Input));
        command.Parameters.Add(new OracleParameter("P_ICON", OracleDbType.NVarchar2, screen.Icon ?? (object)DBNull.Value, ParameterDirection.Input));
        command.Parameters.Add(new OracleParameter("P_DISPLAY_ORDER", OracleDbType.Int32, screen.DisplayOrder, ParameterDirection.Input));
        command.Parameters.Add(new OracleParameter("P_UPDATE_USER", OracleDbType.NVarchar2, screen.UpdateUser ?? screen.CreationUser, ParameterDirection.Input));

        await command.ExecuteNonQueryAsync();
    }

    public async Task DeleteScreenAsync(long screenId, string updateUser)
    {
        using OracleConnection connection = _dbContext.CreateConnection();
        await connection.OpenAsync();

        using OracleCommand command = connection.CreateCommand();
        command.CommandType = CommandType.StoredProcedure;
        command.CommandText = "SP_SYS_SCREEN_DELETE";

        command.Parameters.Add(new OracleParameter("P_ROW_ID", OracleDbType.Int64, screenId, ParameterDirection.Input));
        command.Parameters.Add(new OracleParameter("P_UPDATE_USER", OracleDbType.NVarchar2, updateUser, ParameterDirection.Input));

        await command.ExecuteNonQueryAsync();
    }

    private SysScreen MapToEntity(OracleDataReader reader)
    {
        return new SysScreen
        {
            RowId = reader.GetInt64(reader.GetOrdinal("ROW_ID")),
            SystemId = reader.GetInt64(reader.GetOrdinal("SYSTEM_ID")),
            ParentScreenId = reader.IsDBNull(reader.GetOrdinal("PARENT_SCREEN_ID")) ? null : reader.GetInt64(reader.GetOrdinal("PARENT_SCREEN_ID")),
            ScreenCode = reader.GetString(reader.GetOrdinal("SCREEN_CODE")),
            ScreenName = reader.GetString(reader.GetOrdinal("SCREEN_NAME")),
            ScreenNameE = reader.GetString(reader.GetOrdinal("SCREEN_NAME_E")),
            Route = reader.IsDBNull(reader.GetOrdinal("ROUTE")) ? null : reader.GetString(reader.GetOrdinal("ROUTE")),
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
