using Oracle.ManagedDataAccess.Client;
using System.Data;
using ThinkOnErp.Domain.Entities;
using ThinkOnErp.Domain.Interfaces;
using ThinkOnErp.Infrastructure.Data;

namespace ThinkOnErp.Infrastructure.Repositories;

/// <summary>
/// Repository implementation for SysTicketComment entity using ADO.NET with Oracle stored procedures.
/// Implements ITicketCommentRepository interface from the Domain layer.
/// Uses OracleDbContext to create connections and maps Oracle data types to C# types.
/// </summary>
public class TicketCommentRepository : ITicketCommentRepository
{
    private readonly OracleDbContext _dbContext;

    /// <summary>
    /// Initializes a new instance of the TicketCommentRepository class.
    /// </summary>
    /// <param name="dbContext">The Oracle database context for connection management.</param>
    /// <exception cref="ArgumentNullException">Thrown when dbContext is null.</exception>
    public TicketCommentRepository(OracleDbContext dbContext)
    {
        _dbContext = dbContext ?? throw new ArgumentNullException(nameof(dbContext));
    }

    /// <summary>
    /// Retrieves all comments for a specific ticket with authorization filtering.
    /// Calls SP_SYS_TICKET_COMMENT_SELECT_BY_TICKET stored procedure.
    /// </summary>
    /// <param name="ticketId">The unique identifier of the ticket</param>
    /// <param name="includeInternal">Whether to include internal comments (admin only)</param>
    /// <returns>A list of comments ordered by creation date</returns>
    public async Task<List<SysTicketComment>> GetByTicketIdAsync(Int64 ticketId, bool includeInternal = false)
    {
        List<SysTicketComment> comments = new();

        using (var connection = _dbContext.CreateConnection())
        {
            await connection.OpenAsync();

            using var command = connection.CreateCommand();
            command.CommandType = CommandType.StoredProcedure;
            command.CommandText = "SP_SYS_TICKET_COMMENT_SELECT_BY_TICKET";

            // Add input parameters
            _ = command.Parameters.Add(new OracleParameter
            {
                ParameterName = "P_TICKET_ID",
                OracleDbType = OracleDbType.Decimal,
                Direction = ParameterDirection.Input,
                Value = ticketId
            });

            _ = command.Parameters.Add(new OracleParameter
            {
                ParameterName = "P_INCLUDE_INTERNAL",
                OracleDbType = OracleDbType.Char,
                Direction = ParameterDirection.Input,
                Value = includeInternal ? "Y" : "N"
            });

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
                comments.Add(MapToEntity(reader));
            }
        }

