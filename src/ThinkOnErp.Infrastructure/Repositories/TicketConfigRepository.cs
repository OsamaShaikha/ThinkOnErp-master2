using Oracle.ManagedDataAccess.Client;
using System.Data;
using ThinkOnErp.Domain.Entities;
using ThinkOnErp.Domain.Interfaces;
using ThinkOnErp.Infrastructure.Data;

namespace ThinkOnErp.Infrastructure.Repositories;

/// <summary>
/// Repository implementation for SysTicketConfig entity using ADO.NET with Oracle stored procedures.
/// Implements ITicketConfigRepository interface from the Domain layer.
/// </summary>
public class TicketConfigRepository : ITicketConfigRepository
{
    private readonly OracleDbContext _dbContext;

    public TicketConfigRepository(OracleDbContext dbContext)
    {
        _dbContext = dbContext ?? throw new ArgumentNullException(nameof(dbContext));
    }

    public async Task<List<SysTicketConfig>> GetAllAsync()
    {
        List<SysTicketConfig> configs = new();

        using (var connection = _dbContext.CreateConnection())
        {
            await connection.OpenAsync();

            using var command = connection.CreateCommand();
            command.CommandType = CommandType.StoredProcedure;
            command.CommandText = "SP_SYS_TICKET_CONFIG_SELECT_ALL";

            OracleParameter cursorParam = new()
            {
                ParameterName = "p_cursor",
                OracleDbType = OracleDbType.RefCursor,
                Direction = ParameterDirection.Output
            };
            _ = command.Parameters.Add(cursorParam);

            using var reader = await command.ExecuteReaderAsync();
            while (await reader.ReadAsync())
            {
                configs.Add(MapToEntity(reader));
            }
        }

        return configs;
    }

