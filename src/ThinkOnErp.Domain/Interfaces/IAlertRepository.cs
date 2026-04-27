using ThinkOnErp.Domain.Models;

namespace ThinkOnErp.Domain.Interfaces;

/// <summary>
/// Repository interface for alert persistence and retrieval.
/// Manages alert history storage in the SYS_SECURITY_THREATS table.
/// </summary>
public interface IAlertRepository
{
    /// <summary>
    /// Save an alert to the database.
    /// Inserts a new record into SYS_SECURITY_THREATS table.
    /// </summary>
    /// <param name="alert">The alert to save</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>The saved alert with assigned ID</returns>
    Task<Alert> SaveAlertAsync(Alert alert, CancellationToken cancellationToken = default);

    /// <summary>
    /// Get alert history with pagination.
    /// Retrieves alerts from SYS_SECURITY_THREATS table with optional filtering.
    /// </summary>
    /// <param name="pagination">Pagination options</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Paged result of alert history</returns>
    Task<PagedResult<AlertHistory>> GetAlertHistoryAsync(
        PaginationOptions pagination,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Get an alert by ID.
    /// </summary>
    /// <param name="alertId">The alert ID</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>The alert if found, null otherwise</returns>
    Task<Alert?> GetAlertByIdAsync(long alertId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Acknowledge an alert.
    /// Updates the ACKNOWLEDGED_BY and ACKNOWLEDGED_DATE fields and sets STATUS to 'Acknowledged'.
    /// </summary>
    /// <param name="alertId">The alert ID to acknowledge</param>
    /// <param name="userId">The user ID acknowledging the alert</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>True if successful, false if alert not found</returns>
    Task<bool> AcknowledgeAlertAsync(
        long alertId,
        long userId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Resolve an alert.
    /// Updates the RESOLVED_DATE field, sets STATUS to 'Resolved', and optionally stores resolution notes.
    /// </summary>
    /// <param name="alertId">The alert ID to resolve</param>
    /// <param name="userId">The user ID resolving the alert</param>
    /// <param name="resolutionNotes">Optional notes explaining the resolution</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>True if successful, false if alert not found</returns>
    Task<bool> ResolveAlertAsync(
        long alertId,
        long userId,
        string? resolutionNotes = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Get active (unresolved) alerts count.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Count of active alerts</returns>
    Task<int> GetActiveAlertsCountAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Get alerts by status.
    /// </summary>
    /// <param name="status">The status to filter by (Active, Acknowledged, Resolved, FalsePositive)</param>
    /// <param name="pagination">Pagination options</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Paged result of alerts with the specified status</returns>
    Task<PagedResult<AlertHistory>> GetAlertsByStatusAsync(
        string status,
        PaginationOptions pagination,
        CancellationToken cancellationToken = default);
}
