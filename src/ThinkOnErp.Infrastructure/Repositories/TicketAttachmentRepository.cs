using Oracle.ManagedDataAccess.Client;
using System.Data;
using ThinkOnErp.Domain.Entities;
using ThinkOnErp.Domain.Interfaces;
using ThinkOnErp.Infrastructure.Data;

namespace ThinkOnErp.Infrastructure.Repositories;

/// <summary>
/// Repository implementation for SysTicketAttachment entity using ADO.NET with Oracle stored procedures.
/// Implements ITicketAttachmentRepository interface from the Domain layer.
/// Uses OracleDbContext to create connections and maps Oracle data types to C# types.
/// Handles Base64 file storage and retrieval with security validation.
/// </summary>
public class TicketAttachmentRepository : ITicketAttachmentRepository
{
    private readonly OracleDbContext _dbContext;

    /// <summary>
    /// Initializes a new instance of the TicketAttachmentRepository class.
    /// </summary>
    /// <param name="dbContext">The Oracle database context for connection management.</param>
    /// <exception cref="ArgumentNullException">Thrown when dbContext is null.</exception>
    public TicketAttachmentRepository(OracleDbContext dbContext)
    {
        _dbContext = dbContext ?? throw new ArgumentNullException(nameof(dbContext));
    }

    /// <summary>
    /// Retrieves all attachments for a specific ticket.
    /// Calls SP_SYS_TICKET_ATTACHMENT_SELECT_BY_TICKET stored procedure.
    /// </summary>
    /// <param name="ticketId">The unique identifier of the ticket</param>
    /// <returns>A list of attachments for the ticket</returns>
    public async Task<List<SysTicketAttachment>> GetByTicketIdAsync(Int64 ticketId)
    {
        List<SysTicketAttachment> attachments = new();

        using (var connection = _dbContext.CreateConnection())
        {
            await connection.OpenAsync();

            using var command = connection.CreateCommand();
            command.CommandType = CommandType.StoredProcedure;
            command.CommandText = "SP_SYS_TICKET_ATTACHMENT_SELECT_BY_TICKET";

            // Add input parameter
            _ = command.Parameters.Add(new OracleParameter
            {
                ParameterName = "P_TICKET_ID",
                OracleDbType = OracleDbType.Decimal,
                Direction = ParameterDirection.Input,
                Value = ticketId
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
                attachments.Add(MapToEntityMetadata(reader));
            }
        }

        return attachments;
    }

    /// <summary>
    /// Retrieves a specific attachment by its ID.
    /// Calls SP_SYS_TICKET_ATTACHMENT_SELECT_BY_ID stored procedure.
    /// </summary>
    /// <param name="rowId">The unique identifier of the attachment</param>
    /// <returns>The SysTicketAttachment entity if found, null otherwise</returns>
    public async Task<SysTicketAttachment?> GetByIdAsync(Int64 rowId)
    {
        using var connection = _dbContext.CreateConnection();
        await connection.OpenAsync();

        using var command = connection.CreateCommand();
        command.CommandType = CommandType.StoredProcedure;
        command.CommandText = "SP_SYS_TICKET_ATTACHMENT_SELECT_BY_ID";

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
            return MapToEntityWithContent(reader);
        }

        return null;
    }