        return comments;
    }

    /// <summary>
    /// Retrieves a specific comment by its ID.
    /// </summary>
    /// <param name="rowId">The unique identifier of the comment</param>
    /// <returns>The SysTicketComment entity if found, null otherwise</returns>
    public async Task<SysTicketComment?> GetByIdAsync(Int64 rowId)
    {
        using var connection = _dbContext.CreateConnection();
        await connection.OpenAsync();

        using var command = connection.CreateCommand();
        command.CommandType = CommandType.Text;
        command.CommandText = @"
            SELECT 
                c.ROW_ID,
                c.TICKET_ID,
                c.COMMENT_TEXT,
                c.IS_INTERNAL,
                c.CREATION_USER,
                c.CREATION_DATE
            FROM SYS_TICKET_COMMENT c
            WHERE c.ROW_ID = :rowId";

        _ = command.Parameters.Add(new OracleParameter
        {
            ParameterName = "rowId",
            OracleDbType = OracleDbType.Decimal,
            Direction = ParameterDirection.Input,
            Value = rowId
        });

        using var reader = await command.ExecuteReaderAsync();
        if (await reader.ReadAsync())
        {
            return MapToEntity(reader);
        }

        return null;
    }

    /// <summary>
    /// Creates a new comment in the database.
    /// Calls SP_SYS_TICKET_COMMENT_INSERT stored procedure.
    /// </summary>
    /// <param name="comment">The comment entity to create</param>
    /// <returns>The generated RowId from SEQ_SYS_TICKET_COMMENT sequence</returns>
    public async Task<Int64> CreateAsync(SysTicketComment comment)
    {
        using var connection = _dbContext.CreateConnection();
        await connection.OpenAsync();

        using var command = connection.CreateCommand();
        command.CommandType = CommandType.StoredProcedure;
        command.CommandText = "SP_SYS_TICKET_COMMENT_INSERT";

        // Add input parameters
        _ = command.Parameters.Add(new OracleParameter
        {
            ParameterName = "P_TICKET_ID",
            OracleDbType = OracleDbType.Decimal,
            Direction = ParameterDirection.Input,
            Value = comment.TicketId
        });

        _ = command.Parameters.Add(new OracleParameter
        {
            ParameterName = "P_COMMENT_TEXT",
            OracleDbType = OracleDbType.NClob,
            Direction = ParameterDirection.Input,
            Value = comment.CommentText
        });

        _ = command.Parameters.Add(new OracleParameter
        {
            ParameterName = "P_IS_INTERNAL",
            OracleDbType = OracleDbType.Char,
            Direction = ParameterDirection.Input,
            Value = comment.IsInternal ? "Y" : "N"
        });

        _ = command.Parameters.Add(new OracleParameter
        {
            ParameterName = "P_CREATION_USER",
            OracleDbType = OracleDbType.NVarchar2,
            Direction = ParameterDirection.Input,
            Value = comment.CreationUser
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
    /// Gets the count of comments for a specific ticket.
    /// </summary>
    /// <param name="ticketId">The unique identifier of the ticket</param>
    /// <param name="includeInternal">Whether to include internal comments in count</param>
    /// <returns>The number of comments</returns>
    public async Task<int> GetCommentCountAsync(Int64 ticketId, bool includeInternal = false)
    {
        using var connection = _dbContext.CreateConnection();
        await connection.OpenAsync();

        using var command = connection.CreateCommand();
        command.CommandType = CommandType.Text;
        
        var whereClause = "WHERE TICKET_ID = :ticketId";
        if (!includeInternal)
        {
            whereClause += " AND IS_INTERNAL = 'N'";
        }

        command.CommandText = $@"
            SELECT COUNT(*) 
            FROM SYS_TICKET_COMMENT 
            {whereClause}";

        _ = command.Parameters.Add(new OracleParameter
        {
            ParameterName = "ticketId",
            OracleDbType = OracleDbType.Decimal,
            Direction = ParameterDirection.Input,
            Value = ticketId
        });

        var result = await command.ExecuteScalarAsync();
        return Convert.ToInt32(result);
    }

    /// <summary>
    /// Retrieves recent comments across all tickets for activity monitoring.
    /// </summary>
    /// <param name="companyId">Optional company filter for authorization</param>
    /// <param name="branchId">Optional branch filter for authorization</param>
    /// <param name="hours">Number of hours to look back for recent comments</param>
    /// <param name="limit">Maximum number of comments to return</param>
    /// <returns>A list of recent comments</returns>
    public async Task<List<SysTicketComment>> GetRecentCommentsAsync(
        Int64? companyId = null, 
        Int64? branchId = null, 
        int hours = 24, 
        int limit = 50)
    {
        List<SysTicketComment> comments = new();

        using var connection = _dbContext.CreateConnection();
        await connection.OpenAsync();

        using var command = connection.CreateCommand();
        command.CommandType = CommandType.Text;
        
        var whereClause = "WHERE c.CREATION_DATE >= SYSDATE - (:hours / 24)";
        
        if (companyId.HasValue)
        {
            whereClause += " AND t.COMPANY_ID = :companyId";
        }
        
        if (branchId.HasValue)
        {
            whereClause += " AND t.BRANCH_ID = :branchId";
        }

        command.CommandText = $@"
            SELECT * FROM (
                SELECT 
                    c.ROW_ID,
                    c.TICKET_ID,
                    c.COMMENT_TEXT,
                    c.IS_INTERNAL,
                    c.CREATION_USER,
                    c.CREATION_DATE
                FROM SYS_TICKET_COMMENT c
                INNER JOIN SYS_REQUEST_TICKET t ON c.TICKET_ID = t.ROW_ID
                {whereClause}
                AND t.IS_ACTIVE = 'Y'
                ORDER BY c.CREATION_DATE DESC
            ) WHERE ROWNUM <= :limit";

        _ = command.Parameters.Add(new OracleParameter
        {
            ParameterName = "hours",
            OracleDbType = OracleDbType.Decimal,
            Direction = ParameterDirection.Input,
            Value = hours
        });

        _ = command.Parameters.Add(new OracleParameter
        {
            ParameterName = "limit",
            OracleDbType = OracleDbType.Decimal,
            Direction = ParameterDirection.Input,
            Value = limit
        });

        if (companyId.HasValue)
        {
            _ = command.Parameters.Add(new OracleParameter
            {
                ParameterName = "companyId",
                OracleDbType = OracleDbType.Decimal,
                Direction = ParameterDirection.Input,
                Value = companyId.Value
            });
        }

        if (branchId.HasValue)
        {
            _ = command.Parameters.Add(new OracleParameter
            {
                ParameterName = "branchId",
                OracleDbType = OracleDbType.Decimal,
                Direction = ParameterDirection.Input,
                Value = branchId.Value
            });
        }

        using var reader = await command.ExecuteReaderAsync();
        while (await reader.ReadAsync())
        {
            comments.Add(MapToEntity(reader));
        }

        return comments;
    }

    /// <summary>
    /// Retrieves comments created by a specific user.
    /// </summary>
    /// <param name="userName">The username of the comment creator</param>
    /// <param name="companyId">Optional company filter for authorization</param>
    /// <param name="branchId">Optional branch filter for authorization</param>
    /// <param name="fromDate">Optional date range start</param>
    /// <param name="toDate">Optional date range end</param>
    /// <returns>A list of comments created by the user</returns>
    public async Task<List<SysTicketComment>> GetByUserAsync(
        string userName, 
        Int64? companyId = null, 
        Int64? branchId = null, 
        DateTime? fromDate = null, 
        DateTime? toDate = null)
    {
        List<SysTicketComment> comments = new();

        using var connection = _dbContext.CreateConnection();
        await connection.OpenAsync();

        using var command = connection.CreateCommand();
        command.CommandType = CommandType.Text;
        
        var whereClause = "WHERE c.CREATION_USER = :userName";
        
        if (companyId.HasValue)
        {
            whereClause += " AND t.COMPANY_ID = :companyId";
        }
        
        if (branchId.HasValue)
        {
            whereClause += " AND t.BRANCH_ID = :branchId";
        }

        if (fromDate.HasValue)
        {
            whereClause += " AND c.CREATION_DATE >= :fromDate";
        }

        if (toDate.HasValue)
        {
            whereClause += " AND c.CREATION_DATE <= :toDate";
        }

        command.CommandText = $@"
            SELECT 
                c.ROW_ID,
                c.TICKET_ID,
                c.COMMENT_TEXT,
                c.IS_INTERNAL,
                c.CREATION_USER,
                c.CREATION_DATE
            FROM SYS_TICKET_COMMENT c
            INNER JOIN SYS_REQUEST_TICKET t ON c.TICKET_ID = t.ROW_ID
            {whereClause}
            AND t.IS_ACTIVE = 'Y'
            ORDER BY c.CREATION_DATE DESC";

        _ = command.Parameters.Add(new OracleParameter
        {
            ParameterName = "userName",
            OracleDbType = OracleDbType.NVarchar2,
            Direction = ParameterDirection.Input,
            Value = userName
        });

        if (companyId.HasValue)
        {
            _ = command.Parameters.Add(new OracleParameter
            {
                ParameterName = "companyId",
                OracleDbType = OracleDbType.Decimal,
                Direction = ParameterDirection.Input,
                Value = companyId.Value
            });
        }

        if (branchId.HasValue)
        {
            _ = command.Parameters.Add(new OracleParameter
            {
                ParameterName = "branchId",
                OracleDbType = OracleDbType.Decimal,
                Direction = ParameterDirection.Input,
                Value = branchId.Value
            });
        }

        if (fromDate.HasValue)
        {
            _ = command.Parameters.Add(new OracleParameter
            {
                ParameterName = "fromDate",
                OracleDbType = OracleDbType.Date,
                Direction = ParameterDirection.Input,
                Value = fromDate.Value
            });
        }

        if (toDate.HasValue)
        {
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
            comments.Add(MapToEntity(reader));
        }

        return comments;
    }

    /// <summary>
    /// Searches comments by text content.
    /// </summary>
    /// <param name="searchTerm">The search term to look for in comment text</param>
    /// <param name="companyId">Optional company filter for authorization</param>
    /// <param name="branchId">Optional branch filter for authorization</param>
    /// <param name="includeInternal">Whether to include internal comments in search</param>
    /// <param name="page">Page number for pagination</param>
    /// <param name="pageSize">Number of records per page</param>
    /// <returns>A tuple containing search results and total count</returns>
    public async Task<(List<SysTicketComment> Comments, int TotalCount)> SearchCommentsAsync(
        string searchTerm,
        Int64? companyId = null,
        Int64? branchId = null,
        bool includeInternal = false,
        int page = 1,
        int pageSize = 20)
    {
        List<SysTicketComment> comments = new();
        int totalCount = 0;

        using var connection = _dbContext.CreateConnection();
        await connection.OpenAsync();

        // Build WHERE clause
        var whereClause = "WHERE UPPER(c.COMMENT_TEXT) LIKE UPPER(:searchTerm)";
        
        if (!includeInternal)
        {
            whereClause += " AND c.IS_INTERNAL = 'N'";
        }
        
        if (companyId.HasValue)
        {
            whereClause += " AND t.COMPANY_ID = :companyId";
        }
        
        if (branchId.HasValue)
        {
            whereClause += " AND t.BRANCH_ID = :branchId";
        }

        // First, get the total count
        using (var countCommand = connection.CreateCommand())
        {
            countCommand.CommandType = CommandType.Text;
            countCommand.CommandText = $@"
                SELECT COUNT(*) 
                FROM SYS_TICKET_COMMENT c
                INNER JOIN SYS_REQUEST_TICKET t ON c.TICKET_ID = t.ROW_ID
                {whereClause}
                AND t.IS_ACTIVE = 'Y'";

            _ = countCommand.Parameters.Add(new OracleParameter
            {
                ParameterName = "searchTerm",
                OracleDbType = OracleDbType.NVarchar2,
                Direction = ParameterDirection.Input,
                Value = $"%{searchTerm}%"
            });

            if (companyId.HasValue)
            {
                _ = countCommand.Parameters.Add(new OracleParameter
                {
                    ParameterName = "companyId",
                    OracleDbType = OracleDbType.Decimal,
                    Direction = ParameterDirection.Input,
                    Value = companyId.Value
                });
            }

            if (branchId.HasValue)
            {
                _ = countCommand.Parameters.Add(new OracleParameter
                {
                    ParameterName = "branchId",
                    OracleDbType = OracleDbType.Decimal,
                    Direction = ParameterDirection.Input,
                    Value = branchId.Value
                });
            }

            var result = await countCommand.ExecuteScalarAsync();
            totalCount = Convert.ToInt32(result);
        }

        // Then get the paginated results
        using var command = connection.CreateCommand();
        command.CommandType = CommandType.Text;
        
        var offset = (page - 1) * pageSize;
        
        command.CommandText = $@"
            SELECT * FROM (
                SELECT 
                    c.ROW_ID,
                    c.TICKET_ID,
                    c.COMMENT_TEXT,
                    c.IS_INTERNAL,
                    c.CREATION_USER,
                    c.CREATION_DATE,
                    ROW_NUMBER() OVER (ORDER BY c.CREATION_DATE DESC) AS RN
                FROM SYS_TICKET_COMMENT c
                INNER JOIN SYS_REQUEST_TICKET t ON c.TICKET_ID = t.ROW_ID
                {whereClause}
                AND t.IS_ACTIVE = 'Y'
            ) WHERE RN > :offset AND RN <= :endRow";

        _ = command.Parameters.Add(new OracleParameter
        {
            ParameterName = "searchTerm",
            OracleDbType = OracleDbType.NVarchar2,
            Direction = ParameterDirection.Input,
            Value = $"%{searchTerm}%"
        });

        _ = command.Parameters.Add(new OracleParameter
        {
            ParameterName = "offset",
            OracleDbType = OracleDbType.Decimal,
            Direction = ParameterDirection.Input,
            Value = offset
        });

        _ = command.Parameters.Add(new OracleParameter
        {
            ParameterName = "endRow",
            OracleDbType = OracleDbType.Decimal,
            Direction = ParameterDirection.Input,
            Value = offset + pageSize
        });

        if (companyId.HasValue)
        {
            _ = command.Parameters.Add(new OracleParameter
            {
                ParameterName = "companyId",
                OracleDbType = OracleDbType.Decimal,
                Direction = ParameterDirection.Input,
                Value = companyId.Value
            });
        }

        if (branchId.HasValue)
        {
            _ = command.Parameters.Add(new OracleParameter
            {
                ParameterName = "branchId",
                OracleDbType = OracleDbType.Decimal,
                Direction = ParameterDirection.Input,
                Value = branchId.Value
            });
        }

        using var reader = await command.ExecuteReaderAsync();
        while (await reader.ReadAsync())
        {
            comments.Add(MapToEntity(reader));
        }

        return (comments, totalCount);
    }

    /// <summary>
    /// Maps an OracleDataReader row to a SysTicketComment entity.
    /// Handles Oracle data type conversions to C# types.
    /// </summary>
    /// <param name="reader">The OracleDataReader positioned at a row</param>
    /// <returns>A SysTicketComment entity populated with data from the reader</returns>
    private SysTicketComment MapToEntity(OracleDataReader reader)
    {
        return new SysTicketComment
        {
            RowId = reader.GetInt64(reader.GetOrdinal("ROW_ID")),
            TicketId = reader.GetInt64(reader.GetOrdinal("TICKET_ID")),
            CommentText = reader.GetString(reader.GetOrdinal("COMMENT_TEXT")),
            IsInternal = MapIsInternalToBoolean(reader.GetString(reader.GetOrdinal("IS_INTERNAL"))),
            CreationUser = reader.GetString(reader.GetOrdinal("CREATION_USER")),
            CreationDate = reader.IsDBNull(reader.GetOrdinal("CREATION_DATE")) ? null : reader.GetDateTime(reader.GetOrdinal("CREATION_DATE"))
        };
    }

    /// <summary>
    /// Maps Oracle IS_INTERNAL values to C# boolean.
    /// Converts 'Y' or '1' to true, 'N' or '0' to false.
    /// </summary>
    /// <param name="value">The Oracle IS_INTERNAL value</param>
    /// <returns>True if value is 'Y' or '1', false otherwise</returns>
    private bool MapIsInternalToBoolean(string value)
    {
        return value is "Y" or "1";
    }
}