using Oracle.ManagedDataAccess.Client;
using System.Data;
using ThinkOnErp.Domain.Entities;
using ThinkOnErp.Domain.Interfaces;
using ThinkOnErp.Infrastructure.Data;

namespace ThinkOnErp.Infrastructure.Repositories;

/// <summary>
/// Repository implementation for SysTicketStatus entity using ADO.NET with Oracle stored procedures.
/// </summary>
public class TicketStatusRepository : ITicketStatusRepository
{
    private readonly OracleDbContext _dbContext;

    public TicketStatusRepository(OracleDbContext dbContext)
    {
        _dbContext = dbContext ?? throw new ArgumentNullException(nameof(dbContext));
    }

    public async Task<List<SysTicketStatus>> GetAllAsync()
    {
        List<SysTicketStatus> statuses = new();

        using var connection = _dbContext.CreateConnection();
        await connection.OpenAsync();

        using var command = connection.CreateCommand();
        command.CommandType = CommandType.StoredProcedure;
        command.CommandText = "SP_SYS_TICKET_STATUS_SELECT_ALL";

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
            statuses.Add(MapToEntity(reader));
        }

        return statuses;
    }

    public async Task<SysTicketStatus?> GetByIdAsync(Int64 rowId)
    {
        using var connection = _dbContext.CreateConnection();
        await connection.OpenAsync();

        using var command = connection.CreateCommand();
        command.CommandType = CommandType.StoredProcedure;
        command.CommandText = "SP_SYS_TICKET_STATUS_SELECT_BY_ID";

        _ = command.Parameters.Add(new OracleParameter
        {
            ParameterName = "P_ROW_ID",
            OracleDbType = OracleDbType.Decimal,
            Direction = ParameterDirection.Input,
            Value = rowId
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
            return MapToEntity(reader);
        }

        return null;
    }

    public async Task<SysTicketStatus?> GetByCodeAsync(string statusCode)
    {
        using var connection = _dbContext.CreateConnection();
        await connection.OpenAsync();

        using var command = connection.CreateCommand();
        command.CommandType = CommandType.Text;
        command.CommandText = @"
            SELECT ROW_ID, STATUS_NAME_AR, STATUS_NAME_EN, STATUS_CODE, 
                   DISPLAY_ORDER, IS_FINAL_STATUS, IS_ACTIVE, 
                   CREATION_USER, CREATION_DATE
            FROM SYS_TICKET_STATUS
            WHERE STATUS_CODE = :statusCode AND IS_ACTIVE = 'Y'";

        _ = command.Parameters.Add(new OracleParameter
        {
            ParameterName = "statusCode",
            OracleDbType = OracleDbType.NVarchar2,
            Direction = ParameterDirection.Input,
            Value = statusCode
        });

        using var reader = await command.ExecuteReaderAsync();
        if (await reader.ReadAsync())
        {
            return MapToEntity(reader);
        }

        return null;
    }

    public async Task<bool> IsTransitionAllowedAsync(Int64 fromStatusId, Int64 toStatusId)
    {
        var fromStatus = await GetByIdAsync(fromStatusId);
        var toStatus = await GetByIdAsync(toStatusId);

        if (fromStatus == null || toStatus == null)
        {
            return false;
        }

        // If from status is final, no transitions allowed
        if (fromStatus.IsFinalStatus)
        {
            return false;
        }

        // All other transitions are allowed
        return true;
    }

    public async Task<SysTicketStatus?> GetDefaultInitialStatusAsync()
    {
        // Default initial status is "OPEN"
        return await GetByCodeAsync("OPEN");
    }

    public async Task<List<SysTicketStatus>> GetFinalStatusesAsync()
    {
        List<SysTicketStatus> statuses = new();

        using var connection = _dbContext.CreateConnection();
        await connection.OpenAsync();

        using var command = connection.CreateCommand();
        command.CommandType = CommandType.Text;
        command.CommandText = @"
            SELECT ROW_ID, STATUS_NAME_AR, STATUS_NAME_EN, STATUS_CODE, 
                   DISPLAY_ORDER, IS_FINAL_STATUS, IS_ACTIVE, 
                   CREATION_USER, CREATION_DATE
            FROM SYS_TICKET_STATUS
            WHERE IS_FINAL_STATUS = 'Y' AND IS_ACTIVE = 'Y'
            ORDER BY DISPLAY_ORDER";

        using var reader = await command.ExecuteReaderAsync();
        while (await reader.ReadAsync())
        {
            statuses.Add(MapToEntity(reader));
        }

        return statuses;
    }

    public async Task<List<(SysTicketStatus Status, int TicketCount)>> GetUsageStatisticsAsync(
        DateTime? fromDate = null,
        DateTime? toDate = null,
        Int64? companyId = null,
        Int64? branchId = null)
    {
        List<(SysTicketStatus, int)> results = new();

        using var connection = _dbContext.CreateConnection();
        await connection.OpenAsync();

        using var command = connection.CreateCommand();
        command.CommandType = CommandType.Text;
        
        var whereClause = "WHERE s.IS_ACTIVE = 'Y' AND t.IS_ACTIVE = 'Y'";
        if (fromDate.HasValue && toDate.HasValue)
        {
            whereClause += " AND t.CREATION_DATE >= :fromDate AND t.CREATION_DATE <= :toDate";
        }
        if (companyId.HasValue)
        {
            whereClause += " AND t.COMPANY_ID = :companyId";
        }
        if (branchId.HasValue)
        {
            whereClause += " AND t.BRANCH_ID = :branchId";
        }

        command.CommandText = $@"
            SELECT 
                s.ROW_ID, s.STATUS_NAME_AR, s.STATUS_NAME_EN, s.STATUS_CODE,
                s.DISPLAY_ORDER, s.IS_FINAL_STATUS, s.IS_ACTIVE,
                s.CREATION_USER, s.CREATION_DATE,
                COUNT(t.ROW_ID) AS TICKET_COUNT
            FROM SYS_TICKET_STATUS s
            LEFT JOIN SYS_REQUEST_TICKET t ON s.ROW_ID = t.TICKET_STATUS_ID
            {whereClause}
            GROUP BY s.ROW_ID, s.STATUS_NAME_AR, s.STATUS_NAME_EN, s.STATUS_CODE,
                     s.DISPLAY_ORDER, s.IS_FINAL_STATUS, s.IS_ACTIVE,
                     s.CREATION_USER, s.CREATION_DATE
            ORDER BY s.DISPLAY_ORDER";

        if (fromDate.HasValue && toDate.HasValue)
        {
            _ = command.Parameters.Add(new OracleParameter("fromDate", OracleDbType.Date, fromDate.Value, ParameterDirection.Input));
            _ = command.Parameters.Add(new OracleParameter("toDate", OracleDbType.Date, toDate.Value, ParameterDirection.Input));
        }
        if (companyId.HasValue)
        {
            _ = command.Parameters.Add(new OracleParameter("companyId", OracleDbType.Decimal, companyId.Value, ParameterDirection.Input));
        }
        if (branchId.HasValue)
        {
            _ = command.Parameters.Add(new OracleParameter("branchId", OracleDbType.Decimal, branchId.Value, ParameterDirection.Input));
        }

        using var reader = await command.ExecuteReaderAsync();
        while (await reader.ReadAsync())
        {
            var status = MapToEntity(reader);
            var ticketCount = reader.GetInt32(reader.GetOrdinal("TICKET_COUNT"));
            results.Add((status, ticketCount));
        }

        return results;
    }

    private SysTicketStatus MapToEntity(OracleDataReader reader)
    {
        return new SysTicketStatus
        {
            RowId = reader.GetInt64(reader.GetOrdinal("ROW_ID")),
            StatusNameAr = reader.GetString(reader.GetOrdinal("STATUS_NAME_AR")),
            StatusNameEn = reader.GetString(reader.GetOrdinal("STATUS_NAME_EN")),
            StatusCode = reader.GetString(reader.GetOrdinal("STATUS_CODE")),
            DisplayOrder = reader.GetInt32(reader.GetOrdinal("DISPLAY_ORDER")),
            IsFinalStatus = reader.GetString(reader.GetOrdinal("IS_FINAL_STATUS")) == "Y",
            IsActive = reader.GetString(reader.GetOrdinal("IS_ACTIVE")) == "Y",
            CreationUser = reader.GetString(reader.GetOrdinal("CREATION_USER")),
            CreationDate = reader.IsDBNull(reader.GetOrdinal("CREATION_DATE")) ? null : reader.GetDateTime(reader.GetOrdinal("CREATION_DATE"))
        };
    }
}
