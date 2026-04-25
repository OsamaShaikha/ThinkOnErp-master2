using ThinkOnErp.Domain.Entities;

namespace ThinkOnErp.Domain.Interfaces;

/// <summary>
/// Repository interface for SysTicketAttachment entity data access operations.
/// Defines the contract for ticket attachment management in the Domain layer with zero external dependencies.
/// Handles file storage, retrieval, and security validation.
/// </summary>
public interface ITicketAttachmentRepository
{
    /// <summary>
    /// Retrieves all attachments for a specific ticket.
    /// Calls SP_SYS_TICKET_ATTACHMENT_SELECT_BY_TICKET stored procedure.
    /// </summary>
    /// <param name="ticketId">The unique identifier of the ticket</param>
    /// <returns>A list of attachments for the ticket</returns>
    Task<List<SysTicketAttachment>> GetByTicketIdAsync(Int64 ticketId);

    /// <summary>
    /// Retrieves a specific attachment by its ID.
    /// Calls SP_SYS_TICKET_ATTACHMENT_SELECT_BY_ID stored procedure.
    /// </summary>
    /// <param name="rowId">The unique identifier of the attachment</param>
    /// <returns>The SysTicketAttachment entity if found, null otherwise</returns>
    Task<SysTicketAttachment?> GetByIdAsync(Int64 rowId);

    /// <summary>
    /// Creates a new attachment in the database.
    /// Calls SP_SYS_TICKET_ATTACHMENT_INSERT stored procedure.
    /// Validates file size, type, and content before storage.
    /// </summary>
    /// <param name="attachment">The attachment entity to create</param>
    /// <returns>The generated RowId from SEQ_SYS_TICKET_ATTACHMENT sequence</returns>
    Task<Int64> CreateAsync(SysTicketAttachment attachment);

    /// <summary>
    /// Deletes an attachment from the database.
    /// Calls SP_SYS_TICKET_ATTACHMENT_DELETE stored procedure.
    /// </summary>
    /// <param name="rowId">The unique identifier of the attachment to delete</param>
    /// <param name="userName">The username of the user performing the deletion</param>
    /// <returns>The number of rows affected</returns>
    Task<Int64> DeleteAsync(Int64 rowId, string userName);

    /// <summary>
    /// Gets the count of attachments for a specific ticket.
    /// Calls SP_SYS_TICKET_ATTACHMENT_COUNT_BY_TICKET stored procedure.
    /// </summary>
    /// <param name="ticketId">The unique identifier of the ticket</param>
    /// <returns>The number of attachments</returns>
    Task<int> GetAttachmentCountAsync(Int64 ticketId);

    /// <summary>
    /// Gets the total size of all attachments for a specific ticket.
    /// Calls SP_SYS_TICKET_ATTACHMENT_SIZE_BY_TICKET stored procedure.
    /// </summary>
    /// <param name="ticketId">The unique identifier of the ticket</param>
    /// <returns>The total size in bytes</returns>
    Task<Int64> GetTotalAttachmentSizeAsync(Int64 ticketId);

    /// <summary>
    /// Retrieves attachment metadata without file content for listing purposes.
    /// Calls SP_SYS_TICKET_ATTACHMENT_SELECT_METADATA stored procedure.
    /// </summary>
    /// <param name="ticketId">The unique identifier of the ticket</param>
    /// <returns>A list of attachments with metadata only (no file content)</returns>
    Task<List<SysTicketAttachment>> GetAttachmentMetadataAsync(Int64 ticketId);

    /// <summary>
    /// Retrieves the file content for a specific attachment for download.
    /// Calls SP_SYS_TICKET_ATTACHMENT_GET_CONTENT stored procedure.
    /// </summary>
    /// <param name="rowId">The unique identifier of the attachment</param>
    /// <returns>The file content as byte array, null if not found</returns>
    Task<byte[]?> GetFileContentAsync(Int64 rowId);

    /// <summary>
    /// Validates if adding a new attachment would exceed limits.
    /// Checks both count and size limits per ticket.
    /// </summary>
    /// <param name="ticketId">The unique identifier of the ticket</param>
    /// <param name="newFileSize">The size of the new file to be added</param>
    /// <returns>True if the attachment can be added, false if limits would be exceeded</returns>
    Task<bool> CanAddAttachmentAsync(Int64 ticketId, Int64 newFileSize);

    /// <summary>
    /// Retrieves attachments by file type for analytics.
    /// Calls SP_SYS_TICKET_ATTACHMENT_SELECT_BY_TYPE stored procedure.
    /// </summary>
    /// <param name="mimeType">The MIME type to filter by</param>
    /// <param name="companyId">Optional company filter</param>
    /// <param name="branchId">Optional branch filter</param>
    /// <param name="fromDate">Optional date range start</param>
    /// <param name="toDate">Optional date range end</param>
    /// <returns>A list of attachments matching the criteria</returns>
    Task<List<SysTicketAttachment>> GetByFileTypeAsync(
        string mimeType,
        Int64? companyId = null,
        Int64? branchId = null,
        DateTime? fromDate = null,
        DateTime? toDate = null);

    /// <summary>
    /// Gets attachment statistics for reporting.
    /// Calls SP_SYS_TICKET_ATTACHMENT_STATS stored procedure.
    /// </summary>
    /// <param name="companyId">Optional company filter</param>
    /// <param name="branchId">Optional branch filter</param>
    /// <param name="fromDate">Optional date range start</param>
    /// <param name="toDate">Optional date range end</param>
    /// <returns>Dictionary containing attachment statistics</returns>
    Task<Dictionary<string, object>> GetAttachmentStatisticsAsync(
        Int64? companyId = null,
        Int64? branchId = null,
        DateTime? fromDate = null,
        DateTime? toDate = null);
}