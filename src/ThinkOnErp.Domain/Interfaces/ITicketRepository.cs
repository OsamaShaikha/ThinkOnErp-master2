using ThinkOnErp.Domain.Entities;

namespace ThinkOnErp.Domain.Interfaces;

/// <summary>
/// Repository interface for SysRequestTicket entity data access operations.
/// Defines the contract for ticket management in the Domain layer with zero external dependencies.
/// Includes CRUD operations, search functionality, and specialized ticket operations.
/// </summary>
public interface ITicketRepository
{
    /// <summary>
    /// Retrieves all active tickets with optional filtering and pagination.
    /// Calls SP_SYS_REQUEST_TICKET_SELECT_ALL stored procedure.
    /// </summary>
    /// <param name="companyId">Optional company filter for authorization</param>
    /// <param name="branchId">Optional branch filter for authorization</param>
    /// <param name="assigneeId">Optional assignee filter</param>
    /// <param name="statusId">Optional status filter</param>
    /// <param name="priorityId">Optional priority filter</param>
    /// <param name="typeId">Optional type filter</param>
    /// <param name="searchTerm">Optional search term for title/description</param>
    /// <param name="createdFrom">Optional creation date range start</param>
    /// <param name="createdTo">Optional creation date range end</param>
    /// <param name="page">Page number for pagination (1-based)</param>
    /// <param name="pageSize">Number of records per page</param>
    /// <param name="sortBy">Sort field (CreationDate, Priority, Status, etc.)</param>
    /// <param name="sortDirection">Sort direction (ASC/DESC)</param>
    /// <returns>A tuple containing the list of tickets and total count for pagination</returns>
    Task<(List<SysRequestTicket> Tickets, int TotalCount)> GetAllAsync(
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
        string sortDirection = "DESC");

    /// <summary>
    /// Retrieves a specific ticket by its ID with full navigation properties.
    /// Calls SP_SYS_REQUEST_TICKET_SELECT_BY_ID stored procedure.
    /// </summary>
    /// <param name="rowId">The unique identifier of the ticket</param>
    /// <returns>The SysRequestTicket entity if found, null otherwise</returns>
    Task<SysRequestTicket?> GetByIdAsync(Int64 rowId);

    /// <summary>
    /// Creates a new ticket in the database with SLA calculation.
    /// Calls SP_SYS_REQUEST_TICKET_INSERT stored procedure.
    /// </summary>
    /// <param name="ticket">The ticket entity to create</param>
    /// <returns>The generated RowId from SEQ_SYS_REQUEST_TICKET sequence</returns>
    Task<Int64> CreateAsync(SysRequestTicket ticket);

    /// <summary>
    /// Updates an existing ticket in the database with audit trail.
    /// Calls SP_SYS_REQUEST_TICKET_UPDATE stored procedure.
    /// </summary>
    /// <param name="ticket">The ticket entity with updated values</param>
    /// <returns>The number of rows affected</returns>
    Task<Int64> UpdateAsync(SysRequestTicket ticket);

    /// <summary>
    /// Performs a soft delete on a ticket by setting IS_ACTIVE to false.
    /// Calls SP_SYS_REQUEST_TICKET_DELETE stored procedure.
    /// </summary>
    /// <param name="rowId">The unique identifier of the ticket to delete</param>
    /// <param name="userName">The username of the user performing the deletion</param>
    /// <returns>The number of rows affected</returns>
    Task<Int64> DeleteAsync(Int64 rowId, string userName);

    /// <summary>
    /// Assigns a ticket to a support staff member.
    /// Calls SP_SYS_REQUEST_TICKET_ASSIGN stored procedure.
    /// </summary>
    /// <param name="ticketId">The unique identifier of the ticket</param>
    /// <param name="assigneeId">The unique identifier of the assignee (null to unassign)</param>
    /// <param name="userName">The username of the user making the assignment</param>
    /// <returns>The number of rows affected</returns>
    Task<Int64> AssignTicketAsync(Int64 ticketId, Int64? assigneeId, string userName);

    /// <summary>
    /// Updates the status of a ticket with workflow validation.
    /// Calls SP_SYS_REQUEST_TICKET_UPDATE_STATUS stored procedure.
    /// </summary>
    /// <param name="ticketId">The unique identifier of the ticket</param>
    /// <param name="newStatusId">The new status ID</param>
    /// <param name="statusChangeReason">Optional reason for the status change</param>
    /// <param name="userName">The username of the user making the change</param>
    /// <returns>The number of rows affected</returns>
    Task<Int64> UpdateStatusAsync(Int64 ticketId, Int64 newStatusId, string? statusChangeReason, string userName);

