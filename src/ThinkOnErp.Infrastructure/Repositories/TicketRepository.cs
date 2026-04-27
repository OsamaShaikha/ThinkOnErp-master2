using Oracle.ManagedDataAccess.Client;
using Oracle.ManagedDataAccess.Types;
using System.Data;
using ThinkOnErp.Domain.Entities;
using ThinkOnErp.Domain.Interfaces;
using ThinkOnErp.Infrastructure.Data;

namespace ThinkOnErp.Infrastructure.Repositories;

/// <summary>
/// Repository implementation for SysRequestTicket entity using ADO.NET with Oracle stored procedures.
/// Implements ITicketRepository interface from the Domain layer.
/// Uses OracleDbContext to create connections and maps Oracle data types to C# types.
/// Follows existing ThinkOnERP patterns for consistency and maintainability.
/// </summary>
public class TicketRepository : ITicketRepository
{
    private readonly OracleDbContext _dbContext;

    /// <summary>
    /// Initializes a new instance of the TicketRepository class.
    /// </summary>
    /// <param name="dbContext">The Oracle database context for connection management.</param>
    /// <exception cref="ArgumentNullException">Thrown when dbContext is null.</exception>
    public TicketRepository(OracleDbContext dbContext)
    {
        _dbContext = dbContext ?? throw new ArgumentNullException(nameof(dbContext));
    }

    /// <summary>
    /// Retrieves all active tickets with optional filtering and pagination.
    /// Calls SP_SYS_REQUEST_TICKET_SELECT_ALL stored procedure.
    /// </summary>
    public async Task<(List<SysRequestTicket> Tickets, int TotalCount)> GetAllAsync(
        Int64? companyId = null,
        Int64? branchId = null,
        Int64? assigneeId = null,
        Int64? statusId = null,
        Int64? priorityId = null,
        Int64? typeId = null,
        string? searchTerm = null,
        DateTime? createdFrom = null,
        DateTime? createdTo = null,
        int page = 1,
        int pageSize = 20,
        string sortBy = "CreationDate",
        string sortDirection = "DESC")
    {
        List<SysRequestTicket> tickets = new();
        int totalCount = 0;

        using var connection = _dbContext.CreateConnection();
        await connection.OpenAsync();

        using var command = connection.CreateCommand();
        command.CommandType = CommandType.StoredProcedure;
        command.CommandText = "SP_SYS_REQUEST_TICKET_SELECT_ALL";

        // Add input parameters
        _ = command.Parameters.Add(new OracleParameter
        {
            ParameterName = "P_COMPANY_ID",
            OracleDbType = OracleDbType.Decimal,
            Direction = ParameterDirection.Input,
            Value = companyId ?? 0
        });

        _ = command.Parameters.Add(new OracleParameter
        {
            ParameterName = "P_BRANCH_ID",
            OracleDbType = OracleDbType.Decimal,
            Direction = ParameterDirection.Input,
            Value = branchId ?? 0
        });

        _ = command.Parameters.Add(new OracleParameter
        {
            ParameterName = "P_ASSIGNEE_ID",
            OracleDbType = OracleDbType.Decimal,
            Direction = ParameterDirection.Input,
            Value = assigneeId ?? 0
        });

        _ = command.Parameters.Add(new OracleParameter
        {
            ParameterName = "P_STATUS_ID",
            OracleDbType = OracleDbType.Decimal,
            Direction = ParameterDirection.Input,
            Value = statusId ?? 0
        });

        _ = command.Parameters.Add(new OracleParameter
        {
            ParameterName = "P_PRIORITY_ID",
            OracleDbType = OracleDbType.Decimal,
            Direction = ParameterDirection.Input,
            Value = priorityId ?? 0
        });

        _ = command.Parameters.Add(new OracleParameter
        {
            ParameterName = "P_TYPE_ID",
            OracleDbType = OracleDbType.Decimal,
            Direction = ParameterDirection.Input,
            Value = typeId ?? 0
        });

        _ = command.Parameters.Add(new OracleParameter
        {
            ParameterName = "P_SEARCH_TERM",
            OracleDbType = OracleDbType.NVarchar2,
            Direction = ParameterDirection.Input,
            Value = (object?)searchTerm ?? DBNull.Value
        });

        _ = command.Parameters.Add(new OracleParameter
        {
            ParameterName = "P_PAGE_NUMBER",
            OracleDbType = OracleDbType.Decimal,
            Direction = ParameterDirection.Input,
            Value = page
        });

        _ = command.Parameters.Add(new OracleParameter
        {
            ParameterName = "P_PAGE_SIZE",
            OracleDbType = OracleDbType.Decimal,
            Direction = ParameterDirection.Input,
            Value = pageSize
        });

        _ = command.Parameters.Add(new OracleParameter
        {
            ParameterName = "P_SORT_BY",
            OracleDbType = OracleDbType.Varchar2,
            Direction = ParameterDirection.Input,
            Value = sortBy
        });

        _ = command.Parameters.Add(new OracleParameter
        {
            ParameterName = "P_SORT_DIRECTION",
            OracleDbType = OracleDbType.Varchar2,
            Direction = ParameterDirection.Input,
            Value = sortDirection
        });

        // Add output parameters
        OracleParameter cursorParam = new()
        {
            ParameterName = "P_RESULT_CURSOR",
            OracleDbType = OracleDbType.RefCursor,
            Direction = ParameterDirection.Output
        };
        _ = command.Parameters.Add(cursorParam);

        OracleParameter totalCountParam = new()
        {
            ParameterName = "P_TOTAL_COUNT",
            OracleDbType = OracleDbType.Decimal,
            Direction = ParameterDirection.Output
        };
        _ = command.Parameters.Add(totalCountParam);

        using var reader = await command.ExecuteReaderAsync();
        while (await reader.ReadAsync())
        {
            tickets.Add(MapToEntity(reader));
        }

        // Get total count from output parameter
        totalCount = Convert.ToInt32(((OracleDecimal)totalCountParam.Value).Value);

        return (tickets, totalCount);
    }

    /// <summary>
    /// Retrieves a specific ticket by its ID with full navigation properties.
    /// Calls SP_SYS_REQUEST_TICKET_SELECT_BY_ID stored procedure.
    /// </summary>
    public async Task<SysRequestTicket?> GetByIdAsync(Int64 rowId)
    {
        using var connection = _dbContext.CreateConnection();
        await connection.OpenAsync();

        using var command = connection.CreateCommand();
        command.CommandType = CommandType.StoredProcedure;
        command.CommandText = "SP_SYS_REQUEST_TICKET_SELECT_BY_ID";

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
            return MapToDetailEntity(reader);
        }