    public async Task<SysTicketConfig?> GetByKeyAsync(string configKey)
    {
        using var connection = _dbContext.CreateConnection();
        await connection.OpenAsync();

        using var command = connection.CreateCommand();
        command.CommandType = CommandType.StoredProcedure;
        command.CommandText = "SP_SYS_TICKET_CONFIG_SELECT_BY_KEY";

        _ = command.Parameters.Add(new OracleParameter
        {
            ParameterName = "p_config_key",
            OracleDbType = OracleDbType.NVarchar2,
            Direction = ParameterDirection.Input,
            Value = configKey
        });

        OracleParameter cursorParam = new()
        {
            ParameterName = "p_cursor",
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

    public async Task<List<SysTicketConfig>> GetByTypeAsync(string configType)
    {
        List<SysTicketConfig> configs = new();

        using (var connection = _dbContext.CreateConnection())
        {
            await connection.OpenAsync();

            using var command = connection.CreateCommand();
            command.CommandType = CommandType.StoredProcedure;
            command.CommandText = "SP_SYS_TICKET_CONFIG_SELECT_BY_TYPE";

            _ = command.Parameters.Add(new OracleParameter
            {
                ParameterName = "p_config_type",
                OracleDbType = OracleDbType.NVarchar2,
                Direction = ParameterDirection.Input,
                Value = configType
            });

            OracleParameter cursorParam = new()
            {
                ParameterName = "p_cursor",
                OracleDbType = OracleDbType.RefCursor,
                Direction = ParameterDirection.Output
            };
            _ = command.Parameters.Add(cursorParam);

            using var reader = await command.ExecuteReaderAsync();
            while (await reader.ReadAsync())
            {
                configs.Add(MapToEntity(reader));
            }
        }

        return configs;
    }

    public async Task<Int64> CreateAsync(SysTicketConfig config)
    {
        using var connection = _dbContext.CreateConnection();
        await connection.OpenAsync();

        using var command = connection.CreateCommand();
        command.CommandType = CommandType.StoredProcedure;
        command.CommandText = "SP_SYS_TICKET_CONFIG_INSERT";

        _ = command.Parameters.Add(new OracleParameter
        {
            ParameterName = "p_config_key",
            OracleDbType = OracleDbType.NVarchar2,
            Direction = ParameterDirection.Input,
            Value = config.ConfigKey
        });

        _ = command.Parameters.Add(new OracleParameter
        {
            ParameterName = "p_config_value",
            OracleDbType = OracleDbType.NClob,
            Direction = ParameterDirection.Input,
            Value = config.ConfigValue
        });

        _ = command.Parameters.Add(new OracleParameter
        {
            ParameterName = "p_config_type",
            OracleDbType = OracleDbType.NVarchar2,
            Direction = ParameterDirection.Input,
            Value = config.ConfigType
        });

        _ = command.Parameters.Add(new OracleParameter
        {
            ParameterName = "p_description_ar",
            OracleDbType = OracleDbType.NVarchar2,
            Direction = ParameterDirection.Input,
            Value = (object?)config.DescriptionAr ?? DBNull.Value
        });

        _ = command.Parameters.Add(new OracleParameter
        {
            ParameterName = "p_description_en",
            OracleDbType = OracleDbType.NVarchar2,
            Direction = ParameterDirection.Input,
            Value = (object?)config.DescriptionEn ?? DBNull.Value
        });

        _ = command.Parameters.Add(new OracleParameter
        {
            ParameterName = "p_creation_user",
            OracleDbType = OracleDbType.NVarchar2,
            Direction = ParameterDirection.Input,
            Value = config.CreationUser
        });

        OracleParameter newIdParam = new()
        {
            ParameterName = "p_row_id",
            OracleDbType = OracleDbType.Decimal,
            Direction = ParameterDirection.Output
        };
        _ = command.Parameters.Add(newIdParam);

        _ = await command.ExecuteNonQueryAsync();

        return long.Parse(newIdParam.Value.ToString()!);
    }

    public async Task<Int64> UpdateAsync(SysTicketConfig config)
    {
        using var connection = _dbContext.CreateConnection();
        await connection.OpenAsync();

        using var command = connection.CreateCommand();
        command.CommandType = CommandType.StoredProcedure;
        command.CommandText = "SP_SYS_TICKET_CONFIG_UPDATE";

        _ = command.Parameters.Add(new OracleParameter
        {
            ParameterName = "p_row_id",
            OracleDbType = OracleDbType.Decimal,
            Direction = ParameterDirection.Input,
            Value = config.RowId
        });

        _ = command.Parameters.Add(new OracleParameter
        {
            ParameterName = "p_config_value",
            OracleDbType = OracleDbType.NClob,
            Direction = ParameterDirection.Input,
            Value = config.ConfigValue
        });

        _ = command.Parameters.Add(new OracleParameter
        {
            ParameterName = "p_description_ar",
            OracleDbType = OracleDbType.NVarchar2,
            Direction = ParameterDirection.Input,
            Value = (object?)config.DescriptionAr ?? DBNull.Value
        });

        _ = command.Parameters.Add(new OracleParameter
        {
            ParameterName = "p_description_en",
            OracleDbType = OracleDbType.NVarchar2,
            Direction = ParameterDirection.Input,
            Value = (object?)config.DescriptionEn ?? DBNull.Value
        });

        _ = command.Parameters.Add(new OracleParameter
        {
            ParameterName = "p_update_user",
            OracleDbType = OracleDbType.NVarchar2,
            Direction = ParameterDirection.Input,
            Value = config.UpdateUser ?? string.Empty
        });

        _ = await command.ExecuteNonQueryAsync();
        return config.RowId;
    }

    public async Task<bool> UpdateByKeyAsync(string configKey, string configValue, string updateUser)
    {
        using var connection = _dbContext.CreateConnection();
        await connection.OpenAsync();

        using var command = connection.CreateCommand();
        command.CommandType = CommandType.StoredProcedure;
        command.CommandText = "SP_SYS_TICKET_CONFIG_UPDATE_BY_KEY";

        _ = command.Parameters.Add(new OracleParameter
        {
            ParameterName = "p_config_key",
            OracleDbType = OracleDbType.NVarchar2,
            Direction = ParameterDirection.Input,
            Value = configKey
        });

        _ = command.Parameters.Add(new OracleParameter
        {
            ParameterName = "p_config_value",
            OracleDbType = OracleDbType.NClob,
            Direction = ParameterDirection.Input,
            Value = configValue
        });

        _ = command.Parameters.Add(new OracleParameter
        {
            ParameterName = "p_update_user",
            OracleDbType = OracleDbType.NVarchar2,
            Direction = ParameterDirection.Input,
            Value = updateUser
        });

        var rowsAffected = await command.ExecuteNonQueryAsync();
        return rowsAffected > 0;
    }

    public async Task<bool> DeleteAsync(Int64 rowId, string updateUser)
    {
        using var connection = _dbContext.CreateConnection();
        await connection.OpenAsync();

        using var command = connection.CreateCommand();
        command.CommandType = CommandType.StoredProcedure;
        command.CommandText = "SP_SYS_TICKET_CONFIG_DELETE";

        _ = command.Parameters.Add(new OracleParameter
        {
            ParameterName = "p_row_id",
            OracleDbType = OracleDbType.Decimal,
            Direction = ParameterDirection.Input,
            Value = rowId
        });

        _ = command.Parameters.Add(new OracleParameter
        {
            ParameterName = "p_update_user",
            OracleDbType = OracleDbType.NVarchar2,
            Direction = ParameterDirection.Input,
            Value = updateUser
        });

        var rowsAffected = await command.ExecuteNonQueryAsync();
        return rowsAffected > 0;
    }

    private SysTicketConfig MapToEntity(OracleDataReader reader)
    {
        return new SysTicketConfig
        {
            RowId = reader.GetInt64(reader.GetOrdinal("ROW_ID")),
            ConfigKey = reader.GetString(reader.GetOrdinal("CONFIG_KEY")),
            ConfigValue = reader.GetString(reader.GetOrdinal("CONFIG_VALUE")),
            ConfigType = reader.GetString(reader.GetOrdinal("CONFIG_TYPE")),
            DescriptionAr = reader.IsDBNull(reader.GetOrdinal("DESCRIPTION_AR")) ? null : reader.GetString(reader.GetOrdinal("DESCRIPTION_AR")),
            DescriptionEn = reader.IsDBNull(reader.GetOrdinal("DESCRIPTION_EN")) ? null : reader.GetString(reader.GetOrdinal("DESCRIPTION_EN")),
            IsActive = reader.GetString(reader.GetOrdinal("IS_ACTIVE")) == "Y",
            CreationUser = reader.GetString(reader.GetOrdinal("CREATION_USER")),
            CreationDate = reader.IsDBNull(reader.GetOrdinal("CREATION_DATE")) ? null : reader.GetDateTime(reader.GetOrdinal("CREATION_DATE")),
            UpdateUser = reader.IsDBNull(reader.GetOrdinal("UPDATE_USER")) ? null : reader.GetString(reader.GetOrdinal("UPDATE_USER")),
            UpdateDate = reader.IsDBNull(reader.GetOrdinal("UPDATE_DATE")) ? null : reader.GetDateTime(reader.GetOrdinal("UPDATE_DATE"))
        };
    }
}