    /// <summary>
    /// Retrieves tickets that are overdue based on SLA targets.
    /// Calls SP_SYS_REQUEST_TICKET_SELECT_OVERDUE stored procedure.
    /// </summary>
    /// <param name="companyId">Optional company filter</param>
    /// <param name="branchId">Optional branch filter</param>
    /// <returns>A list of overdue tickets</returns>
    Task<List<SysRequestTicket>> GetOverdueTicketsAsync(Int64? companyId = null, Int64? branchId = null);

    /// <summary>
    /// Retrieves tickets that are overdue based on current time.
    /// </summary>
    /// <param name="currentTime">Current time to compare against expected resolution date</param>
    /// <returns>A list of overdue tickets</returns>
    Task<List<SysRequestTicket>> GetOverdueTicketsAsync(DateTime currentTime);

    /// <summary>
    /// Retrieves tickets approaching SLA deadline for escalation alerts.
    /// Calls SP_SYS_REQUEST_TICKET_SELECT_ESCALATION stored procedure.
    /// </summary>
    /// <param name="hoursBeforeDeadline">Hours before SLA deadline to trigger escalation</param>
    /// <param name="companyId">Optional company filter</param>
    /// <param name="branchId">Optional branch filter</param>
    /// <returns>A list of tickets requiring escalation</returns>
    Task<List<SysRequestTicket>> GetTicketsForEscalationAsync(int hoursBeforeDeadline = 2, Int64? companyId = null, Int64? branchId = null);

    /// <summary>
    /// Retrieves tickets approaching SLA deadline based on cutoff time.
    /// </summary>
    /// <param name="cutoffTime">Time threshold for approaching deadline</param>
    /// <returns>A list of tickets approaching deadline</returns>
    Task<List<SysRequestTicket>> GetTicketsApproachingSlaDeadlineAsync(DateTime cutoffTime);

    /// <summary>
    /// Retrieves tickets assigned to a specific user.
    /// </summary>
    /// <param name="assigneeId">The unique identifier of the assignee</param>
    /// <param name="includeResolved">Whether to include resolved tickets</param>
    /// <returns>A list of assigned tickets</returns>
    Task<List<SysRequestTicket>> GetTicketsByAssigneeAsync(Int64 assigneeId, bool includeResolved = false);

    /// <summary>
    /// Retrieves tickets created by a specific user.
    /// </summary>
    /// <param name="requesterId">The unique identifier of the requester</param>
    /// <param name="companyId">Company ID for authorization</param>
    /// <param name="branchId">Branch ID for authorization</param>
    /// <returns>A list of tickets created by the user</returns>
    Task<List<SysRequestTicket>> GetTicketsByRequesterAsync(Int64 requesterId, Int64 companyId, Int64 branchId);

    /// <summary>
    /// Performs full-text search across ticket titles and descriptions.
    /// Calls SP_SYS_REQUEST_TICKET_SEARCH stored procedure.
    /// </summary>
    /// <param name="searchTerm">The search term</param>
    /// <param name="companyId">Optional company filter for authorization</param>
    /// <param name="branchId">Optional branch filter for authorization</param>
    /// <param name="page">Page number for pagination</param>
    /// <param name="pageSize">Number of records per page</param>
    /// <returns>A tuple containing search results and total count</returns>
    Task<(List<SysRequestTicket> Tickets, int TotalCount)> SearchTicketsAsync(
        string searchTerm, 
        Int64? companyId = null, 
        Int64? branchId = null, 
        int page = 1, 
        int pageSize = 20);

    /// <summary>
    /// Gets ticket statistics for reporting and dashboard.
    /// Calls SP_SYS_REQUEST_TICKET_STATS stored procedure.
    /// </summary>
    /// <param name="companyId">Optional company filter</param>
    /// <param name="branchId">Optional branch filter</param>
    /// <param name="fromDate">Optional date range start</param>
    /// <param name="toDate">Optional date range end</param>
    /// <returns>Dictionary containing various statistics</returns>
    Task<Dictionary<string, object>> GetTicketStatisticsAsync(
        Int64? companyId = null, 
        Int64? branchId = null, 
        DateTime? fromDate = null, 
        DateTime? toDate = null);

