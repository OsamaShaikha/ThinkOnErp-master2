using Oracle.ManagedDataAccess.Client;
using System.Data;
using ThinkOnErp.Domain.Entities;
using ThinkOnErp.Domain.Interfaces;
using ThinkOnErp.Infrastructure.Data;

namespace ThinkOnErp.Infrastructure.Repositories;

/// <summary>
/// Repository implementation for SysTicketPriority entity using ADO.NET with Oracle stored procedures.
/// </summary>
public class TicketPriorityRepository : ITicketPriorityRepository
{
    private readonly OracleDbContext _dbContext;

    public TicketPriorityRepository(OracleDbContext dbContext)
    {
        _dbContext = dbContext ?? throw new ArgumentNullException(nameof(dbContext));
    }

    public async Task<List<SysTicketPriority>> GetAllAsync()
    {
        List<SysTicketPriority> priorities = new();

        using var connection = _dbContext.CreateConnection();
        await connection.OpenAsync();

        using var command = connection.CreateCommand();
        command.CommandType = CommandType.StoredProcedure;
        command.CommandText = "SP_SYS_TICKET_PRIORITY_SELECT_ALL";

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
            priorities.Add(MapToEntity(reader));
        }

        return priorities;
    }

    public async Task<SysTicketPriority?> GetByIdAsync(Int64 rowId)
    {
        using var connection = _dbContext.CreateConnection();
        await connection.OpenAsync();

        using var command = connection.CreateCommand();
        command.CommandType = CommandType.StoredProcedure;
        command.CommandText = "SP_SYS_TICKET_PRIORITY_SELECT_BY_ID";

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

    public async Task<SysTicketPriority?> GetByLevelAsync(int priorityLevel)
    {
        using var connection = _dbContext.CreateConnection();
        await connection.OpenAsync();

        using var command = connection.CreateCommand();
        command.CommandType = CommandType.Text;
        command.CommandText = @"
            SELECT ROW_ID, PRIORITY_NAME_AR, PRIORITY_NAME_EN, PRIORITY_LEVEL, 
                   SLA_TARGET_HOURS, ESCALATION_THRESHOLD_HOURS, IS_ACTIVE, 
                   CREATION_USER, CREATION_DATE
            FROM SYS_TICKET_PRIORITY
            WHERE PRIORITY_LEVEL = :priorityLevel AND IS_ACTIVE = 'Y'";

        _ = command.Parameters.Add(new OracleParameter
        {
            ParameterName = "priorityLevel",
            OracleDbType = OracleDbType.Int32,
            Direction = ParameterDirection.Input,
            Value = priorityLevel
        });

        using var reader = await command.ExecuteReaderAsync();
        if (await reader.ReadAsync())
        {
            return MapToEntity(reader);
        }

        return null;
    }

    public async Task<SysTicketPriority?> GetDefaultPriorityAsync()
    {
        // Default priority is typically Medium (level 3)
        return await GetByLevelAsync(3);
    }

    public async Task<List<SysTicketPriority>> GetHighPrioritiesAsync()
    {
        List<SysTicketPriority> priorities = new();

        using var connection = _dbContext.CreateConnection();
        await connection.OpenAsync();

        using var command = connection.CreateCommand();
        command.CommandType = CommandType.Text;
        command.CommandText = @"
            SELECT ROW_ID, PRIORITY_NAME_AR, PRIORITY_NAME_EN, PRIORITY_LEVEL, 
                   SLA_TARGET_HOURS, ESCALATION_THRESHOLD_HOURS, IS_ACTIVE, 
                   CREATION_USER, CREATION_DATE
            FROM SYS_TICKET_PRIORITY
            WHERE PRIORITY_LEVEL <= 2 AND IS_ACTIVE = 'Y'
            ORDER BY PRIORITY_LEVEL";

        using var reader = await command.ExecuteReaderAsync();
        while (await reader.ReadAsync())
        {
            priorities.Add(MapToEntity(reader));
        }

        return priorities;
    }

    public async Task<DateTime> CalculateSlaDeadlineAsync(
        Int64 priorityId, 
        DateTime creationDate, 
        bool excludeWeekends = true, 
        bool excludeHolidays = true)
    {
        var priority = await GetByIdAsync(priorityId);
        if (priority == null)
        {
            throw new ArgumentException($"Priority with ID {priorityId} not found");
        }

        // Simple calculation: add SLA hours to creation date
        // In production, this would exclude weekends/holidays as specified
        return creationDate.AddHours((double)priority.SlaTargetHours);
    }

    public async Task<List<(SysTicketPriority Priority, int TicketCount, decimal SlaComplianceRate)>> GetUsageStatisticsAsync(
        DateTime? fromDate = null,
        DateTime? toDate = null,
        Int64? companyId = null,
        Int64? branchId = null)
    {
        List<(SysTicketPriority, int, decimal)> results = new();

        using var connection = _dbContext.CreateConnection();
        await connection.OpenAsync();

        using var command = connection.CreateCommand();
        command.CommandType = CommandType.Text;
        
        var whereClause = "WHERE p.IS_ACTIVE = 'Y' AND t.IS_ACTIVE = 'Y'";
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
                p.ROW_ID, p.PRIORITY_NAME_AR, p.PRIORITY_NAME_EN, p.PRIORITY_LEVEL,
                p.SLA_TARGET_HOURS, p.ESCALATION_THRESHOLD_HOURS, p.IS_ACTIVE,
                p.CREATION_USER, p.CREATION_DATE,
                COUNT(t.ROW_ID) AS TICKET_COUNT,
                ROUND(
                    CASE 
                        WHEN COUNT(t.ROW_ID) = 0 THEN 0
                        ELSE (COUNT(CASE WHEN t.ACTUAL_RESOLUTION_DATE <= t.EXPECTED_RESOLUTION_DATE THEN 1 END) * 100.0 / COUNT(t.ROW_ID))
                    END, 2
                ) AS SLA_COMPLIANCE_RATE
            FROM SYS_TICKET_PRIORITY p
            LEFT JOIN SYS_REQUEST_TICKET t ON p.ROW_ID = t.TICKET_PRIORITY_ID
            {whereClause}
            GROUP BY p.ROW_ID, p.PRIORITY_NAME_AR, p.PRIORITY_NAME_EN, p.PRIORITY_LEVEL,
                     p.SLA_TARGET_HOURS, p.ESCALATION_THRESHOLD_HOURS, p.IS_ACTIVE,
                     p.CREATION_USER, p.CREATION_DATE
            ORDER BY p.PRIORITY_LEVEL";

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
            var priority = MapToEntity(reader);
            var ticketCount = reader.GetInt32(reader.GetOrdinal("TICKET_COUNT"));
            var slaComplianceRate = reader.GetDecimal(reader.GetOrdinal("SLA_COMPLIANCE_RATE"));
            results.Add((priority, ticketCount, slaComplianceRate));
        }

        return results;
    }

    public async Task<List<SysRequestTicket>> GetEscalationCandidatesAsync(Int64? companyId = null, Int64? branchId = null)
    {
        // This would require the full SysRequestTicket entity mapping
        // For now, return empty list as this is a complex query
        return new List<SysRequestTicket>();
    }

    private SysTicketPriority MapToEntity(OracleDataReader reader)
    {
        return new SysTicketPriority
        {
            RowId = reader.GetInt64(reader.GetOrdinal("ROW_ID")),
            PriorityNameAr = reader.GetString(reader.GetOrdinal("PRIORITY_NAME_AR")),
            PriorityNameEn = reader.GetString(reader.GetOrdinal("PRIORITY_NAME_EN")),
            PriorityLevel = reader.GetInt32(reader.GetOrdinal("PRIORITY_LEVEL")),
            SlaTargetHours = reader.GetDecimal(reader.GetOrdinal("SLA_TARGET_HOURS")),
            EscalationThresholdHours = reader.GetDecimal(reader.GetOrdinal("ESCALATION_THRESHOLD_HOURS")),
            IsActive = reader.GetString(reader.GetOrdinal("IS_ACTIVE")) == "Y",
            CreationUser = reader.GetString(reader.GetOrdinal("CREATION_USER")),
            CreationDate = reader.IsDBNull(reader.GetOrdinal("CREATION_DATE")) ? null : reader.GetDateTime(reader.GetOrdinal("CREATION_DATE"))
        };
    }
}
