using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using ThinkOnErp.Domain.Entities;
using ThinkOnErp.Domain.Interfaces;
using ThinkOnErp.Infrastructure.Data;
using ThinkOnErp.Infrastructure.Exceptions;

namespace ThinkOnErp.Infrastructure.Repositories.EfCore;

/// <summary>
/// EF Core repository implementation for SysTicketAttachment entity using LINQ queries.
/// Implements ITicketAttachmentRepository interface from the Domain layer.
/// Uses ThinkOnErpDbContext for database operations with BLOB handling for file content.
/// </summary>
public class TicketAttachmentRepository : ITicketAttachmentRepository
{
    private readonly ThinkOnErpDbContext _context;
    private readonly ILogger<TicketAttachmentRepository> _logger;

    /// <summary>
    /// Initializes a new instance of the TicketAttachmentRepository class.
    /// </summary>
    /// <param name="context">The EF Core database context for entity management.</param>
    /// <param name="logger">Logger for recording operations and errors.</param>
    /// <exception cref="ArgumentNullException">Thrown when context or logger is null.</exception>
    public TicketAttachmentRepository(
        ThinkOnErpDbContext context,
        ILogger<TicketAttachmentRepository> logger)
    {
        _context = context ?? throw new ArgumentNullException(nameof(context));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <summary>
    /// Retrieves all attachments for a specific ticket.
    /// Uses LINQ with Where clause to filter by ticket ID.
    /// Includes full file content (BLOB data).
    /// </summary>
    /// <param name="ticketId">The unique identifier of the ticket</param>
    /// <returns>A list of attachments for the ticket</returns>
    public async Task<List<SysTicketAttachment>> GetByTicketIdAsync(long ticketId)
    {
        return await RepositoryExceptionHandler.ExecuteWithExceptionHandlingAsync(
            async () =>
            {
                _logger.LogDebug("Retrieving attachments for ticket ID: {TicketId}", ticketId);

                var attachments = await _context.TicketAttachments
                    .AsNoTracking()
                    .Where(a => a.TicketId == ticketId)
                    .OrderBy(a => a.CreationDate)
                    .ToListAsync();

                _logger.LogDebug("Retrieved {Count} attachments for ticket ID: {TicketId}", 
                    attachments.Count, ticketId);

                return attachments;
            },
            "GetAttachmentsByTicketId",
            _logger);
    }

    /// <summary>
    /// Retrieves a specific attachment by its ID.
    /// Uses LINQ FirstOrDefaultAsync for single record retrieval.
    /// Includes full file content (BLOB data).
    /// </summary>
    /// <param name="rowId">The unique identifier of the attachment</param>
    /// <returns>The SysTicketAttachment entity if found, null otherwise</returns>
    public async Task<SysTicketAttachment?> GetByIdAsync(long rowId)
    {
        return await RepositoryExceptionHandler.ExecuteWithExceptionHandlingAsync(
            async () =>
            {
                _logger.LogDebug("Retrieving attachment with ID: {RowId}", rowId);

                var attachment = await _context.TicketAttachments
                    .AsNoTracking()
                    .FirstOrDefaultAsync(a => a.RowId == rowId);

                if (attachment == null)
                {
                    _logger.LogDebug("Attachment with ID {RowId} not found", rowId);
                }
                else
                {
                    _logger.LogDebug("Retrieved attachment: {FileName}, Size: {FileSize} bytes", 
                        attachment.FileName, attachment.FileSize);
                }

                return attachment;
            },
            "GetAttachmentById",
            _logger);
    }

    /// <summary>
    /// Creates a new attachment in the database using EF Core.
    /// EF Core automatically retrieves the generated ID from Oracle sequence SEQ_SYS_TICKET_ATTACHMENT.
    /// Handles BLOB storage for file content (byte array).
    /// </summary>
    /// <param name="attachment">The attachment entity to create</param>
    /// <returns>The generated RowId from SEQ_SYS_TICKET_ATTACHMENT sequence</returns>
    public async Task<long> CreateAsync(SysTicketAttachment attachment)
    {
        return await RepositoryExceptionHandler.ExecuteWithExceptionHandlingAsync(
            async () =>
            {
                _logger.LogDebug("Creating new attachment for ticket ID: {TicketId}, FileName: {FileName}, Size: {FileSize} bytes", 
                    attachment.TicketId, attachment.FileName, attachment.FileSize);

                // Set creation date if not already set
                if (!attachment.CreationDate.HasValue)
                {
                    attachment.CreationDate = DateTime.Now;
                }

                // Validate attachment before saving
                if (!attachment.IsValid)
                {
                    var errorMessage = "Invalid attachment: ";
                    if (!attachment.IsFileSizeValid)
                        errorMessage += "File size exceeds maximum allowed. ";
                    if (!attachment.IsFileExtensionValid)
                        errorMessage += "File extension not allowed. ";
                    if (!attachment.IsMimeTypeValid)
                        errorMessage += "MIME type not allowed. ";

                    _logger.LogWarning("Attachment validation failed: {ErrorMessage}", errorMessage);
                    throw new InvalidOperationException(errorMessage.Trim());
                }

                // Add the entity to the context
                _context.TicketAttachments.Add(attachment);

                // Save changes - EF Core will automatically retrieve the generated ID from the sequence
                await _context.SaveChangesAsync();

                _logger.LogInformation("Created attachment with ID: {RowId} for ticket ID: {TicketId}, FileName: {FileName}", 
                    attachment.RowId, attachment.TicketId, attachment.FileName);

                return attachment.RowId;
            },
            "CreateAttachment",
            _logger);
    }

    /// <summary>
    /// Deletes an attachment from the database (hard delete).
    /// Uses Remove method to delete the entity permanently.
    /// </summary>
    /// <param name="rowId">The unique identifier of the attachment to delete</param>
    /// <param name="userName">The username of the user performing the deletion</param>
    /// <returns>The number of rows affected</returns>
    public async Task<long> DeleteAsync(long rowId, string userName)
    {
        return await RepositoryExceptionHandler.ExecuteWithExceptionHandlingAsync(
            async () =>
            {
                _logger.LogDebug("Deleting attachment with ID: {RowId} by user: {UserName}", rowId, userName);

                // Find the attachment to delete
                var attachment = await _context.TicketAttachments
                    .FirstOrDefaultAsync(a => a.RowId == rowId);

                if (attachment == null)
                {
                    _logger.LogWarning("Attachment with ID {RowId} not found for deletion", rowId);
                    return 0;
                }

                // Perform hard delete by removing the entity
                _context.TicketAttachments.Remove(attachment);

                var rowsAffected = await _context.SaveChangesAsync();

                if (rowsAffected > 0)
                {
                    _logger.LogInformation("Deleted attachment with ID: {RowId}, FileName: {FileName} by user: {UserName}", 
                        rowId, attachment.FileName, userName);
                }

                return rowsAffected;
            },
            "DeleteAttachment",
            _logger);
    }

    /// <summary>
    /// Gets the count of attachments for a specific ticket.
    /// Uses LINQ CountAsync for efficient counting.
    /// </summary>
    /// <param name="ticketId">The unique identifier of the ticket</param>
    /// <returns>The number of attachments</returns>
    public async Task<int> GetAttachmentCountAsync(long ticketId)
    {
        return await RepositoryExceptionHandler.ExecuteWithExceptionHandlingAsync(
            async () =>
            {
                _logger.LogDebug("Counting attachments for ticket ID: {TicketId}", ticketId);

                var count = await _context.TicketAttachments
                    .Where(a => a.TicketId == ticketId)
                    .CountAsync();

                _logger.LogDebug("Attachment count for ticket ID {TicketId}: {Count}", ticketId, count);

                return count;
            },
            "GetAttachmentCount",
            _logger);
    }

    /// <summary>
    /// Gets the total size of all attachments for a specific ticket.
    /// Uses LINQ SumAsync to calculate total file size.
    /// </summary>
    /// <param name="ticketId">The unique identifier of the ticket</param>
    /// <returns>The total size in bytes</returns>
    public async Task<long> GetTotalAttachmentSizeAsync(long ticketId)
    {
        return await RepositoryExceptionHandler.ExecuteWithExceptionHandlingAsync(
            async () =>
            {
                _logger.LogDebug("Calculating total attachment size for ticket ID: {TicketId}", ticketId);

                var totalSize = await _context.TicketAttachments
                    .Where(a => a.TicketId == ticketId)
                    .SumAsync(a => a.FileSize);

                _logger.LogDebug("Total attachment size for ticket ID {TicketId}: {TotalSize} bytes", 
                    ticketId, totalSize);

                return totalSize;
            },
            "GetTotalAttachmentSize",
            _logger);
    }

    /// <summary>
    /// Retrieves attachment metadata without file content for listing purposes.
    /// Uses LINQ projection with Select to exclude FileContent BLOB field.
    /// </summary>
    /// <param name="ticketId">The unique identifier of the ticket</param>
    /// <returns>A list of attachments with metadata only (no file content)</returns>
    public async Task<List<SysTicketAttachment>> GetAttachmentMetadataAsync(long ticketId)
    {
        return await RepositoryExceptionHandler.ExecuteWithExceptionHandlingAsync(
            async () =>
            {
                _logger.LogDebug("Retrieving attachment metadata for ticket ID: {TicketId}", ticketId);

                var attachments = await _context.TicketAttachments
                    .AsNoTracking()
                    .Where(a => a.TicketId == ticketId)
                    .Select(a => new SysTicketAttachment
                    {
                        RowId = a.RowId,
                        TicketId = a.TicketId,
                        FileName = a.FileName,
                        FileSize = a.FileSize,
                        MimeType = a.MimeType,
                        CreationUser = a.CreationUser,
                        CreationDate = a.CreationDate,
                        FileContent = Array.Empty<byte>() // Exclude BLOB content for performance
                    })
                    .OrderBy(a => a.CreationDate)
                    .ToListAsync();

                _logger.LogDebug("Retrieved metadata for {Count} attachments for ticket ID: {TicketId}", 
                    attachments.Count, ticketId);

                return attachments;
            },
            "GetAttachmentMetadata",
            _logger);
    }

    /// <summary>
    /// Retrieves the file content for a specific attachment for download.
    /// Uses LINQ projection to select only the FileContent BLOB field.
    /// </summary>
    /// <param name="rowId">The unique identifier of the attachment</param>
    /// <returns>The file content as byte array, null if not found</returns>
    public async Task<byte[]?> GetFileContentAsync(long rowId)
    {
        return await RepositoryExceptionHandler.ExecuteWithExceptionHandlingAsync(
            async () =>
            {
                _logger.LogDebug("Retrieving file content for attachment ID: {RowId}", rowId);

                // Use projection to select only the file content column for efficiency
                var fileContent = await _context.TicketAttachments
                    .AsNoTracking()
                    .Where(a => a.RowId == rowId)
                    .Select(a => a.FileContent)
                    .FirstOrDefaultAsync();

                if (fileContent == null || fileContent.Length == 0)
                {
                    _logger.LogDebug("File content not found for attachment ID {RowId}", rowId);
                }
                else
                {
                    _logger.LogDebug("Retrieved file content for attachment ID {RowId}, Size: {Size} bytes", 
                        rowId, fileContent.Length);
                }

                return fileContent;
            },
            "GetFileContent",
            _logger);
    }

    /// <summary>
    /// Validates if adding a new attachment would exceed limits.
    /// Checks both count and size limits per ticket.
    /// </summary>
    /// <param name="ticketId">The unique identifier of the ticket</param>
    /// <param name="newFileSize">The size of the new file to be added</param>
    /// <returns>True if the attachment can be added, false if limits would be exceeded</returns>
    public async Task<bool> CanAddAttachmentAsync(long ticketId, long newFileSize)
    {
        return await RepositoryExceptionHandler.ExecuteWithExceptionHandlingAsync(
            async () =>
            {
                _logger.LogDebug("Checking if attachment can be added to ticket ID: {TicketId}, NewFileSize: {NewFileSize} bytes", 
                    ticketId, newFileSize);

                // Check count limit
                var currentCount = await GetAttachmentCountAsync(ticketId);
                if (currentCount >= SysTicketAttachment.MaxAttachmentsPerTicket)
                {
                    _logger.LogWarning("Cannot add attachment: ticket ID {TicketId} already has {Count} attachments (max: {Max})", 
                        ticketId, currentCount, SysTicketAttachment.MaxAttachmentsPerTicket);
                    return false;
                }

                // Check file size limit
                if (newFileSize > SysTicketAttachment.MaxFileSizeBytes)
                {
                    _logger.LogWarning("Cannot add attachment: file size {FileSize} bytes exceeds maximum {Max} bytes", 
                        newFileSize, SysTicketAttachment.MaxFileSizeBytes);
                    return false;
                }

                _logger.LogDebug("Attachment can be added to ticket ID: {TicketId}", ticketId);
                return true;
            },
            "CanAddAttachment",
            _logger);
    }

    /// <summary>
    /// Retrieves attachments by file type for analytics.
    /// Uses LINQ with Where clause for filtering by MIME type and optional filters.
    /// </summary>
    /// <param name="mimeType">The MIME type to filter by</param>
    /// <param name="companyId">Optional company filter</param>
    /// <param name="branchId">Optional branch filter</param>
    /// <param name="fromDate">Optional date range start</param>
    /// <param name="toDate">Optional date range end</param>
    /// <returns>A list of attachments matching the criteria</returns>
    public async Task<List<SysTicketAttachment>> GetByFileTypeAsync(
        string mimeType,
        long? companyId = null,
        long? branchId = null,
        DateTime? fromDate = null,
        DateTime? toDate = null)
    {
        return await RepositoryExceptionHandler.ExecuteWithExceptionHandlingAsync(
            async () =>
            {
                _logger.LogDebug("Retrieving attachments by MIME type: {MimeType}", mimeType);

                var query = _context.TicketAttachments
                    .AsNoTracking()
                    .Include(a => a.Ticket)
                    .Where(a => a.MimeType == mimeType);

                // Apply company filter if provided
                if (companyId.HasValue)
                {
                    query = query.Where(a => a.Ticket != null && a.Ticket.CompanyId == companyId.Value);
                }

                // Apply branch filter if provided
                if (branchId.HasValue)
                {
                    query = query.Where(a => a.Ticket != null && a.Ticket.BranchId == branchId.Value);
                }

                // Apply date range filters if provided
                if (fromDate.HasValue)
                {
                    query = query.Where(a => a.CreationDate >= fromDate.Value);
                }

                if (toDate.HasValue)
                {
                    query = query.Where(a => a.CreationDate <= toDate.Value);
                }

                var attachments = await query
                    .OrderByDescending(a => a.CreationDate)
                    .ToListAsync();

                _logger.LogDebug("Retrieved {Count} attachments with MIME type: {MimeType}", 
                    attachments.Count, mimeType);

                return attachments;
            },
            "GetAttachmentsByFileType",
            _logger);
    }

    /// <summary>
    /// Gets attachment statistics for reporting.
    /// Uses LINQ aggregation functions to calculate statistics.
    /// </summary>
    /// <param name="companyId">Optional company filter</param>
    /// <param name="branchId">Optional branch filter</param>
    /// <param name="fromDate">Optional date range start</param>
    /// <param name="toDate">Optional date range end</param>
    /// <returns>Dictionary containing attachment statistics</returns>
    public async Task<Dictionary<string, object>> GetAttachmentStatisticsAsync(
        long? companyId = null,
        long? branchId = null,
        DateTime? fromDate = null,
        DateTime? toDate = null)
    {
        return await RepositoryExceptionHandler.ExecuteWithExceptionHandlingAsync(
            async () =>
            {
                _logger.LogDebug("Calculating attachment statistics");

                var query = _context.TicketAttachments
                    .AsNoTracking()
                    .Include(a => a.Ticket)
                    .AsQueryable();

                // Apply company filter if provided
                if (companyId.HasValue)
                {
                    query = query.Where(a => a.Ticket != null && a.Ticket.CompanyId == companyId.Value);
                }

                // Apply branch filter if provided
                if (branchId.HasValue)
                {
                    query = query.Where(a => a.Ticket != null && a.Ticket.BranchId == branchId.Value);
                }

                // Apply date range filters if provided
                if (fromDate.HasValue)
                {
                    query = query.Where(a => a.CreationDate >= fromDate.Value);
                }

                if (toDate.HasValue)
                {
                    query = query.Where(a => a.CreationDate <= toDate.Value);
                }

                // Calculate statistics
                var totalCount = await query.CountAsync();
                var totalSize = await query.SumAsync(a => a.FileSize);
                var averageSize = totalCount > 0 ? totalSize / totalCount : 0;

                // Get file type distribution
                var fileTypeDistribution = await query
                    .GroupBy(a => a.MimeType)
                    .Select(g => new { MimeType = g.Key, Count = g.Count() })
                    .OrderByDescending(x => x.Count)
                    .Take(10)
                    .ToDictionaryAsync(x => x.MimeType, x => (object)x.Count);

                var statistics = new Dictionary<string, object>
                {
                    { "TotalCount", totalCount },
                    { "TotalSize", totalSize },
                    { "AverageSize", averageSize },
                    { "FileTypeDistribution", fileTypeDistribution }
                };

                _logger.LogDebug("Calculated attachment statistics: TotalCount={TotalCount}, TotalSize={TotalSize} bytes", 
                    totalCount, totalSize);

                return statistics;
            },
            "GetAttachmentStatistics",
            _logger);
    }
}
