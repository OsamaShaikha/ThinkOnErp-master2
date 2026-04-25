using Oracle.ManagedDataAccess.Client;
using System.Data;
using ThinkOnErp.Domain.Entities;
using ThinkOnErp.Domain.Interfaces;
using ThinkOnErp.Infrastructure.Data;

namespace ThinkOnErp.Infrastructure.Repositories;

/// <summary>
/// Repository implementation for SysTicketType entity using ADO.NET with Oracle stored procedures.
/// Implements ITicketTypeRepository interface from the Domain layer.
/// Uses OracleDbContext to create connections and maps Oracle data types to C# types.
/// </summary>
public class TicketTypeRepository : ITicketTypeRepository
{
    private readonly OracleDbContext _dbContext;

    /// <summary>
    /// Initializes a new instance of the TicketTypeRepository class.
    /// </summary>
    /// <param name="dbContext">The Oracle database context for connection management.</param>
    /// <exception cref="ArgumentNullException">Thrown when dbContext is null.</exception>
    public TicketTypeRepository(OracleDbContext dbContext)
    {
        _dbContext = dbContext ?? throw new ArgumentNullException(nameof(dbContext));
    }

    /// <summary>
    /// Retrieves all active ticket types from the database.
    /// Calls SP_SYS_TICKET_TYPE_SELECT_ALL stored procedure.
    /// </summary>
    /// <returns>A list of all active SysTicketType entities</returns>
    public async Task<List<SysTicketType>> GetAllAsync()
    {
        List<SysTicketType> ticketTypes = new();

        using (var connection = _dbContext.CreateConnection())
        {
            await connection.OpenAsync();

            using var command = connection.CreateCommand();
            command.CommandType = CommandType.StoredProcedure;
            command.CommandText = "SP_SYS_TICKET_TYPE_SELECT_ALL";

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
                ticketTypes.Add(MapToEntity(reader));
            }
        }