    /// <summary>
    /// Creates a new attachment in the database.
    /// Calls SP_SYS_TICKET_ATTACHMENT_INSERT stored procedure.
    /// Validates file size, type, and content before storage.
    /// </summary>
    /// <param name="attachment">The attachment entity to create</param>
    /// <returns>The generated RowId from SEQ_SYS_TICKET_ATTACHMENT sequence</returns>
    public async Task<Int64> CreateAsync(SysTicketAttachment attachment)
    {
        // Validate attachment before insertion
        if (!attachment.IsValid)
        {
            throw new ArgumentException("Attachment validation failed. Check file size, type, and content.");
        }

        using var connection = _dbContext.CreateConnection();
        await connection.OpenAsync();

        using var command = connection.CreateCommand();
        command.CommandType = CommandType.StoredProcedure;
        command.CommandText = "SP_SYS_TICKET_ATTACHMENT_INSERT";

        // Add input parameters
        _ = command.Parameters.Add(new OracleParameter
        {
            ParameterName = "P_TICKET_ID",
            OracleDbType = OracleDbType.Decimal,
            Direction = ParameterDirection.Input,
            Value = attachment.TicketId
        });

        _ = command.Parameters.Add(new OracleParameter
        {
            ParameterName = "P_FILE_NAME",
            OracleDbType = OracleDbType.NVarchar2,
            Direction = ParameterDirection.Input,
            Value = attachment.FileName
        });

        _ = command.Parameters.Add(new OracleParameter
        {
            ParameterName = "P_FILE_SIZE",
            OracleDbType = OracleDbType.Decimal,
            Direction = ParameterDirection.Input,
            Value = attachment.FileSize
        });

        _ = command.Parameters.Add(new OracleParameter
        {
            ParameterName = "P_MIME_TYPE",
            OracleDbType = OracleDbType.NVarchar2,
            Direction = ParameterDirection.Input,
            Value = attachment.MimeType
        });

        _ = command.Parameters.Add(new OracleParameter
        {
            ParameterName = "P_FILE_CONTENT",
            OracleDbType = OracleDbType.Blob,
            Direction = ParameterDirection.Input,
            Value = attachment.FileContent
        });

        _ = command.Parameters.Add(new OracleParameter
        {
            ParameterName = "P_CREATION_USER",
            OracleDbType = OracleDbType.NVarchar2,
            Direction = ParameterDirection.Input,
            Value = attachment.CreationUser
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
    /// Deletes an attachment from the database.
    /// Calls SP_SYS_TICKET_ATTACHMENT_DELETE stored procedure.
    /// </summary>
    /// <param name="rowId">The unique identifier of the attachment to delete</param>
    /// <param name="userName">The username of the user performing the deletion</param>
    /// <returns>The number of rows affected</returns>
    public async Task<Int64> DeleteAsync(Int64 rowId, string userName)
    {
        using var connection = _dbContext.CreateConnection();
        await connection.OpenAsync();

        using var command = connection.CreateCommand();
        command.CommandType = CommandType.StoredProcedure;
        command.CommandText = "SP_SYS_TICKET_ATTACHMENT_DELETE";

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
    /// Gets the count of attachments for a specific ticket.
    /// </summary>
    /// <param name="ticketId">The unique identifier of the ticket</param>
    /// <returns>The number of attachments</returns>
    public async Task<int> GetAttachmentCountAsync(Int64 ticketId)
    {
        using var connection = _dbContext.CreateConnection();
        await connection.OpenAsync();

        using var command = connection.CreateCommand();
        command.CommandType = CommandType.Text;
        command.CommandText = @"
            SELECT COUNT(*) 
            FROM SYS_TICKET_ATTACHMENT 
            WHERE TICKET_ID = :ticketId";

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
    /// Gets the total size of all attachments for a specific ticket.
    /// </summary>
    /// <param name="ticketId">The unique identifier of the ticket</param>
    /// <returns>The total size in bytes</returns>
    public async Task<Int64> GetTotalAttachmentSizeAsync(Int64 ticketId)
    {
        using var connection = _dbContext.CreateConnection();
        await connection.OpenAsync();

        using var command = connection.CreateCommand();
        command.CommandType = CommandType.Text;
        command.CommandText = @"
            SELECT COALESCE(SUM(FILE_SIZE), 0) 
            FROM SYS_TICKET_ATTACHMENT 
            WHERE TICKET_ID = :ticketId";

        _ = command.Parameters.Add(new OracleParameter
        {
            ParameterName = "ticketId",
            OracleDbType = OracleDbType.Decimal,
            Direction = ParameterDirection.Input,
            Value = ticketId
        });

        var result = await command.ExecuteScalarAsync();
        return Convert.ToInt64(result);
    }

    /// <summary>
    /// Retrieves attachment metadata without file content for listing purposes.
    /// Calls SP_SYS_TICKET_ATTACHMENT_SELECT_BY_TICKET stored procedure.
    /// </summary>
    /// <param name="ticketId">The unique identifier of the ticket</param>
    /// <returns>A list of attachments with metadata only (no file content)</returns>
    public async Task<List<SysTicketAttachment>> GetAttachmentMetadataAsync(Int64 ticketId)
    {
        // This is the same as GetByTicketIdAsync since the stored procedure already excludes BLOB content
        return await GetByTicketIdAsync(ticketId);
    }

    /// <summary>
    /// Retrieves the file content for a specific attachment for download.
    /// </summary>
    /// <param name="rowId">The unique identifier of the attachment</param>
    /// <returns>The file content as byte array, null if not found</returns>
    public async Task<byte[]?> GetFileContentAsync(Int64 rowId)
    {
        var attachment = await GetByIdAsync(rowId);
        return attachment?.FileContent;
    }

    /// <summary>
    /// Validates if adding a new attachment would exceed limits.
    /// Checks both count and size limits per ticket.
    /// </summary>
    /// <param name="ticketId">The unique identifier of the ticket</param>
    /// <param name="newFileSize">The size of the new file to be added</param>
    /// <returns>True if the attachment can be added, false if limits would be exceeded</returns>
    public async Task<bool> CanAddAttachmentAsync(Int64 ticketId, Int64 newFileSize)
    {
        // Check file size limit
        if (newFileSize > SysTicketAttachment.MaxFileSizeBytes)
        {
            return false;
        }

        // Check attachment count limit
        var currentCount = await GetAttachmentCountAsync(ticketId);
        if (currentCount >= SysTicketAttachment.MaxAttachmentsPerTicket)
        {
            return false;
        }

        return true;
    }

    /// <summary>
    /// Retrieves attachments by file type for analytics.
    /// </summary>
    /// <param name="mimeType">The MIME type to filter by</param>
    /// <param name="companyId">Optional company filter</param>
    /// <param name="branchId">Optional branch filter</param>
    /// <param name="fromDate">Optional date range start</param>
    /// <param name="toDate">Optional date range end</param>
    /// <returns>A list of attachments matching the criteria</returns>
    public async Task<List<SysTicketAttachment>> GetByFileTypeAsync(
        string mimeType,
        Int64? companyId = null,
        Int64? branchId = null,
        DateTime? fromDate = null,
        DateTime? toDate = null)
    {
        List<SysTicketAttachment> attachments = new();

        using var connection = _dbContext.CreateConnection();
        await connection.OpenAsync();

        using var command = connection.CreateCommand();
        command.CommandType = CommandType.Text;
        
        var whereClause = "WHERE a.MIME_TYPE = :mimeType";
        
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
            whereClause += " AND a.CREATION_DATE >= :fromDate";
        }

        if (toDate.HasValue)
        {
            whereClause += " AND a.CREATION_DATE <= :toDate";
        }

        command.CommandText = $@"
            SELECT 
                a.ROW_ID,
                a.TICKET_ID,
                a.FILE_NAME,
                a.FILE_SIZE,
                a.MIME_TYPE,
                a.CREATION_USER,
                a.CREATION_DATE
            FROM SYS_TICKET_ATTACHMENT a
            INNER JOIN SYS_REQUEST_TICKET t ON a.TICKET_ID = t.ROW_ID
            {whereClause}
            AND t.IS_ACTIVE = 'Y'
            ORDER BY a.CREATION_DATE DESC";

        _ = command.Parameters.Add(new OracleParameter
        {
            ParameterName = "mimeType",
            OracleDbType = OracleDbType.NVarchar2,
            Direction = ParameterDirection.Input,
            Value = mimeType
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
            attachments.Add(MapToEntityMetadata(reader));
        }

        return attachments;
    }

    /// <summary>
    /// Gets attachment statistics for reporting.
    /// </summary>
    /// <param name="companyId">Optional company filter</param>
    /// <param name="branchId">Optional branch filter</param>
    /// <param name="fromDate">Optional date range start</param>
    /// <param name="toDate">Optional date range end</param>
    /// <returns>Dictionary containing attachment statistics</returns>
    public async Task<Dictionary<string, object>> GetAttachmentStatisticsAsync(
        Int64? companyId = null,
        Int64? branchId = null,
        DateTime? fromDate = null,
        DateTime? toDate = null)
    {
        var statistics = new Dictionary<string, object>();

        using var connection = _dbContext.CreateConnection();
        await connection.OpenAsync();

        using var command = connection.CreateCommand();
        command.CommandType = CommandType.Text;
        
        var whereClause = "WHERE 1=1";
        
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
            whereClause += " AND a.CREATION_DATE >= :fromDate";
        }

        if (toDate.HasValue)
        {
            whereClause += " AND a.CREATION_DATE <= :toDate";
        }

        command.CommandText = $@"
            SELECT 
                COUNT(*) AS TOTAL_ATTACHMENTS,
                COUNT(DISTINCT a.TICKET_ID) AS TICKETS_WITH_ATTACHMENTS,
                SUM(a.FILE_SIZE) AS TOTAL_SIZE_BYTES,
                AVG(a.FILE_SIZE) AS AVERAGE_SIZE_BYTES,
                MAX(a.FILE_SIZE) AS MAX_SIZE_BYTES,
                MIN(a.FILE_SIZE) AS MIN_SIZE_BYTES
            FROM SYS_TICKET_ATTACHMENT a
            INNER JOIN SYS_REQUEST_TICKET t ON a.TICKET_ID = t.ROW_ID
            {whereClause}
            AND t.IS_ACTIVE = 'Y'";

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
        if (await reader.ReadAsync())
        {
            statistics["TotalAttachments"] = reader.GetInt32(reader.GetOrdinal("TOTAL_ATTACHMENTS"));
            statistics["TicketsWithAttachments"] = reader.GetInt32(reader.GetOrdinal("TICKETS_WITH_ATTACHMENTS"));
            statistics["TotalSizeBytes"] = reader.IsDBNull(reader.GetOrdinal("TOTAL_SIZE_BYTES")) ? 0L : reader.GetInt64(reader.GetOrdinal("TOTAL_SIZE_BYTES"));
            statistics["AverageSizeBytes"] = reader.IsDBNull(reader.GetOrdinal("AVERAGE_SIZE_BYTES")) ? 0.0 : reader.GetDouble(reader.GetOrdinal("AVERAGE_SIZE_BYTES"));
            statistics["MaxSizeBytes"] = reader.IsDBNull(reader.GetOrdinal("MAX_SIZE_BYTES")) ? 0L : reader.GetInt64(reader.GetOrdinal("MAX_SIZE_BYTES"));
            statistics["MinSizeBytes"] = reader.IsDBNull(reader.GetOrdinal("MIN_SIZE_BYTES")) ? 0L : reader.GetInt64(reader.GetOrdinal("MIN_SIZE_BYTES"));
        }

        return statistics;
    }

    /// <summary>
    /// Maps an OracleDataReader row to a SysTicketAttachment entity with metadata only (no BLOB content).
    /// Handles Oracle data type conversions to C# types.
    /// </summary>
    /// <param name="reader">The OracleDataReader positioned at a row</param>
    /// <returns>A SysTicketAttachment entity populated with metadata from the reader</returns>
    private SysTicketAttachment MapToEntityMetadata(OracleDataReader reader)
    {
        return new SysTicketAttachment
        {
            RowId = reader.GetInt64(reader.GetOrdinal("ROW_ID")),
            TicketId = reader.GetInt64(reader.GetOrdinal("TICKET_ID")),
            FileName = reader.GetString(reader.GetOrdinal("FILE_NAME")),
            FileSize = reader.GetInt64(reader.GetOrdinal("FILE_SIZE")),
            MimeType = reader.GetString(reader.GetOrdinal("MIME_TYPE")),
            FileContent = Array.Empty<byte>(), // No content in metadata queries for performance
            CreationUser = reader.GetString(reader.GetOrdinal("CREATION_USER")),
            CreationDate = reader.IsDBNull(reader.GetOrdinal("CREATION_DATE")) ? null : reader.GetDateTime(reader.GetOrdinal("CREATION_DATE"))
        };
    }

    /// <summary>
    /// Maps an OracleDataReader row to a SysTicketAttachment entity with full content including BLOB.
    /// Handles Oracle data type conversions to C# types.
    /// </summary>
    /// <param name="reader">The OracleDataReader positioned at a row</param>
    /// <returns>A SysTicketAttachment entity populated with complete data from the reader</returns>
    private SysTicketAttachment MapToEntityWithContent(OracleDataReader reader)
    {
        return new SysTicketAttachment
        {
            RowId = reader.GetInt64(reader.GetOrdinal("ROW_ID")),
            TicketId = reader.GetInt64(reader.GetOrdinal("TICKET_ID")),
            FileName = reader.GetString(reader.GetOrdinal("FILE_NAME")),
            FileSize = reader.GetInt64(reader.GetOrdinal("FILE_SIZE")),
            MimeType = reader.GetString(reader.GetOrdinal("MIME_TYPE")),
            FileContent = reader.IsDBNull(reader.GetOrdinal("FILE_CONTENT")) ? 
                         Array.Empty<byte>() : 
                         (byte[])reader.GetValue(reader.GetOrdinal("FILE_CONTENT")),
            CreationUser = reader.GetString(reader.GetOrdinal("CREATION_USER")),
            CreationDate = reader.IsDBNull(reader.GetOrdinal("CREATION_DATE")) ? null : reader.GetDateTime(reader.GetOrdinal("CREATION_DATE"))
        };
    }
}