    /// <summary>
    /// Generates ticket volume reports by time period, company, and type.
    /// Calls SP_SYS_TICKET_REPORTS_VOLUME stored procedure.
    /// </summary>
    /// <param name="startDate">Report start date</param>
    /// <param name="endDate">Report end date</param>
    /// <param name="companyId">Filter by company (0 = all)</param>
    /// <param name="ticketTypeId">Filter by ticket type (0 = all)</param>
    /// <param name="groupBy">Grouping option (DAILY, WEEKLY, MONTHLY, COMPANY, TYPE)</param>
    /// <returns>List of volume report data</returns>
    Task<List<Dictionary<string, object>>> GetTicketVolumeReportAsync(
        DateTime startDate,
        DateTime endDate,
        Int64 companyId = 0,
        Int64 ticketTypeId = 0,
        string groupBy = "DAILY");

    /// <summary>
    /// Calculates SLA compliance percentages by priority and type.
    /// Calls SP_SYS_TICKET_REPORTS_SLA_COMPLIANCE stored procedure.
    /// </summary>
    /// <param name="startDate">Report start date</param>
    /// <param name="endDate">Report end date</param>
    /// <param name="companyId">Filter by company (0 = all)</param>
    /// <returns>List of SLA compliance data</returns>
    Task<List<Dictionary<string, object>>> GetSlaComplianceReportAsync(
        DateTime startDate,
        DateTime endDate,
        Int64 companyId = 0);

    /// <summary>
    /// Generates workload reports showing active and resolved tickets per assignee.
    /// Calls SP_SYS_TICKET_REPORTS_WORKLOAD stored procedure.
    /// </summary>
    /// <param name="startDate">Report start date (optional)</param>
    /// <param name="endDate">Report end date (optional)</param>
    /// <param name="companyId">Filter by company (0 = all)</param>
    /// <returns>List of workload data per assignee</returns>
    Task<List<Dictionary<string, object>>> GetWorkloadReportAsync(
        DateTime? startDate = null,
        DateTime? endDate = null,
        Int64 companyId = 0);

    /// <summary>
    /// Provides trend analysis showing ticket creation and resolution patterns over time.
    /// Calls SP_SYS_TICKET_REPORTS_TRENDS stored procedure.
    /// </summary>
    /// <param name="startDate">Analysis start date</param>
    /// <param name="endDate">Analysis end date</param>
    /// <param name="periodType">Period grouping (DAILY, WEEKLY, MONTHLY)</param>
    /// <returns>List of trend data</returns>
    Task<List<Dictionary<string, object>>> GetTicketTrendsReportAsync(
        DateTime startDate,
        DateTime endDate,
        string periodType = "DAILY");

    /// <summary>
    /// Performs advanced search with multi-criteria filtering, AND/OR logic, and relevance scoring.
    /// Calls SP_SYS_REQUEST_TICKET_ADVANCED_SEARCH stored procedure.
    /// </summary>
    /// <param name="searchTerm">Full-text search term</param>
    /// <param name="companyId">Filter by company (0 = all)</param>
    /// <param name="branchId">Filter by branch (0 = all)</param>
    /// <param name="assigneeId">Filter by assignee (0 = all)</param>
    /// <param name="requesterId">Filter by requester (0 = all)</param>
    /// <param name="statusIds">Comma-separated status IDs</param>
    /// <param name="priorityIds">Comma-separated priority IDs</param>
    /// <param name="typeIds">Comma-separated type IDs</param>
    /// <param name="categoryIds">Comma-separated category IDs</param>
    /// <param name="createdFrom">Creation date range start</param>
    /// <param name="createdTo">Creation date range end</param>
    /// <param name="dueFrom">Expected resolution date range start</param>
    /// <param name="dueTo">Expected resolution date range end</param>
    /// <param name="slaStatus">Filter by SLA status (OnTime, AtRisk, Overdue)</param>
    /// <param name="filterLogic">AND or OR for combining criteria</param>
    /// <param name="includeInactive">Include inactive tickets</param>
    /// <param name="page">Page number for pagination</param>
    /// <param name="pageSize">Records per page</param>
    /// <param name="sortBy">Sort field (RELEVANCE, CREATION_DATE, PRIORITY, etc.)</param>
    /// <param name="sortDirection">Sort direction (ASC/DESC)</param>
    /// <returns>A tuple containing search results with relevance scores and total count</returns>
    Task<(List<SysRequestTicket> Tickets, int TotalCount)> AdvancedSearchAsync(
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
        string sortDirection = "DESC");
}