        return ticketTypes;
    }

    /// <summary>
    /// Retrieves a specific ticket type by its ID.
    /// Calls SP_SYS_TICKET_TYPE_SELECT_BY_ID stored procedure.
    /// </summary>
    /// <param name="rowId">The unique identifier of the ticket type</param>
    /// <returns>The SysTicketType entity if found, null otherwise</returns>
    public async Task<SysTicketType?> GetByIdAsync(Int64 rowId)
    {
        using var connection = _dbContext.CreateConnection();
        await connection.OpenAsync();

        using var command = connection.CreateCommand();
        command.CommandType = CommandType.StoredProcedure;
        command.CommandText = "SP_SYS_TICKET_TYPE_SELECT_BY_ID";

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
    /// Creates a new ticket type in the database.
    /// Calls SP_SYS_TICKET_TYPE_INSERT stored procedure.
    /// </summary>
    /// <param name="ticketType">The ticket type entity to create</param>
    /// <returns>The generated RowId from SEQ_SYS_TICKET_TYPE sequence</returns>
    public async Task<Int64> CreateAsync(SysTicketType ticketType)
    {
        using var connection = _dbContext.CreateConnection();
        await connection.OpenAsync();

        using var command = connection.CreateCommand();
        command.CommandType = CommandType.StoredProcedure;
        command.CommandText = "SP_SYS_TICKET_TYPE_INSERT";

        // Add input parameters
        _ = command.Parameters.Add(new OracleParameter
        {
            ParameterName = "P_TYPE_NAME_AR",
            OracleDbType = OracleDbType.NVarchar2,
            Direction = ParameterDirection.Input,
            Value = ticketType.TypeNameAr
        });

        _ = command.Parameters.Add(new OracleParameter
        {
            ParameterName = "P_TYPE_NAME_EN",
            OracleDbType = OracleDbType.NVarchar2,
            Direction = ParameterDirection.Input,
            Value = ticketType.TypeNameEn
        });

        _ = command.Parameters.Add(new OracleParameter
        {
            ParameterName = "P_DESCRIPTION_AR",
            OracleDbType = OracleDbType.NVarchar2,
            Direction = ParameterDirection.Input,
            Value = (object?)GetDescriptionAr(ticketType) ?? DBNull.Value
        });

        _ = command.Parameters.Add(new OracleParameter
        {
            ParameterName = "P_DESCRIPTION_EN",
            OracleDbType = OracleDbType.NVarchar2,
            Direction = ParameterDirection.Input,
            Value = (object?)GetDescriptionEn(ticketType) ?? DBNull.Value
        });

        _ = command.Parameters.Add(new OracleParameter
        {
            ParameterName = "P_DEFAULT_PRIORITY_ID",
            OracleDbType = OracleDbType.Decimal,
            Direction = ParameterDirection.Input,
            Value = ticketType.DefaultPriorityId
        });

        _ = command.Parameters.Add(new OracleParameter
        {
            ParameterName = "P_SLA_TARGET_HOURS",
            OracleDbType = OracleDbType.Decimal,
            Direction = ParameterDirection.Input,
            Value = ticketType.SlaTargetHours
        });

        _ = command.Parameters.Add(new OracleParameter
        {
            ParameterName = "P_CREATION_USER",
            OracleDbType = OracleDbType.NVarchar2,
            Direction = ParameterDirection.Input,
            Value = ticketType.CreationUser
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
        return long.Parse(newIdParam.Value.ToString()!);
    }

    /// <summary>
    /// Updates an existing ticket type in the database.
    /// Calls SP_SYS_TICKET_TYPE_UPDATE stored procedure.
    /// </summary>
    /// <param name="ticketType">The ticket type entity with updated values</param>
    /// <returns>The number of rows affected</returns>
    public async Task<Int64> UpdateAsync(SysTicketType ticketType)
    {
        using var connection = _dbContext.CreateConnection();
        await connection.OpenAsync();

        using var command = connection.CreateCommand();
        command.CommandType = CommandType.StoredProcedure;
        command.CommandText = "SP_SYS_TICKET_TYPE_UPDATE";

        // Add input parameters
        _ = command.Parameters.Add(new OracleParameter
        {
            ParameterName = "P_ROW_ID",
            OracleDbType = OracleDbType.Decimal,
            Direction = ParameterDirection.Input,
            Value = ticketType.RowId
        });

        _ = command.Parameters.Add(new OracleParameter
        {
            ParameterName = "P_TYPE_NAME_AR",
            OracleDbType = OracleDbType.NVarchar2,
            Direction = ParameterDirection.Input,
            Value = ticketType.TypeNameAr
        });

        _ = command.Parameters.Add(new OracleParameter
        {
            ParameterName = "P_TYPE_NAME_EN",
            OracleDbType = OracleDbType.NVarchar2,
            Direction = ParameterDirection.Input,
            Value = ticketType.TypeNameEn
        });

        _ = command.Parameters.Add(new OracleParameter
        {
            ParameterName = "P_DESCRIPTION_AR",
            OracleDbType = OracleDbType.NVarchar2,
            Direction = ParameterDirection.Input,
            Value = (object?)GetDescriptionAr(ticketType) ?? DBNull.Value
        });

        _ = command.Parameters.Add(new OracleParameter
        {
            ParameterName = "P_DESCRIPTION_EN",
            OracleDbType = OracleDbType.NVarchar2,
            Direction = ParameterDirection.Input,
            Value = (object?)GetDescriptionEn(ticketType) ?? DBNull.Value
        });

        _ = command.Parameters.Add(new OracleParameter
        {
            ParameterName = "P_DEFAULT_PRIORITY_ID",
            OracleDbType = OracleDbType.Decimal,
            Direction = ParameterDirection.Input,
            Value = ticketType.DefaultPriorityId
        });

        _ = command.Parameters.Add(new OracleParameter
        {
            ParameterName = "P_SLA_TARGET_HOURS",
            OracleDbType = OracleDbType.Decimal,
            Direction = ParameterDirection.Input,
            Value = ticketType.SlaTargetHours
        });

        _ = command.Parameters.Add(new OracleParameter
        {
            ParameterName = "P_UPDATE_USER",
            OracleDbType = OracleDbType.NVarchar2,
            Direction = ParameterDirection.Input,
            Value = ticketType.UpdateUser ?? string.Empty
        });

        return await command.ExecuteNonQueryAsync();
    }

    /// <summary>
    /// Performs a soft delete on a ticket type by setting IS_ACTIVE to false.
    /// Calls SP_SYS_TICKET_TYPE_DELETE stored procedure.
    /// Validates that no active tickets are using this type before deletion.
    /// </summary>
    /// <param name="rowId">The unique identifier of the ticket type to delete</param>
    /// <param name="userName">The username of the user performing the deletion</param>
    /// <returns>The number of rows affected</returns>
    public async Task<Int64> DeleteAsync(Int64 rowId, string userName)
    {
        using var connection = _dbContext.CreateConnection();
        await connection.OpenAsync();

        using var command = connection.CreateCommand();
        command.CommandType = CommandType.StoredProcedure;
        command.CommandText = "SP_SYS_TICKET_TYPE_DELETE";

        // Add input parameter for ROW_ID
        _ = command.Parameters.Add(new OracleParameter
        {
            ParameterName = "P_ROW_ID",
            OracleDbType = OracleDbType.Decimal,
            Direction = ParameterDirection.Input,
            Value = rowId
        });

        _ = command.Parameters.Add(new OracleParameter
        {
            ParameterName = "P_DELETE_USER",
            OracleDbType = OracleDbType.NVarchar2,
            Direction = ParameterDirection.Input,
            Value = userName
        });

        return await command.ExecuteNonQueryAsync();
    }

    /// <summary>
    /// Checks if a ticket type is being used by any active tickets.
    /// </summary>
    /// <param name="rowId">The unique identifier of the ticket type</param>
    /// <returns>True if the ticket type is in use, false otherwise</returns>
    public async Task<bool> IsInUseAsync(Int64 rowId)
    {
        using var connection = _dbContext.CreateConnection();
        await connection.OpenAsync();

        using var command = connection.CreateCommand();
        command.CommandType = CommandType.Text;
        command.CommandText = @"
            SELECT COUNT(*) 
            FROM SYS_REQUEST_TICKET 
            WHERE TICKET_TYPE_ID = :typeId AND IS_ACTIVE = 'Y'";

        _ = command.Parameters.Add(new OracleParameter
        {
            ParameterName = "typeId",
            OracleDbType = OracleDbType.Decimal,
            Direction = ParameterDirection.Input,
            Value = rowId
        });

        var result = await command.ExecuteScalarAsync();
        return Convert.ToInt32(result) > 0;
    }

    /// <summary>
    /// Retrieves ticket types ordered by usage frequency for analytics.
    /// </summary>
    /// <param name="fromDate">Optional date range start</param>
    /// <param name="toDate">Optional date range end</param>
    /// <returns>A list of ticket types with usage statistics</returns>
    public async Task<List<(SysTicketType TicketType, int TicketCount)>> GetByUsageAsync(DateTime? fromDate = null, DateTime? toDate = null)
    {
        List<(SysTicketType, int)> results = new();

        using var connection = _dbContext.CreateConnection();
        await connection.OpenAsync();

        using var command = connection.CreateCommand();
        command.CommandType = CommandType.Text;
        
        var whereClause = "WHERE tt.IS_ACTIVE = 'Y' AND t.IS_ACTIVE = 'Y'";
        if (fromDate.HasValue && toDate.HasValue)
        {
            whereClause += " AND t.CREATION_DATE >= :fromDate AND t.CREATION_DATE <= :toDate";
        }

        command.CommandText = $@"
            SELECT 
                tt.ROW_ID,
                tt.TYPE_NAME_AR,
                tt.TYPE_NAME_EN,
                tt.DEFAULT_PRIORITY_ID,
                tt.SLA_TARGET_HOURS,
                tt.IS_ACTIVE,
                tt.CREATION_USER,
                tt.CREATION_DATE,
                tt.UPDATE_USER,
                tt.UPDATE_DATE,
                COUNT(t.ROW_ID) AS TICKET_COUNT
            FROM SYS_TICKET_TYPE tt
            LEFT JOIN SYS_REQUEST_TICKET t ON tt.ROW_ID = t.TICKET_TYPE_ID
            {whereClause}
            GROUP BY tt.ROW_ID, tt.TYPE_NAME_AR, tt.TYPE_NAME_EN, tt.DEFAULT_PRIORITY_ID, 
                     tt.SLA_TARGET_HOURS, tt.IS_ACTIVE, tt.CREATION_USER, tt.CREATION_DATE, 
                     tt.UPDATE_USER, tt.UPDATE_DATE
            ORDER BY COUNT(t.ROW_ID) DESC, tt.TYPE_NAME_EN";

        if (fromDate.HasValue && toDate.HasValue)
        {
            _ = command.Parameters.Add(new OracleParameter
            {
                ParameterName = "fromDate",
                OracleDbType = OracleDbType.Date,
                Direction = ParameterDirection.Input,
                Value = fromDate.Value
            });

            _ = command.Parameters.Add(new OracleParameter
            {
                ParameterName = "toDate",
                OracleDbType = OracleDbType.Date,
                Direction = ParameterDirection.Input,
                Value = toDate.Value
            });
        }

        using var reader = await command.ExecuteReaderAsync();
        while (await reader.ReadAsync())
        {
            var ticketType = MapToEntityFromUsageQuery(reader);
            var ticketCount = reader.GetInt32(reader.GetOrdinal("TICKET_COUNT"));
            results.Add((ticketType, ticketCount));
        }

        return results;
    }

    /// <summary>
    /// Maps an OracleDataReader row to a SysTicketType entity.
    /// Handles Oracle data type conversions to C# types.
    /// </summary>
    /// <param name="reader">The OracleDataReader positioned at a row</param>
    /// <returns>A SysTicketType entity populated with data from the reader</returns>
    private SysTicketType MapToEntity(OracleDataReader reader)
    {
        return new SysTicketType
        {
            RowId = reader.GetInt64(reader.GetOrdinal("ROW_ID")),
            TypeNameAr = reader.GetString(reader.GetOrdinal("TYPE_NAME_AR")),
            TypeNameEn = reader.GetString(reader.GetOrdinal("TYPE_NAME_EN")),
            DescriptionAr = reader.IsDBNull(reader.GetOrdinal("DESCRIPTION_AR")) ? null : reader.GetString(reader.GetOrdinal("DESCRIPTION_AR")),
            DescriptionEn = reader.IsDBNull(reader.GetOrdinal("DESCRIPTION_EN")) ? null : reader.GetString(reader.GetOrdinal("DESCRIPTION_EN")),
            DefaultPriorityId = reader.GetInt64(reader.GetOrdinal("DEFAULT_PRIORITY_ID")),
            SlaTargetHours = reader.GetDecimal(reader.GetOrdinal("SLA_TARGET_HOURS")),
            IsActive = MapIsActiveToBoolean(reader.GetString(reader.GetOrdinal("IS_ACTIVE"))),
            CreationUser = reader.GetString(reader.GetOrdinal("CREATION_USER")),
            CreationDate = reader.IsDBNull(reader.GetOrdinal("CREATION_DATE")) ? null : reader.GetDateTime(reader.GetOrdinal("CREATION_DATE")),
            UpdateUser = reader.IsDBNull(reader.GetOrdinal("UPDATE_USER")) ? null : reader.GetString(reader.GetOrdinal("UPDATE_USER")),
            UpdateDate = reader.IsDBNull(reader.GetOrdinal("UPDATE_DATE")) ? null : reader.GetDateTime(reader.GetOrdinal("UPDATE_DATE"))
        };
    }

    /// <summary>
    /// Maps an OracleDataReader row to a SysTicketType entity from usage query.
    /// Handles Oracle data type conversions to C# types for usage statistics queries.
    /// </summary>
    /// <param name="reader">The OracleDataReader positioned at a row</param>
    /// <returns>A SysTicketType entity populated with data from the reader</returns>
    private SysTicketType MapToEntityFromUsageQuery(OracleDataReader reader)
    {
        return new SysTicketType
        {
            RowId = reader.GetInt64(reader.GetOrdinal("ROW_ID")),
            TypeNameAr = reader.GetString(reader.GetOrdinal("TYPE_NAME_AR")),
            TypeNameEn = reader.GetString(reader.GetOrdinal("TYPE_NAME_EN")),
            DescriptionAr = null, // Not included in usage query
            DescriptionEn = null, // Not included in usage query
            DefaultPriorityId = reader.GetInt64(reader.GetOrdinal("DEFAULT_PRIORITY_ID")),
            SlaTargetHours = reader.GetDecimal(reader.GetOrdinal("SLA_TARGET_HOURS")),
            IsActive = MapIsActiveToBoolean(reader.GetString(reader.GetOrdinal("IS_ACTIVE"))),
            CreationUser = reader.GetString(reader.GetOrdinal("CREATION_USER")),
            CreationDate = reader.IsDBNull(reader.GetOrdinal("CREATION_DATE")) ? null : reader.GetDateTime(reader.GetOrdinal("CREATION_DATE")),
            UpdateUser = reader.IsDBNull(reader.GetOrdinal("UPDATE_USER")) ? null : reader.GetString(reader.GetOrdinal("UPDATE_USER")),
            UpdateDate = reader.IsDBNull(reader.GetOrdinal("UPDATE_DATE")) ? null : reader.GetDateTime(reader.GetOrdinal("UPDATE_DATE"))
        };
    }

    /// <summary>
    /// Maps Oracle IS_ACTIVE values to C# boolean.
    /// Converts 'Y' or '1' to true, 'N' or '0' to false.
    /// </summary>
    /// <param name="value">The Oracle IS_ACTIVE value</param>
    /// <returns>True if value is 'Y' or '1', false otherwise</returns>
    private bool MapIsActiveToBoolean(string value)
    {
        return value is "Y" or "1";
    }

    /// <summary>
    /// Gets the description in Arabic from the ticket type entity.
    /// </summary>
    /// <param name="ticketType">The ticket type entity</param>
    /// <returns>Description in Arabic or null</returns>
    private string? GetDescriptionAr(SysTicketType ticketType)
    {
        return ticketType.DescriptionAr;
    }

    /// <summary>
    /// Gets the description in English from the ticket type entity.
    /// </summary>
    /// <param name="ticketType">The ticket type entity</param>
    /// <returns>Description in English or null</returns>
    private string? GetDescriptionEn(SysTicketType ticketType)
    {
        return ticketType.DescriptionEn;
    }
}