        return null;
    }

    /// <summary>
    /// Creates a new ticket in the database with SLA calculation.
    /// Calls SP_SYS_REQUEST_TICKET_INSERT stored procedure.
    /// </summary>
    public async Task<Int64> CreateAsync(SysRequestTicket ticket)
    {
        using var connection = _dbContext.CreateConnection();
        await connection.OpenAsync();

        using var command = connection.CreateCommand();
        command.CommandType = CommandType.StoredProcedure;
        command.CommandText = "SP_SYS_REQUEST_TICKET_INSERT";

        // Add input parameters
        _ = command.Parameters.Add(new OracleParameter
        {
            ParameterName = "P_TITLE_AR",
            OracleDbType = OracleDbType.NVarchar2,
            Direction = ParameterDirection.Input,
            Value = ticket.TitleAr
        });

        _ = command.Parameters.Add(new OracleParameter
        {
            ParameterName = "P_TITLE_EN",
            OracleDbType = OracleDbType.NVarchar2,
            Direction = ParameterDirection.Input,
            Value = ticket.TitleEn
        });

        _ = command.Parameters.Add(new OracleParameter
        {
            ParameterName = "P_DESCRIPTION",
            OracleDbType = OracleDbType.NClob,
            Direction = ParameterDirection.Input,
            Value = ticket.Description
        });

        _ = command.Parameters.Add(new OracleParameter
        {
            ParameterName = "P_COMPANY_ID",
            OracleDbType = OracleDbType.Decimal,
            Direction = ParameterDirection.Input,
            Value = ticket.CompanyId
        });

        _ = command.Parameters.Add(new OracleParameter
        {
            ParameterName = "P_BRANCH_ID",
            OracleDbType = OracleDbType.Decimal,
            Direction = ParameterDirection.Input,
            Value = ticket.BranchId
        });

        _ = command.Parameters.Add(new OracleParameter
        {
            ParameterName = "P_REQUESTER_ID",
            OracleDbType = OracleDbType.Decimal,
            Direction = ParameterDirection.Input,
            Value = ticket.RequesterId
        });

        _ = command.Parameters.Add(new OracleParameter
        {
            ParameterName = "P_ASSIGNEE_ID",
            OracleDbType = OracleDbType.Decimal,
            Direction = ParameterDirection.Input,
            Value = (object?)ticket.AssigneeId ?? DBNull.Value
        });

        _ = command.Parameters.Add(new OracleParameter
        {
            ParameterName = "P_TICKET_TYPE_ID",
            OracleDbType = OracleDbType.Decimal,
            Direction = ParameterDirection.Input,
            Value = ticket.TicketTypeId
        });

        _ = command.Parameters.Add(new OracleParameter
        {
            ParameterName = "P_TICKET_PRIORITY_ID",
            OracleDbType = OracleDbType.Decimal,
            Direction = ParameterDirection.Input,
            Value = ticket.TicketPriorityId
        });

        _ = command.Parameters.Add(new OracleParameter
        {
            ParameterName = "P_TICKET_CATEGORY_ID",
            OracleDbType = OracleDbType.Decimal,
            Direction = ParameterDirection.Input,
            Value = (object?)ticket.TicketCategoryId ?? DBNull.Value
        });

        _ = command.Parameters.Add(new OracleParameter
        {
            ParameterName = "P_CREATION_USER",
            OracleDbType = OracleDbType.NVarchar2,
            Direction = ParameterDirection.Input,
            Value = ticket.CreationUser
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
        return Convert.ToInt64(((OracleDecimal)newIdParam.Value).Value);
    }

    /// <summary>
    /// Updates an existing ticket in the database with audit trail.
    /// Calls SP_SYS_REQUEST_TICKET_UPDATE stored procedure.
    /// </summary>
    public async Task<Int64> UpdateAsync(SysRequestTicket ticket)
    {
        using var connection = _dbContext.CreateConnection();
        await connection.OpenAsync();

        using var command = connection.CreateCommand();
        command.CommandType = CommandType.StoredProcedure;
        command.CommandText = "SP_SYS_REQUEST_TICKET_UPDATE";

        // Add input parameters
        _ = command.Parameters.Add(new OracleParameter
        {
            ParameterName = "P_ROW_ID",
            OracleDbType = OracleDbType.Decimal,
            Direction = ParameterDirection.Input,
            Value = ticket.RowId
        });

        _ = command.Parameters.Add(new OracleParameter
        {
            ParameterName = "P_TITLE_AR",
            OracleDbType = OracleDbType.NVarchar2,
            Direction = ParameterDirection.Input,
            Value = ticket.TitleAr
        });

        _ = command.Parameters.Add(new OracleParameter
        {
            ParameterName = "P_TITLE_EN",
            OracleDbType = OracleDbType.NVarchar2,
            Direction = ParameterDirection.Input,
            Value = ticket.TitleEn
        });

        _ = command.Parameters.Add(new OracleParameter
        {
            ParameterName = "P_DESCRIPTION",
            OracleDbType = OracleDbType.NClob,
            Direction = ParameterDirection.Input,
            Value = ticket.Description
        });

        _ = command.Parameters.Add(new OracleParameter
        {
            ParameterName = "P_ASSIGNEE_ID",
            OracleDbType = OracleDbType.Decimal,
            Direction = ParameterDirection.Input,
            Value = (object?)ticket.AssigneeId ?? DBNull.Value
        });

        _ = command.Parameters.Add(new OracleParameter
        {
            ParameterName = "P_TICKET_TYPE_ID",
            OracleDbType = OracleDbType.Decimal,
            Direction = ParameterDirection.Input,
            Value = ticket.TicketTypeId
        });

        _ = command.Parameters.Add(new OracleParameter
        {
            ParameterName = "P_TICKET_PRIORITY_ID",
            OracleDbType = OracleDbType.Decimal,
            Direction = ParameterDirection.Input,
            Value = ticket.TicketPriorityId
        });

        _ = command.Parameters.Add(new OracleParameter
        {
            ParameterName = "P_TICKET_CATEGORY_ID",
            OracleDbType = OracleDbType.Decimal,
            Direction = ParameterDirection.Input,
            Value = (object?)ticket.TicketCategoryId ?? DBNull.Value
        });

        _ = command.Parameters.Add(new OracleParameter
        {
            ParameterName = "P_UPDATE_USER",
            OracleDbType = OracleDbType.NVarchar2,
            Direction = ParameterDirection.Input,
            Value = ticket.UpdateUser ?? string.Empty
        });

        return await command.ExecuteNonQueryAsync();
    }

    /// <summary>
    /// Performs a soft delete on a ticket by setting IS_ACTIVE to false.
    /// Calls SP_SYS_REQUEST_TICKET_DELETE stored procedure.
    /// </summary>
    public async Task<Int64> DeleteAsync(Int64 rowId, string userName)
    {
        using var connection = _dbContext.CreateConnection();
        await connection.OpenAsync();

        using var command = connection.CreateCommand();
        command.CommandType = CommandType.StoredProcedure;
        command.CommandText = "SP_SYS_REQUEST_TICKET_DELETE";

        // Add input parameters
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
    /// Assigns a ticket to a support staff member.
    /// Calls SP_SYS_REQUEST_TICKET_ASSIGN stored procedure.
    /// </summary>
    public async Task<Int64> AssignTicketAsync(Int64 ticketId, Int64? assigneeId, string userName)
    {
        using var connection = _dbContext.CreateConnection();
        await connection.OpenAsync();

        using var command = connection.CreateCommand();
        command.CommandType = CommandType.StoredProcedure;
        command.CommandText = "SP_SYS_REQUEST_TICKET_ASSIGN";

        // Add input parameters
        _ = command.Parameters.Add(new OracleParameter
        {
            ParameterName = "P_ROW_ID",
            OracleDbType = OracleDbType.Decimal,
            Direction = ParameterDirection.Input,
            Value = ticketId
        });

        _ = command.Parameters.Add(new OracleParameter
        {
            ParameterName = "P_ASSIGNEE_ID",
            OracleDbType = OracleDbType.Decimal,
            Direction = ParameterDirection.Input,
            Value = (object?)assigneeId ?? DBNull.Value
        });

        _ = command.Parameters.Add(new OracleParameter
        {
            ParameterName = "P_ASSIGNMENT_REASON",
            OracleDbType = OracleDbType.NVarchar2,
            Direction = ParameterDirection.Input,
            Value = DBNull.Value // Optional parameter
        });

        _ = command.Parameters.Add(new OracleParameter
        {
            ParameterName = "P_UPDATE_USER",
            OracleDbType = OracleDbType.NVarchar2,
            Direction = ParameterDirection.Input,
            Value = userName
        });

        return await command.ExecuteNonQueryAsync();
    }

    /// <summary>
    /// Updates the status of a ticket with workflow validation.
    /// Calls SP_SYS_REQUEST_TICKET_UPDATE_STATUS stored procedure.
    /// </summary>
    public async Task<Int64> UpdateStatusAsync(Int64 ticketId, Int64 newStatusId, string? statusChangeReason, string userName)
    {
        using var connection = _dbContext.CreateConnection();
        await connection.OpenAsync();

        using var command = connection.CreateCommand();
        command.CommandType = CommandType.StoredProcedure;
        command.CommandText = "SP_SYS_REQUEST_TICKET_UPDATE_STATUS";

        // Add input parameters
        _ = command.Parameters.Add(new OracleParameter
        {
            ParameterName = "P_ROW_ID",
            OracleDbType = OracleDbType.Decimal,
            Direction = ParameterDirection.Input,
            Value = ticketId
        });

        _ = command.Parameters.Add(new OracleParameter
        {
            ParameterName = "P_NEW_STATUS_ID",
            OracleDbType = OracleDbType.Decimal,
            Direction = ParameterDirection.Input,
            Value = newStatusId
        });

        _ = command.Parameters.Add(new OracleParameter
        {
            ParameterName = "P_STATUS_CHANGE_REASON",
            OracleDbType = OracleDbType.NVarchar2,
            Direction = ParameterDirection.Input,
            Value = (object?)statusChangeReason ?? DBNull.Value
        });

        _ = command.Parameters.Add(new OracleParameter
        {
            ParameterName = "P_UPDATE_USER",
            OracleDbType = OracleDbType.NVarchar2,
            Direction = ParameterDirection.Input,
            Value = userName
        });

        return await command.ExecuteNonQueryAsync();
    }

    /// <summary>
    /// Retrieves tickets that are overdue based on SLA targets.
    /// Uses direct SQL query for overdue ticket identification.
    /// </summary>
    public async Task<List<SysRequestTicket>> GetOverdueTicketsAsync(Int64? companyId = null, Int64? branchId = null)
    {
        List<SysRequestTicket> tickets = new();

        using var connection = _dbContext.CreateConnection();
        await connection.OpenAsync();

        using var command = connection.CreateCommand();
        command.CommandType = CommandType.Text;
        
        var whereClause = "WHERE t.IS_ACTIVE = 'Y' AND t.EXPECTED_RESOLUTION_DATE < SYSDATE AND t.ACTUAL_RESOLUTION_DATE IS NULL";
        
        if (companyId.HasValue && companyId > 0)
        {
            whereClause += " AND t.COMPANY_ID = :companyId";
        }
        
        if (branchId.HasValue && branchId > 0)
        {
            whereClause += " AND t.BRANCH_ID = :branchId";
        }

        command.CommandText = $@"
            SELECT 
                t.ROW_ID,
                t.TITLE_AR,
                t.TITLE_EN,
                t.DESCRIPTION,
                t.COMPANY_ID,
                c.ROW_DESC_E AS COMPANY_NAME,
                t.BRANCH_ID,
                b.ROW_DESC_E AS BRANCH_NAME,
                t.REQUESTER_ID,
                req.ROW_DESC_E AS REQUESTER_NAME,
                t.ASSIGNEE_ID,
                ass.ROW_DESC_E AS ASSIGNEE_NAME,
                t.TICKET_TYPE_ID,
                tt.TYPE_NAME_EN AS TYPE_NAME,
                t.TICKET_STATUS_ID,
                st.STATUS_NAME_EN AS STATUS_NAME,
                st.STATUS_CODE,
                t.TICKET_PRIORITY_ID,
                pr.PRIORITY_NAME_EN AS PRIORITY_NAME,
                pr.PRIORITY_LEVEL,
                t.TICKET_CATEGORY_ID,
                cat.CATEGORY_NAME_EN AS CATEGORY_NAME,
                t.EXPECTED_RESOLUTION_DATE,
                t.ACTUAL_RESOLUTION_DATE,
                t.IS_ACTIVE,
                t.CREATION_USER,
                t.CREATION_DATE,
                t.UPDATE_USER,
                t.UPDATE_DATE,
                'Overdue' AS SLA_STATUS
            FROM SYS_REQUEST_TICKET t
            LEFT JOIN SYS_COMPANY c ON t.COMPANY_ID = c.ROW_ID
            LEFT JOIN SYS_BRANCH b ON t.BRANCH_ID = b.ROW_ID
            LEFT JOIN SYS_USERS req ON t.REQUESTER_ID = req.ROW_ID
            LEFT JOIN SYS_USERS ass ON t.ASSIGNEE_ID = ass.ROW_ID
            LEFT JOIN SYS_TICKET_TYPE tt ON t.TICKET_TYPE_ID = tt.ROW_ID
            LEFT JOIN SYS_TICKET_STATUS st ON t.TICKET_STATUS_ID = st.ROW_ID
            LEFT JOIN SYS_TICKET_PRIORITY pr ON t.TICKET_PRIORITY_ID = pr.ROW_ID
            LEFT JOIN SYS_TICKET_CATEGORY cat ON t.TICKET_CATEGORY_ID = cat.ROW_ID
            {whereClause}
            ORDER BY t.EXPECTED_RESOLUTION_DATE ASC";

        if (companyId.HasValue && companyId > 0)
        {
            _ = command.Parameters.Add(new OracleParameter("companyId", companyId.Value));
        }
        
        if (branchId.HasValue && branchId > 0)
        {
            _ = command.Parameters.Add(new OracleParameter("branchId", branchId.Value));
        }

        using var reader = await command.ExecuteReaderAsync();
        while (await reader.ReadAsync())
        {
            tickets.Add(MapToEntity(reader));
        }

        return tickets;
    }

    /// <summary>
    /// Retrieves tickets approaching SLA deadline for escalation alerts.
    /// Uses direct SQL query for escalation candidate identification.
    /// </summary>
    public async Task<List<SysRequestTicket>> GetTicketsForEscalationAsync(int hoursBeforeDeadline = 2, Int64? companyId = null, Int64? branchId = null)
    {
        List<SysRequestTicket> tickets = new();

        using var connection = _dbContext.CreateConnection();
        await connection.OpenAsync();

        using var command = connection.CreateCommand();
        command.CommandType = CommandType.Text;
        
        var whereClause = @"WHERE t.IS_ACTIVE = 'Y' 
                           AND t.ACTUAL_RESOLUTION_DATE IS NULL 
                           AND t.EXPECTED_RESOLUTION_DATE > SYSDATE 
                           AND t.EXPECTED_RESOLUTION_DATE <= SYSDATE + (:hoursBeforeDeadline / 24)";
        
        if (companyId.HasValue && companyId > 0)
        {
            whereClause += " AND t.COMPANY_ID = :companyId";
        }
        
        if (branchId.HasValue && branchId > 0)
        {
            whereClause += " AND t.BRANCH_ID = :branchId";
        }

        command.CommandText = $@"
            SELECT 
                t.ROW_ID,
                t.TITLE_AR,
                t.TITLE_EN,
                t.DESCRIPTION,
                t.COMPANY_ID,
                c.ROW_DESC_E AS COMPANY_NAME,
                t.BRANCH_ID,
                b.ROW_DESC_E AS BRANCH_NAME,
                t.REQUESTER_ID,
                req.ROW_DESC_E AS REQUESTER_NAME,
                t.ASSIGNEE_ID,
                ass.ROW_DESC_E AS ASSIGNEE_NAME,
                t.TICKET_TYPE_ID,
                tt.TYPE_NAME_EN AS TYPE_NAME,
                t.TICKET_STATUS_ID,
                st.STATUS_NAME_EN AS STATUS_NAME,
                st.STATUS_CODE,
                t.TICKET_PRIORITY_ID,
                pr.PRIORITY_NAME_EN AS PRIORITY_NAME,
                pr.PRIORITY_LEVEL,
                t.TICKET_CATEGORY_ID,
                cat.CATEGORY_NAME_EN AS CATEGORY_NAME,
                t.EXPECTED_RESOLUTION_DATE,
                t.ACTUAL_RESOLUTION_DATE,
                t.IS_ACTIVE,
                t.CREATION_USER,
                t.CREATION_DATE,
                t.UPDATE_USER,
                t.UPDATE_DATE,
                'At Risk' AS SLA_STATUS
            FROM SYS_REQUEST_TICKET t
            LEFT JOIN SYS_COMPANY c ON t.COMPANY_ID = c.ROW_ID
            LEFT JOIN SYS_BRANCH b ON t.BRANCH_ID = b.ROW_ID
            LEFT JOIN SYS_USERS req ON t.REQUESTER_ID = req.ROW_ID
            LEFT JOIN SYS_USERS ass ON t.ASSIGNEE_ID = ass.ROW_ID
            LEFT JOIN SYS_TICKET_TYPE tt ON t.TICKET_TYPE_ID = tt.ROW_ID
            LEFT JOIN SYS_TICKET_STATUS st ON t.TICKET_STATUS_ID = st.ROW_ID
            LEFT JOIN SYS_TICKET_PRIORITY pr ON t.TICKET_PRIORITY_ID = pr.ROW_ID
            LEFT JOIN SYS_TICKET_CATEGORY cat ON t.TICKET_CATEGORY_ID = cat.ROW_ID
            {whereClause}
            ORDER BY t.EXPECTED_RESOLUTION_DATE ASC";

        _ = command.Parameters.Add(new OracleParameter("hoursBeforeDeadline", hoursBeforeDeadline));
        
        if (companyId.HasValue && companyId > 0)
        {
            _ = command.Parameters.Add(new OracleParameter("companyId", companyId.Value));
        }
        
        if (branchId.HasValue && branchId > 0)
        {
            _ = command.Parameters.Add(new OracleParameter("branchId", branchId.Value));
        }

        using var reader = await command.ExecuteReaderAsync();
        while (await reader.ReadAsync())
        {
            tickets.Add(MapToEntity(reader));
        }

        return tickets;
    }

    /// <summary>
    /// Retrieves tickets assigned to a specific user.
    /// </summary>
    public async Task<List<SysRequestTicket>> GetTicketsByAssigneeAsync(Int64 assigneeId, bool includeResolved = false)
    {
        List<SysRequestTicket> tickets = new();

        using var connection = _dbContext.CreateConnection();
        await connection.OpenAsync();

        using var command = connection.CreateCommand();
        command.CommandType = CommandType.Text;
        
        var whereClause = "WHERE t.IS_ACTIVE = 'Y' AND t.ASSIGNEE_ID = :assigneeId";
        
        if (!includeResolved)
        {
            whereClause += " AND t.ACTUAL_RESOLUTION_DATE IS NULL";
        }

        command.CommandText = $@"
            SELECT 
                t.ROW_ID,
                t.TITLE_AR,
                t.TITLE_EN,
                t.DESCRIPTION,
                t.COMPANY_ID,
                c.ROW_DESC_E AS COMPANY_NAME,
                t.BRANCH_ID,
                b.ROW_DESC_E AS BRANCH_NAME,
                t.REQUESTER_ID,
                req.ROW_DESC_E AS REQUESTER_NAME,
                t.ASSIGNEE_ID,
                ass.ROW_DESC_E AS ASSIGNEE_NAME,
                t.TICKET_TYPE_ID,
                tt.TYPE_NAME_EN AS TYPE_NAME,
                t.TICKET_STATUS_ID,
                st.STATUS_NAME_EN AS STATUS_NAME,
                st.STATUS_CODE,
                t.TICKET_PRIORITY_ID,
                pr.PRIORITY_NAME_EN AS PRIORITY_NAME,
                pr.PRIORITY_LEVEL,
                t.TICKET_CATEGORY_ID,
                cat.CATEGORY_NAME_EN AS CATEGORY_NAME,
                t.EXPECTED_RESOLUTION_DATE,
                t.ACTUAL_RESOLUTION_DATE,
                t.IS_ACTIVE,
                t.CREATION_USER,
                t.CREATION_DATE,
                t.UPDATE_USER,
                t.UPDATE_DATE,
                CASE 
                    WHEN t.ACTUAL_RESOLUTION_DATE IS NOT NULL THEN 'Resolved'
                    WHEN t.EXPECTED_RESOLUTION_DATE < SYSDATE THEN 'Overdue'
                    WHEN t.EXPECTED_RESOLUTION_DATE < SYSDATE + (pr.ESCALATION_THRESHOLD_HOURS / 24) THEN 'At Risk'
                    ELSE 'On Time'
                END AS SLA_STATUS
            FROM SYS_REQUEST_TICKET t
            LEFT JOIN SYS_COMPANY c ON t.COMPANY_ID = c.ROW_ID
            LEFT JOIN SYS_BRANCH b ON t.BRANCH_ID = b.ROW_ID
            LEFT JOIN SYS_USERS req ON t.REQUESTER_ID = req.ROW_ID
            LEFT JOIN SYS_USERS ass ON t.ASSIGNEE_ID = ass.ROW_ID
            LEFT JOIN SYS_TICKET_TYPE tt ON t.TICKET_TYPE_ID = tt.ROW_ID
            LEFT JOIN SYS_TICKET_STATUS st ON t.TICKET_STATUS_ID = st.ROW_ID
            LEFT JOIN SYS_TICKET_PRIORITY pr ON t.TICKET_PRIORITY_ID = pr.ROW_ID
            LEFT JOIN SYS_TICKET_CATEGORY cat ON t.TICKET_CATEGORY_ID = cat.ROW_ID
            {whereClause}
            ORDER BY pr.PRIORITY_LEVEL ASC, t.CREATION_DATE DESC";

        _ = command.Parameters.Add(new OracleParameter("assigneeId", assigneeId));

        using var reader = await command.ExecuteReaderAsync();
        while (await reader.ReadAsync())
        {
            tickets.Add(MapToEntity(reader));
        }

        return tickets;
    }

    /// <summary>
    /// Retrieves tickets created by a specific user.
    /// </summary>
    public async Task<List<SysRequestTicket>> GetTicketsByRequesterAsync(Int64 requesterId, Int64 companyId, Int64 branchId)
    {
        List<SysRequestTicket> tickets = new();

        using var connection = _dbContext.CreateConnection();
        await connection.OpenAsync();

        using var command = connection.CreateCommand();
        command.CommandType = CommandType.Text;
        command.CommandText = @"
            SELECT 
                t.ROW_ID,
                t.TITLE_AR,
                t.TITLE_EN,
                t.DESCRIPTION,
                t.COMPANY_ID,
                c.ROW_DESC_E AS COMPANY_NAME,
                t.BRANCH_ID,
                b.ROW_DESC_E AS BRANCH_NAME,
                t.REQUESTER_ID,
                req.ROW_DESC_E AS REQUESTER_NAME,
                t.ASSIGNEE_ID,
                ass.ROW_DESC_E AS ASSIGNEE_NAME,
                t.TICKET_TYPE_ID,
                tt.TYPE_NAME_EN AS TYPE_NAME,
                t.TICKET_STATUS_ID,
                st.STATUS_NAME_EN AS STATUS_NAME,
                st.STATUS_CODE,
                t.TICKET_PRIORITY_ID,
                pr.PRIORITY_NAME_EN AS PRIORITY_NAME,
                pr.PRIORITY_LEVEL,
                t.TICKET_CATEGORY_ID,
                cat.CATEGORY_NAME_EN AS CATEGORY_NAME,
                t.EXPECTED_RESOLUTION_DATE,
                t.ACTUAL_RESOLUTION_DATE,
                t.IS_ACTIVE,
                t.CREATION_USER,
                t.CREATION_DATE,
                t.UPDATE_USER,
                t.UPDATE_DATE,
                CASE 
                    WHEN t.ACTUAL_RESOLUTION_DATE IS NOT NULL THEN 'Resolved'
                    WHEN t.EXPECTED_RESOLUTION_DATE < SYSDATE THEN 'Overdue'
                    WHEN t.EXPECTED_RESOLUTION_DATE < SYSDATE + (pr.ESCALATION_THRESHOLD_HOURS / 24) THEN 'At Risk'
                    ELSE 'On Time'
                END AS SLA_STATUS
            FROM SYS_REQUEST_TICKET t
            LEFT JOIN SYS_COMPANY c ON t.COMPANY_ID = c.ROW_ID
            LEFT JOIN SYS_BRANCH b ON t.BRANCH_ID = b.ROW_ID
            LEFT JOIN SYS_USERS req ON t.REQUESTER_ID = req.ROW_ID
            LEFT JOIN SYS_USERS ass ON t.ASSIGNEE_ID = ass.ROW_ID
            LEFT JOIN SYS_TICKET_TYPE tt ON t.TICKET_TYPE_ID = tt.ROW_ID
            LEFT JOIN SYS_TICKET_STATUS st ON t.TICKET_STATUS_ID = st.ROW_ID
            LEFT JOIN SYS_TICKET_PRIORITY pr ON t.TICKET_PRIORITY_ID = pr.ROW_ID
            LEFT JOIN SYS_TICKET_CATEGORY cat ON t.TICKET_CATEGORY_ID = cat.ROW_ID
            WHERE t.IS_ACTIVE = 'Y' 
            AND t.REQUESTER_ID = :requesterId 
            AND t.COMPANY_ID = :companyId 
            AND t.BRANCH_ID = :branchId
            ORDER BY t.CREATION_DATE DESC";

        _ = command.Parameters.Add(new OracleParameter("requesterId", requesterId));
        _ = command.Parameters.Add(new OracleParameter("companyId", companyId));
        _ = command.Parameters.Add(new OracleParameter("branchId", branchId));

        using var reader = await command.ExecuteReaderAsync();
        while (await reader.ReadAsync())
        {
            tickets.Add(MapToEntity(reader));
        }

        return tickets;
    }

    /// <summary>
    /// Performs full-text search across ticket titles and descriptions.
    /// Uses the same stored procedure as GetAllAsync with search term.
    /// </summary>
    public async Task<(List<SysRequestTicket> Tickets, int TotalCount)> SearchTicketsAsync(
        string searchTerm, 
        Int64? companyId = null, 
        Int64? branchId = null, 
        int page = 1, 
        int pageSize = 20)
    {
        // Delegate to GetAllAsync with search term
        return await GetAllAsync(
            companyId: companyId,
            branchId: branchId,
            searchTerm: searchTerm,
            page: page,
            pageSize: pageSize);
    }

    /// <summary>
    /// Gets ticket statistics for reporting and dashboard.
    /// Uses direct SQL queries for performance.
    /// </summary>
    public async Task<Dictionary<string, object>> GetTicketStatisticsAsync(
        Int64? companyId = null, 
        Int64? branchId = null, 
        DateTime? fromDate = null, 
        DateTime? toDate = null)
    {
        var statistics = new Dictionary<string, object>();

        using var connection = _dbContext.CreateConnection();
        await connection.OpenAsync();

        // Build WHERE clause for filtering
        var whereClause = "WHERE t.IS_ACTIVE = 'Y'";
        var parameters = new List<OracleParameter>();

        if (companyId.HasValue && companyId > 0)
        {
            whereClause += " AND t.COMPANY_ID = :companyId";
            parameters.Add(new OracleParameter("companyId", companyId.Value));
        }

        if (branchId.HasValue && branchId > 0)
        {
            whereClause += " AND t.BRANCH_ID = :branchId";
            parameters.Add(new OracleParameter("branchId", branchId.Value));
        }

        if (fromDate.HasValue)
        {
            whereClause += " AND t.CREATION_DATE >= :fromDate";
            parameters.Add(new OracleParameter("fromDate", fromDate.Value));
        }

        if (toDate.HasValue)
        {
            whereClause += " AND t.CREATION_DATE <= :toDate";
            parameters.Add(new OracleParameter("toDate", toDate.Value));
        }

        // Get overall statistics
        using var command = connection.CreateCommand();
        command.CommandType = CommandType.Text;
        command.CommandText = $@"
            SELECT 
                COUNT(*) AS TOTAL_TICKETS,
                COUNT(CASE WHEN st.STATUS_CODE = 'OPEN' THEN 1 END) AS OPEN_TICKETS,
                COUNT(CASE WHEN st.STATUS_CODE = 'IN_PROGRESS' THEN 1 END) AS IN_PROGRESS_TICKETS,
                COUNT(CASE WHEN st.STATUS_CODE = 'RESOLVED' THEN 1 END) AS RESOLVED_TICKETS,
                COUNT(CASE WHEN st.STATUS_CODE IN ('CLOSED', 'CANCELLED') THEN 1 END) AS CLOSED_TICKETS,
                COUNT(CASE WHEN pr.PRIORITY_LEVEL = 1 THEN 1 END) AS CRITICAL_TICKETS,
                COUNT(CASE WHEN pr.PRIORITY_LEVEL = 2 THEN 1 END) AS HIGH_TICKETS,
                COUNT(CASE WHEN t.EXPECTED_RESOLUTION_DATE < SYSDATE AND t.ACTUAL_RESOLUTION_DATE IS NULL THEN 1 END) AS OVERDUE_TICKETS,
                ROUND(AVG(CASE WHEN t.ACTUAL_RESOLUTION_DATE IS NOT NULL THEN (t.ACTUAL_RESOLUTION_DATE - t.CREATION_DATE) * 24 END), 2) AS AVG_RESOLUTION_HOURS,
                ROUND((COUNT(CASE WHEN t.ACTUAL_RESOLUTION_DATE IS NOT NULL AND t.ACTUAL_RESOLUTION_DATE <= t.EXPECTED_RESOLUTION_DATE THEN 1 END) * 100.0) / 
                      NULLIF(COUNT(CASE WHEN t.ACTUAL_RESOLUTION_DATE IS NOT NULL THEN 1 END), 0), 2) AS SLA_COMPLIANCE_PERCENTAGE
            FROM SYS_REQUEST_TICKET t
            LEFT JOIN SYS_TICKET_STATUS st ON t.TICKET_STATUS_ID = st.ROW_ID
            LEFT JOIN SYS_TICKET_PRIORITY pr ON t.TICKET_PRIORITY_ID = pr.ROW_ID
            {whereClause}";

        foreach (var param in parameters)
        {
            command.Parameters.Add(param);
        }

        using var reader = await command.ExecuteReaderAsync();
        if (await reader.ReadAsync())
        {
            statistics["TotalTickets"] = reader.GetInt32("TOTAL_TICKETS");
            statistics["OpenTickets"] = reader.GetInt32("OPEN_TICKETS");
            statistics["InProgressTickets"] = reader.GetInt32("IN_PROGRESS_TICKETS");
            statistics["ResolvedTickets"] = reader.GetInt32("RESOLVED_TICKETS");
            statistics["ClosedTickets"] = reader.GetInt32("CLOSED_TICKETS");
            statistics["CriticalTickets"] = reader.GetInt32("CRITICAL_TICKETS");
            statistics["HighTickets"] = reader.GetInt32("HIGH_TICKETS");
            statistics["OverdueTickets"] = reader.GetInt32("OVERDUE_TICKETS");
            statistics["AverageResolutionHours"] = reader.IsDBNull("AVG_RESOLUTION_HOURS") ? 0.0 : reader.GetDecimal("AVG_RESOLUTION_HOURS");
            statistics["SlaCompliancePercentage"] = reader.IsDBNull("SLA_COMPLIANCE_PERCENTAGE") ? 0.0 : reader.GetDecimal("SLA_COMPLIANCE_PERCENTAGE");
        }

        return statistics;
    }

    /// <summary>
    /// Maps an OracleDataReader row to a SysRequestTicket entity.
    /// Handles Oracle data type conversions to C# types for list queries.
    /// </summary>
    /// <param name="reader">The OracleDataReader positioned at a row</param>
    /// <returns>A SysRequestTicket entity populated with data from the reader</returns>
    private SysRequestTicket MapToEntity(OracleDataReader reader)
    {
        return new SysRequestTicket
        {
            RowId = reader.GetInt64(reader.GetOrdinal("ROW_ID")),
            TitleAr = reader.GetString(reader.GetOrdinal("TITLE_AR")),
            TitleEn = reader.GetString(reader.GetOrdinal("TITLE_EN")),
            Description = reader.GetString(reader.GetOrdinal("DESCRIPTION")),
            CompanyId = reader.GetInt64(reader.GetOrdinal("COMPANY_ID")),
            BranchId = reader.GetInt64(reader.GetOrdinal("BRANCH_ID")),
            RequesterId = reader.GetInt64(reader.GetOrdinal("REQUESTER_ID")),
            AssigneeId = reader.IsDBNull(reader.GetOrdinal("ASSIGNEE_ID")) ? null : reader.GetInt64(reader.GetOrdinal("ASSIGNEE_ID")),
            TicketTypeId = reader.GetInt64(reader.GetOrdinal("TICKET_TYPE_ID")),
            TicketStatusId = reader.GetInt64(reader.GetOrdinal("TICKET_STATUS_ID")),
            TicketPriorityId = reader.GetInt64(reader.GetOrdinal("TICKET_PRIORITY_ID")),
            TicketCategoryId = reader.IsDBNull(reader.GetOrdinal("TICKET_CATEGORY_ID")) ? null : reader.GetInt64(reader.GetOrdinal("TICKET_CATEGORY_ID")),
            ExpectedResolutionDate = reader.IsDBNull(reader.GetOrdinal("EXPECTED_RESOLUTION_DATE")) ? null : reader.GetDateTime(reader.GetOrdinal("EXPECTED_RESOLUTION_DATE")),
            ActualResolutionDate = reader.IsDBNull(reader.GetOrdinal("ACTUAL_RESOLUTION_DATE")) ? null : reader.GetDateTime(reader.GetOrdinal("ACTUAL_RESOLUTION_DATE")),
            IsActive = MapIsActiveToBoolean(reader.GetString(reader.GetOrdinal("IS_ACTIVE"))),
            CreationUser = reader.GetString(reader.GetOrdinal("CREATION_USER")),
            CreationDate = reader.IsDBNull(reader.GetOrdinal("CREATION_DATE")) ? null : reader.GetDateTime(reader.GetOrdinal("CREATION_DATE")),
            UpdateUser = reader.IsDBNull(reader.GetOrdinal("UPDATE_USER")) ? null : reader.GetString(reader.GetOrdinal("UPDATE_USER")),
            UpdateDate = reader.IsDBNull(reader.GetOrdinal("UPDATE_DATE")) ? null : reader.GetDateTime(reader.GetOrdinal("UPDATE_DATE")),

            // Navigation properties - populate with basic info for list views
            Company = TryGetCompanyInfo(reader),
            Branch = TryGetBranchInfo(reader),
            Requester = TryGetRequesterInfo(reader),
            Assignee = TryGetAssigneeInfo(reader),
            TicketType = TryGetTicketTypeInfo(reader),
            TicketStatus = TryGetTicketStatusInfo(reader),
            TicketPriority = TryGetTicketPriorityInfo(reader),
            TicketCategory = TryGetTicketCategoryInfo(reader)
        };
    }

    /// <summary>
    /// Maps an OracleDataReader row to a SysRequestTicket entity with detailed navigation properties.
    /// Used for GetByIdAsync to provide complete ticket details.
    /// </summary>
    /// <param name="reader">The OracleDataReader positioned at a row</param>
    /// <returns>A SysRequestTicket entity with detailed navigation properties</returns>
    private SysRequestTicket MapToDetailEntity(OracleDataReader reader)
    {
        return new SysRequestTicket
        {
            RowId = reader.GetInt64(reader.GetOrdinal("ROW_ID")),
            TitleAr = reader.GetString(reader.GetOrdinal("TITLE_AR")),
            TitleEn = reader.GetString(reader.GetOrdinal("TITLE_EN")),
            Description = reader.GetString(reader.GetOrdinal("DESCRIPTION")),
            CompanyId = reader.GetInt64(reader.GetOrdinal("COMPANY_ID")),
            BranchId = reader.GetInt64(reader.GetOrdinal("BRANCH_ID")),
            RequesterId = reader.GetInt64(reader.GetOrdinal("REQUESTER_ID")),
            AssigneeId = reader.IsDBNull(reader.GetOrdinal("ASSIGNEE_ID")) ? null : reader.GetInt64(reader.GetOrdinal("ASSIGNEE_ID")),
            TicketTypeId = reader.GetInt64(reader.GetOrdinal("TICKET_TYPE_ID")),
            TicketStatusId = reader.GetInt64(reader.GetOrdinal("TICKET_STATUS_ID")),
            TicketPriorityId = reader.GetInt64(reader.GetOrdinal("TICKET_PRIORITY_ID")),
            TicketCategoryId = reader.IsDBNull(reader.GetOrdinal("TICKET_CATEGORY_ID")) ? null : reader.GetInt64(reader.GetOrdinal("TICKET_CATEGORY_ID")),
            ExpectedResolutionDate = reader.IsDBNull(reader.GetOrdinal("EXPECTED_RESOLUTION_DATE")) ? null : reader.GetDateTime(reader.GetOrdinal("EXPECTED_RESOLUTION_DATE")),
            ActualResolutionDate = reader.IsDBNull(reader.GetOrdinal("ACTUAL_RESOLUTION_DATE")) ? null : reader.GetDateTime(reader.GetOrdinal("ACTUAL_RESOLUTION_DATE")),
            IsActive = MapIsActiveToBoolean(reader.GetString(reader.GetOrdinal("IS_ACTIVE"))),
            CreationUser = reader.GetString(reader.GetOrdinal("CREATION_USER")),
            CreationDate = reader.IsDBNull(reader.GetOrdinal("CREATION_DATE")) ? null : reader.GetDateTime(reader.GetOrdinal("CREATION_DATE")),
            UpdateUser = reader.IsDBNull(reader.GetOrdinal("UPDATE_USER")) ? null : reader.GetString(reader.GetOrdinal("UPDATE_USER")),
            UpdateDate = reader.IsDBNull(reader.GetOrdinal("UPDATE_DATE")) ? null : reader.GetDateTime(reader.GetOrdinal("UPDATE_DATE")),

            // Detailed navigation properties for single ticket view
            Company = TryGetDetailedCompanyInfo(reader),
            Branch = TryGetDetailedBranchInfo(reader),
            Requester = TryGetDetailedRequesterInfo(reader),
            Assignee = TryGetDetailedAssigneeInfo(reader),
            TicketType = TryGetDetailedTicketTypeInfo(reader),
            TicketStatus = TryGetDetailedTicketStatusInfo(reader),
            TicketPriority = TryGetDetailedTicketPriorityInfo(reader),
            TicketCategory = TryGetDetailedTicketCategoryInfo(reader)
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

    // Helper methods for navigation property mapping
    private SysCompany? TryGetCompanyInfo(OracleDataReader reader)
    {
        try
        {
            var companyNameOrdinal = reader.GetOrdinal("COMPANY_NAME");
            if (!reader.IsDBNull(companyNameOrdinal))
            {
                return new SysCompany
                {
                    RowId = reader.GetInt64(reader.GetOrdinal("COMPANY_ID")),
                    RowDescE = reader.GetString(companyNameOrdinal)
                };
            }
        }
        catch (IndexOutOfRangeException)
        {
            // Column not present in this query
        }
        return null;
    }

    private SysBranch? TryGetBranchInfo(OracleDataReader reader)
    {
        try
        {
            var branchNameOrdinal = reader.GetOrdinal("BRANCH_NAME");
            if (!reader.IsDBNull(branchNameOrdinal))
            {
                return new SysBranch
                {
                    RowId = reader.GetInt64(reader.GetOrdinal("BRANCH_ID")),
                    RowDescE = reader.GetString(branchNameOrdinal)
                };
            }
        }
        catch (IndexOutOfRangeException)
        {
            // Column not present in this query
        }
        return null;
    }

    private SysUser? TryGetRequesterInfo(OracleDataReader reader)
    {
        try
        {
            var requesterNameOrdinal = reader.GetOrdinal("REQUESTER_NAME");
            if (!reader.IsDBNull(requesterNameOrdinal))
            {
                return new SysUser
                {
                    RowId = reader.GetInt64(reader.GetOrdinal("REQUESTER_ID")),
                    RowDescE = reader.GetString(requesterNameOrdinal)
                };
            }
        }
        catch (IndexOutOfRangeException)
        {
            // Column not present in this query
        }
        return null;
    }

    private SysUser? TryGetAssigneeInfo(OracleDataReader reader)
    {
        try
        {
            var assigneeIdOrdinal = reader.GetOrdinal("ASSIGNEE_ID");
            if (!reader.IsDBNull(assigneeIdOrdinal))
            {
                var assigneeNameOrdinal = reader.GetOrdinal("ASSIGNEE_NAME");
                return new SysUser
                {
                    RowId = reader.GetInt64(assigneeIdOrdinal),
                    RowDescE = reader.IsDBNull(assigneeNameOrdinal) ? string.Empty : reader.GetString(assigneeNameOrdinal)
                };
            }
        }
        catch (IndexOutOfRangeException)
        {
            // Column not present in this query
        }
        return null;
    }

    private SysTicketType? TryGetTicketTypeInfo(OracleDataReader reader)
    {
        try
        {
            var typeNameOrdinal = reader.GetOrdinal("TYPE_NAME");
            if (!reader.IsDBNull(typeNameOrdinal))
            {
                return new SysTicketType
                {
                    RowId = reader.GetInt64(reader.GetOrdinal("TICKET_TYPE_ID")),
                    TypeNameEn = reader.GetString(typeNameOrdinal)
                };
            }
        }
        catch (IndexOutOfRangeException)
        {
            // Column not present in this query
        }
        return null;
    }

    private SysTicketStatus? TryGetTicketStatusInfo(OracleDataReader reader)
    {
        try
        {
            var statusNameOrdinal = reader.GetOrdinal("STATUS_NAME");
            if (!reader.IsDBNull(statusNameOrdinal))
            {
                return new SysTicketStatus
                {
                    RowId = reader.GetInt64(reader.GetOrdinal("TICKET_STATUS_ID")),
                    StatusNameEn = reader.GetString(statusNameOrdinal),
                    StatusCode = TryGetStringValue(reader, "STATUS_CODE") ?? string.Empty
                };
            }
        }
        catch (IndexOutOfRangeException)
        {
            // Column not present in this query
        }
        return null;
    }

    private SysTicketPriority? TryGetTicketPriorityInfo(OracleDataReader reader)
    {
        try
        {
            var priorityNameOrdinal = reader.GetOrdinal("PRIORITY_NAME");
            if (!reader.IsDBNull(priorityNameOrdinal))
            {
                return new SysTicketPriority
                {
                    RowId = reader.GetInt64(reader.GetOrdinal("TICKET_PRIORITY_ID")),
                    PriorityNameEn = reader.GetString(priorityNameOrdinal),
                    PriorityLevel = TryGetIntValue(reader, "PRIORITY_LEVEL") ?? 0
                };
            }
        }
        catch (IndexOutOfRangeException)
        {
            // Column not present in this query
        }
        return null;
    }

    private SysTicketCategory? TryGetTicketCategoryInfo(OracleDataReader reader)
    {
        try
        {
            var categoryIdOrdinal = reader.GetOrdinal("TICKET_CATEGORY_ID");
            if (!reader.IsDBNull(categoryIdOrdinal))
            {
                var categoryNameOrdinal = reader.GetOrdinal("CATEGORY_NAME");
                return new SysTicketCategory
                {
                    RowId = reader.GetInt64(categoryIdOrdinal),
                    CategoryNameEn = reader.IsDBNull(categoryNameOrdinal) ? string.Empty : reader.GetString(categoryNameOrdinal)
                };
            }
        }
        catch (IndexOutOfRangeException)
        {
            // Column not present in this query
        }
        return null;
    }

    // Detailed mapping methods for GetByIdAsync
    private SysCompany? TryGetDetailedCompanyInfo(OracleDataReader reader)
    {
        try
        {
            return new SysCompany
            {
                RowId = reader.GetInt64(reader.GetOrdinal("COMPANY_ID")),
                RowDesc = TryGetStringValue(reader, "COMPANY_NAME_AR") ?? string.Empty,
                RowDescE = TryGetStringValue(reader, "COMPANY_NAME_EN") ?? string.Empty
            };
        }
        catch (IndexOutOfRangeException)
        {
            return TryGetCompanyInfo(reader);
        }
    }

    private SysBranch? TryGetDetailedBranchInfo(OracleDataReader reader)
    {
        try
        {
            return new SysBranch
            {
                RowId = reader.GetInt64(reader.GetOrdinal("BRANCH_ID")),
                RowDesc = TryGetStringValue(reader, "BRANCH_NAME_AR") ?? string.Empty,
                RowDescE = TryGetStringValue(reader, "BRANCH_NAME_EN") ?? string.Empty
            };
        }
        catch (IndexOutOfRangeException)
        {
            return TryGetBranchInfo(reader);
        }
    }

    private SysUser? TryGetDetailedRequesterInfo(OracleDataReader reader)
    {
        try
        {
            return new SysUser
            {
                RowId = reader.GetInt64(reader.GetOrdinal("REQUESTER_ID")),
                RowDesc = TryGetStringValue(reader, "REQUESTER_NAME_AR") ?? string.Empty,
                RowDescE = TryGetStringValue(reader, "REQUESTER_NAME_EN") ?? string.Empty,
                UserName = TryGetStringValue(reader, "REQUESTER_USERNAME") ?? string.Empty,
                Email = TryGetStringValue(reader, "REQUESTER_EMAIL")
            };
        }
        catch (IndexOutOfRangeException)
        {
            return TryGetRequesterInfo(reader);
        }
    }

    private SysUser? TryGetDetailedAssigneeInfo(OracleDataReader reader)
    {
        try
        {
            var assigneeIdOrdinal = reader.GetOrdinal("ASSIGNEE_ID");
            if (!reader.IsDBNull(assigneeIdOrdinal))
            {
                return new SysUser
                {
                    RowId = reader.GetInt64(assigneeIdOrdinal),
                    RowDesc = TryGetStringValue(reader, "ASSIGNEE_NAME_AR") ?? string.Empty,
                    RowDescE = TryGetStringValue(reader, "ASSIGNEE_NAME_EN") ?? string.Empty,
                    UserName = TryGetStringValue(reader, "ASSIGNEE_USERNAME") ?? string.Empty,
                    Email = TryGetStringValue(reader, "ASSIGNEE_EMAIL")
                };
            }
        }
        catch (IndexOutOfRangeException)
        {
            return TryGetAssigneeInfo(reader);
        }
        return null;
    }

    private SysTicketType? TryGetDetailedTicketTypeInfo(OracleDataReader reader)
    {
        try
        {
            return new SysTicketType
            {
                RowId = reader.GetInt64(reader.GetOrdinal("TICKET_TYPE_ID")),
                TypeNameAr = TryGetStringValue(reader, "TYPE_NAME_AR") ?? string.Empty,
                TypeNameEn = TryGetStringValue(reader, "TYPE_NAME_EN") ?? string.Empty
            };
        }
        catch (IndexOutOfRangeException)
        {
            return TryGetTicketTypeInfo(reader);
        }
    }

    private SysTicketStatus? TryGetDetailedTicketStatusInfo(OracleDataReader reader)
    {
        try
        {
            return new SysTicketStatus
            {
                RowId = reader.GetInt64(reader.GetOrdinal("TICKET_STATUS_ID")),
                StatusNameAr = TryGetStringValue(reader, "STATUS_NAME_AR") ?? string.Empty,
                StatusNameEn = TryGetStringValue(reader, "STATUS_NAME_EN") ?? string.Empty,
                StatusCode = TryGetStringValue(reader, "STATUS_CODE") ?? string.Empty,
                IsFinalStatus = TryGetBooleanValue(reader, "IS_FINAL_STATUS") ?? false
            };
        }
        catch (IndexOutOfRangeException)
        {
            return TryGetTicketStatusInfo(reader);
        }
    }

    private SysTicketPriority? TryGetDetailedTicketPriorityInfo(OracleDataReader reader)
    {
        try
        {
            return new SysTicketPriority
            {
                RowId = reader.GetInt64(reader.GetOrdinal("TICKET_PRIORITY_ID")),
                PriorityNameAr = TryGetStringValue(reader, "PRIORITY_NAME_AR") ?? string.Empty,
                PriorityNameEn = TryGetStringValue(reader, "PRIORITY_NAME_EN") ?? string.Empty,
                PriorityLevel = TryGetIntValue(reader, "PRIORITY_LEVEL") ?? 0,
                SlaTargetHours = TryGetDecimalValue(reader, "SLA_TARGET_HOURS") ?? 0,
                EscalationThresholdHours = TryGetDecimalValue(reader, "ESCALATION_THRESHOLD_HOURS") ?? 0
            };
        }
        catch (IndexOutOfRangeException)
        {
            return TryGetTicketPriorityInfo(reader);
        }
    }

    private SysTicketCategory? TryGetDetailedTicketCategoryInfo(OracleDataReader reader)
    {
        try
        {
            var categoryIdOrdinal = reader.GetOrdinal("TICKET_CATEGORY_ID");
            if (!reader.IsDBNull(categoryIdOrdinal))
            {
                return new SysTicketCategory
                {
                    RowId = reader.GetInt64(categoryIdOrdinal),
                    CategoryNameAr = TryGetStringValue(reader, "CATEGORY_NAME_AR") ?? string.Empty,
                    CategoryNameEn = TryGetStringValue(reader, "CATEGORY_NAME_EN") ?? string.Empty
                };
            }
        }
        catch (IndexOutOfRangeException)
        {
            return TryGetTicketCategoryInfo(reader);
        }
        return null;
    }

    // Utility methods for safe value extraction
    private string? TryGetStringValue(OracleDataReader reader, string columnName)
    {
        try
        {
            var ordinal = reader.GetOrdinal(columnName);
            return reader.IsDBNull(ordinal) ? null : reader.GetString(ordinal);
        }
        catch (IndexOutOfRangeException)
        {
            return null;
        }
    }

    private int? TryGetIntValue(OracleDataReader reader, string columnName)
    {
        try
        {
            var ordinal = reader.GetOrdinal(columnName);
            return reader.IsDBNull(ordinal) ? null : reader.GetInt32(ordinal);
        }
        catch (IndexOutOfRangeException)
        {
            return null;
        }
    }

    private decimal? TryGetDecimalValue(OracleDataReader reader, string columnName)
    {
        try
        {
            var ordinal = reader.GetOrdinal(columnName);
            return reader.IsDBNull(ordinal) ? null : reader.GetDecimal(ordinal);
        }
        catch (IndexOutOfRangeException)
        {
            return null;
        }
    }

    private bool? TryGetBooleanValue(OracleDataReader reader, string columnName)
    {
        try
        {
            var ordinal = reader.GetOrdinal(columnName);
            if (reader.IsDBNull(ordinal)) return null;
            var value = reader.GetString(ordinal);
            return value is "Y" or "1";
        }
        catch (IndexOutOfRangeException)
        {
            return null;
        }
    }

    /// <summary>
    /// Retrieves tickets that are overdue based on current time.
    /// </summary>
    /// <param name="currentTime">Current time to compare against expected resolution date</param>
    /// <returns>A list of overdue tickets</returns>
    public async Task<List<SysRequestTicket>> GetOverdueTicketsAsync(DateTime currentTime)
    {
        using var connection = _dbContext.CreateConnection();
        using var command = new OracleCommand("SP_SYS_REQUEST_TICKET_SELECT_OVERDUE_BY_TIME", connection)
        {
            CommandType = CommandType.StoredProcedure
        };

        // Add parameters
        command.Parameters.Add("p_current_time", OracleDbType.Date).Value = currentTime;
        var cursorParam = new OracleParameter("p_cursor", OracleDbType.RefCursor, ParameterDirection.Output);
        command.Parameters.Add(cursorParam);

        await connection.OpenAsync();
        await command.ExecuteNonQueryAsync();

        var tickets = new List<SysRequestTicket>();
        using var reader = ((OracleRefCursor)cursorParam.Value).GetDataReader();
        
        while (await reader.ReadAsync())
        {
            var ticket = MapToDetailEntity(reader);
            tickets.Add(ticket);
        }

        return tickets;
    }

    /// <summary>
    /// Retrieves tickets approaching SLA deadline based on cutoff time.
    /// </summary>
    /// <param name="cutoffTime">Time threshold for approaching deadline</param>
    /// <returns>A list of tickets approaching deadline</returns>
    public async Task<List<SysRequestTicket>> GetTicketsApproachingSlaDeadlineAsync(DateTime cutoffTime)
    {
        using var connection = _dbContext.CreateConnection();
        using var command = new OracleCommand("SP_SYS_REQUEST_TICKET_SELECT_APPROACHING_SLA", connection)
        {
            CommandType = CommandType.StoredProcedure
        };

        // Add parameters
        command.Parameters.Add("p_cutoff_time", OracleDbType.Date).Value = cutoffTime;
        var cursorParam = new OracleParameter("p_cursor", OracleDbType.RefCursor, ParameterDirection.Output);
        command.Parameters.Add(cursorParam);

        await connection.OpenAsync();
        await command.ExecuteNonQueryAsync();

        var tickets = new List<SysRequestTicket>();
        using var reader = ((OracleRefCursor)cursorParam.Value).GetDataReader();
        
        while (await reader.ReadAsync())
        {
            var ticket = MapToEntity(reader);
            tickets.Add(ticket);
        }

        return tickets;
    }

    /// <summary>
    /// Generates ticket volume reports by time period, company, and type.
    /// Calls SP_SYS_TICKET_REPORTS_VOLUME stored procedure.
    /// </summary>
    public async Task<List<Dictionary<string, object>>> GetTicketVolumeReportAsync(
        DateTime startDate,
        DateTime endDate,
        Int64 companyId = 0,
        Int64 ticketTypeId = 0,
        string groupBy = "DAILY")
    {
        using var connection = _dbContext.CreateConnection();
        using var command = new OracleCommand("SP_SYS_TICKET_REPORTS_VOLUME", connection)
        {
            CommandType = CommandType.StoredProcedure
        };

        // Add parameters
        command.Parameters.Add("P_START_DATE", OracleDbType.Date).Value = startDate;
        command.Parameters.Add("P_END_DATE", OracleDbType.Date).Value = endDate;
        command.Parameters.Add("P_COMPANY_ID", OracleDbType.Int64).Value = companyId;
        command.Parameters.Add("P_TICKET_TYPE_ID", OracleDbType.Int64).Value = ticketTypeId;
        command.Parameters.Add("P_GROUP_BY", OracleDbType.Varchar2).Value = groupBy.ToUpper();
        var cursorParam = new OracleParameter("P_RESULT_CURSOR", OracleDbType.RefCursor, ParameterDirection.Output);
        command.Parameters.Add(cursorParam);

        await connection.OpenAsync();
        await command.ExecuteNonQueryAsync();

        var results = new List<Dictionary<string, object>>();
        using var reader = ((OracleRefCursor)cursorParam.Value).GetDataReader();
        
        while (await reader.ReadAsync())
        {
            var row = new Dictionary<string, object>();
            for (int i = 0; i < reader.FieldCount; i++)
            {
                var columnName = reader.GetName(i);
                row[columnName] = reader.IsDBNull(i) ? null : reader.GetValue(i);
            }
            results.Add(row);
        }

        return results;
    }

    /// <summary>
    /// Calculates SLA compliance percentages by priority and type.
    /// Calls SP_SYS_TICKET_REPORTS_SLA_COMPLIANCE stored procedure.
    /// </summary>
    public async Task<List<Dictionary<string, object>>> GetSlaComplianceReportAsync(
        DateTime startDate,
        DateTime endDate,
        Int64 companyId = 0)
    {
        using var connection = _dbContext.CreateConnection();
        using var command = new OracleCommand("SP_SYS_TICKET_REPORTS_SLA_COMPLIANCE", connection)
        {
            CommandType = CommandType.StoredProcedure
        };

        // Add parameters
        command.Parameters.Add("P_START_DATE", OracleDbType.Date).Value = startDate;
        command.Parameters.Add("P_END_DATE", OracleDbType.Date).Value = endDate;
        command.Parameters.Add("P_COMPANY_ID", OracleDbType.Int64).Value = companyId;
        var cursorParam = new OracleParameter("P_RESULT_CURSOR", OracleDbType.RefCursor, ParameterDirection.Output);
        command.Parameters.Add(cursorParam);

        await connection.OpenAsync();
        await command.ExecuteNonQueryAsync();

        var results = new List<Dictionary<string, object>>();
        using var reader = ((OracleRefCursor)cursorParam.Value).GetDataReader();
        
        while (await reader.ReadAsync())
        {
            var row = new Dictionary<string, object>();
            for (int i = 0; i < reader.FieldCount; i++)
            {
                var columnName = reader.GetName(i);
                row[columnName] = reader.IsDBNull(i) ? null : reader.GetValue(i);
            }
            results.Add(row);
        }

        return results;
    }

    /// <summary>
    /// Generates workload reports showing active and resolved tickets per assignee.
    /// Calls SP_SYS_TICKET_REPORTS_WORKLOAD stored procedure.
    /// </summary>
    public async Task<List<Dictionary<string, object>>> GetWorkloadReportAsync(
        DateTime? startDate = null,
        DateTime? endDate = null,
        Int64 companyId = 0)
    {
        using var connection = _dbContext.CreateConnection();
        using var command = new OracleCommand("SP_SYS_TICKET_REPORTS_WORKLOAD", connection)
        {
            CommandType = CommandType.StoredProcedure
        };

        // Add parameters
        if (startDate.HasValue)
            command.Parameters.Add("P_START_DATE", OracleDbType.Date).Value = startDate.Value;
        else
            command.Parameters.Add("P_START_DATE", OracleDbType.Date).Value = DBNull.Value;

        if (endDate.HasValue)
            command.Parameters.Add("P_END_DATE", OracleDbType.Date).Value = endDate.Value;
        else
            command.Parameters.Add("P_END_DATE", OracleDbType.Date).Value = DBNull.Value;

        command.Parameters.Add("P_COMPANY_ID", OracleDbType.Int64).Value = companyId;
        var cursorParam = new OracleParameter("P_RESULT_CURSOR", OracleDbType.RefCursor, ParameterDirection.Output);
        command.Parameters.Add(cursorParam);

        await connection.OpenAsync();
        await command.ExecuteNonQueryAsync();

        var results = new List<Dictionary<string, object>>();
        using var reader = ((OracleRefCursor)cursorParam.Value).GetDataReader();
        
        while (await reader.ReadAsync())
        {
            var row = new Dictionary<string, object>();
            for (int i = 0; i < reader.FieldCount; i++)
            {
                var columnName = reader.GetName(i);
                row[columnName] = reader.IsDBNull(i) ? null : reader.GetValue(i);
            }
            results.Add(row);
        }

        return results;
    }

    /// <summary>
    /// Provides trend analysis showing ticket creation and resolution patterns over time.
    /// Calls SP_SYS_TICKET_REPORTS_TRENDS stored procedure.
    /// </summary>
    public async Task<List<Dictionary<string, object>>> GetTicketTrendsReportAsync(
        DateTime startDate,
        DateTime endDate,
        string periodType = "DAILY")
    {
        using var connection = _dbContext.CreateConnection();
        using var command = new OracleCommand("SP_SYS_TICKET_REPORTS_TRENDS", connection)
        {
            CommandType = CommandType.StoredProcedure
        };

        // Add parameters
        command.Parameters.Add("P_START_DATE", OracleDbType.Date).Value = startDate;
        command.Parameters.Add("P_END_DATE", OracleDbType.Date).Value = endDate;
        command.Parameters.Add("P_PERIOD_TYPE", OracleDbType.Varchar2).Value = periodType.ToUpper();
        var cursorParam = new OracleParameter("P_RESULT_CURSOR", OracleDbType.RefCursor, ParameterDirection.Output);
        command.Parameters.Add(cursorParam);

        await connection.OpenAsync();
        await command.ExecuteNonQueryAsync();

        var results = new List<Dictionary<string, object>>();
        using var reader = ((OracleRefCursor)cursorParam.Value).GetDataReader();
        
        while (await reader.ReadAsync())
        {
            var row = new Dictionary<string, object>();
            for (int i = 0; i < reader.FieldCount; i++)
            {
                var columnName = reader.GetName(i);
                row[columnName] = reader.IsDBNull(i) ? null : reader.GetValue(i);
            }
            results.Add(row);
        }

        return results;
    }

    /// <summary>
    /// Performs advanced search with multi-criteria filtering, AND/OR logic, and relevance scoring.
    /// Calls SP_SYS_REQUEST_TICKET_ADVANCED_SEARCH stored procedure.
    /// Requirements: 8.1-8.12, 8.9
    /// </summary>
    public async Task<(List<SysRequestTicket> Tickets, int TotalCount)> AdvancedSearchAsync(
        string? searchTerm = null,
        Int64? companyId = null,
        Int64? branchId = null,
        Int64? assigneeId = null,
        Int64? requesterId = null,
        string? statusIds = null,
        string? priorityIds = null,
        string? typeIds = null,
        string? categoryIds = null,
        DateTime? createdFrom = null,
        DateTime? createdTo = null,
        DateTime? dueFrom = null,
        DateTime? dueTo = null,
        string? slaStatus = null,
        string filterLogic = "AND",
        bool includeInactive = false,
        int page = 1,
        int pageSize = 20,
        string sortBy = "RELEVANCE",
        string sortDirection = "DESC")
    {
        List<SysRequestTicket> tickets = new();
        int totalCount = 0;

        using var connection = _dbContext.CreateConnection();
        await connection.OpenAsync();

        using var command = connection.CreateCommand();
        command.CommandType = CommandType.StoredProcedure;
        command.CommandText = "SP_SYS_REQUEST_TICKET_ADVANCED_SEARCH";

        // Add input parameters
        command.Parameters.Add("P_SEARCH_TERM", OracleDbType.NVarchar2).Value = (object?)searchTerm ?? DBNull.Value;
        command.Parameters.Add("P_COMPANY_ID", OracleDbType.Decimal).Value = companyId ?? 0;
        command.Parameters.Add("P_BRANCH_ID", OracleDbType.Decimal).Value = branchId ?? 0;
        command.Parameters.Add("P_ASSIGNEE_ID", OracleDbType.Decimal).Value = assigneeId ?? 0;
        command.Parameters.Add("P_REQUESTER_ID", OracleDbType.Decimal).Value = requesterId ?? 0;
        command.Parameters.Add("P_STATUS_IDS", OracleDbType.Varchar2).Value = (object?)statusIds ?? DBNull.Value;
        command.Parameters.Add("P_PRIORITY_IDS", OracleDbType.Varchar2).Value = (object?)priorityIds ?? DBNull.Value;
        command.Parameters.Add("P_TYPE_IDS", OracleDbType.Varchar2).Value = (object?)typeIds ?? DBNull.Value;
        command.Parameters.Add("P_CATEGORY_IDS", OracleDbType.Varchar2).Value = (object?)categoryIds ?? DBNull.Value;
        command.Parameters.Add("P_CREATED_FROM", OracleDbType.Date).Value = (object?)createdFrom ?? DBNull.Value;
        command.Parameters.Add("P_CREATED_TO", OracleDbType.Date).Value = (object?)createdTo ?? DBNull.Value;
        command.Parameters.Add("P_DUE_FROM", OracleDbType.Date).Value = (object?)dueFrom ?? DBNull.Value;
        command.Parameters.Add("P_DUE_TO", OracleDbType.Date).Value = (object?)dueTo ?? DBNull.Value;
        command.Parameters.Add("P_SLA_STATUS", OracleDbType.Varchar2).Value = (object?)slaStatus ?? DBNull.Value;
        command.Parameters.Add("P_FILTER_LOGIC", OracleDbType.Varchar2).Value = filterLogic;
        command.Parameters.Add("P_INCLUDE_INACTIVE", OracleDbType.Char).Value = includeInactive ? "Y" : "N";
        command.Parameters.Add("P_PAGE_NUMBER", OracleDbType.Decimal).Value = page;
        command.Parameters.Add("P_PAGE_SIZE", OracleDbType.Decimal).Value = pageSize;
        command.Parameters.Add("P_SORT_BY", OracleDbType.Varchar2).Value = sortBy;
        command.Parameters.Add("P_SORT_DIRECTION", OracleDbType.Varchar2).Value = sortDirection;

        // Add output parameters
        var cursorParam = command.Parameters.Add("P_RESULT_CURSOR", OracleDbType.RefCursor);
        cursorParam.Direction = ParameterDirection.Output;

        var totalCountParam = command.Parameters.Add("P_TOTAL_COUNT", OracleDbType.Decimal);
        totalCountParam.Direction = ParameterDirection.Output;

        await command.ExecuteNonQueryAsync();

        // Get total count
        totalCount = Convert.ToInt32(totalCountParam.Value.ToString());

        // Read tickets from cursor
        using var reader = ((OracleRefCursor)cursorParam.Value).GetDataReader();
        while (await reader.ReadAsync())
        {
            var ticket = new SysRequestTicket
            {
                RowId = reader.GetInt64(reader.GetOrdinal("ROW_ID")),
                TitleAr = reader.GetString(reader.GetOrdinal("TITLE_AR")),
                TitleEn = reader.GetString(reader.GetOrdinal("TITLE_EN")),
                Description = reader.GetString(reader.GetOrdinal("DESCRIPTION")),
                CompanyId = reader.GetInt64(reader.GetOrdinal("COMPANY_ID")),
                BranchId = reader.GetInt64(reader.GetOrdinal("BRANCH_ID")),
                RequesterId = reader.GetInt64(reader.GetOrdinal("REQUESTER_ID")),
                AssigneeId = reader.IsDBNull(reader.GetOrdinal("ASSIGNEE_ID")) 
                    ? null 
                    : reader.GetInt64(reader.GetOrdinal("ASSIGNEE_ID")),
                TicketTypeId = reader.GetInt64(reader.GetOrdinal("TICKET_TYPE_ID")),
                TicketStatusId = reader.GetInt64(reader.GetOrdinal("TICKET_STATUS_ID")),
                TicketPriorityId = reader.GetInt64(reader.GetOrdinal("TICKET_PRIORITY_ID")),
                TicketCategoryId = reader.IsDBNull(reader.GetOrdinal("TICKET_CATEGORY_ID")) 
                    ? null 
                    : reader.GetInt64(reader.GetOrdinal("TICKET_CATEGORY_ID")),
                ExpectedResolutionDate = reader.IsDBNull(reader.GetOrdinal("EXPECTED_RESOLUTION_DATE")) 
                    ? null 
                    : reader.GetDateTime(reader.GetOrdinal("EXPECTED_RESOLUTION_DATE")),
                ActualResolutionDate = reader.IsDBNull(reader.GetOrdinal("ACTUAL_RESOLUTION_DATE")) 
                    ? null 
                    : reader.GetDateTime(reader.GetOrdinal("ACTUAL_RESOLUTION_DATE")),
                IsActive = reader.GetString(reader.GetOrdinal("IS_ACTIVE")) == "Y",
                CreationUser = reader.GetString(reader.GetOrdinal("CREATION_USER")),
                CreationDate = reader.IsDBNull(reader.GetOrdinal("CREATION_DATE")) 
                    ? null 
                    : reader.GetDateTime(reader.GetOrdinal("CREATION_DATE")),
                UpdateUser = reader.IsDBNull(reader.GetOrdinal("UPDATE_USER")) 
                    ? null 
                    : reader.GetString(reader.GetOrdinal("UPDATE_USER")),
                UpdateDate = reader.IsDBNull(reader.GetOrdinal("UPDATE_DATE")) 
                    ? null 
                    : reader.GetDateTime(reader.GetOrdinal("UPDATE_DATE"))
            };

            // Store relevance score in a temporary property for later use
            // Note: We'll need to add this to the entity or handle it differently
            var relevanceScore = reader.GetInt32(reader.GetOrdinal("RELEVANCE_SCORE"));
            
            // Create navigation property objects with names
            ticket.Company = new SysCompany 
            { 
                RowId = ticket.CompanyId,
                RowDescE = reader.IsDBNull(reader.GetOrdinal("COMPANY_NAME")) 
                    ? null 
                    : reader.GetString(reader.GetOrdinal("COMPANY_NAME"))
            };

            ticket.Branch = new SysBranch 
            { 
                RowId = ticket.BranchId,
                RowDescE = reader.IsDBNull(reader.GetOrdinal("BRANCH_NAME")) 
                    ? null 
                    : reader.GetString(reader.GetOrdinal("BRANCH_NAME"))
            };

            ticket.Requester = new SysUser 
            { 
                RowId = ticket.RequesterId,
                RowDescE = reader.IsDBNull(reader.GetOrdinal("REQUESTER_NAME")) 
                    ? null 
                    : reader.GetString(reader.GetOrdinal("REQUESTER_NAME"))
            };

            if (ticket.AssigneeId.HasValue)
            {
                ticket.Assignee = new SysUser 
                { 
                    RowId = ticket.AssigneeId.Value,
                    RowDescE = reader.IsDBNull(reader.GetOrdinal("ASSIGNEE_NAME")) 
                        ? null 
                        : reader.GetString(reader.GetOrdinal("ASSIGNEE_NAME"))
                };
            }

            ticket.TicketType = new SysTicketType 
            { 
                RowId = ticket.TicketTypeId,
                TypeNameEn = reader.IsDBNull(reader.GetOrdinal("TYPE_NAME")) 
                    ? null 
                    : reader.GetString(reader.GetOrdinal("TYPE_NAME"))
            };

            ticket.TicketStatus = new SysTicketStatus 
            { 
                RowId = ticket.TicketStatusId,
                StatusNameEn = reader.IsDBNull(reader.GetOrdinal("STATUS_NAME")) 
                    ? null 
                    : reader.GetString(reader.GetOrdinal("STATUS_NAME")),
                StatusCode = reader.IsDBNull(reader.GetOrdinal("STATUS_CODE")) 
                    ? null 
                    : reader.GetString(reader.GetOrdinal("STATUS_CODE"))
            };

            ticket.TicketPriority = new SysTicketPriority 
            { 
                RowId = ticket.TicketPriorityId,
                PriorityNameEn = reader.IsDBNull(reader.GetOrdinal("PRIORITY_NAME")) 
                    ? null 
                    : reader.GetString(reader.GetOrdinal("PRIORITY_NAME")),
                PriorityLevel = reader.GetInt32(reader.GetOrdinal("PRIORITY_LEVEL"))
            };

            if (ticket.TicketCategoryId.HasValue)
            {
                ticket.TicketCategory = new SysTicketCategory 
                { 
                    RowId = ticket.TicketCategoryId.Value,
                    CategoryNameEn = reader.IsDBNull(reader.GetOrdinal("CATEGORY_NAME")) 
                        ? null 
                        : reader.GetString(reader.GetOrdinal("CATEGORY_NAME"))
                };
            }

            tickets.Add(ticket);
        }

        return (tickets, totalCount);
    }
}
