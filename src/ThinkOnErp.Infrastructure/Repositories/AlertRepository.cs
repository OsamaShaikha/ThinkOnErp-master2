using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using ThinkOnErp.Domain.Entities;
using ThinkOnErp.Domain.Interfaces;
using ThinkOnErp.Domain.Models;
using ThinkOnErp.Infrastructure.Data;

namespace ThinkOnErp.Infrastructure.Repositories;

/// <summary>
/// Repository implementation for alert persistence and retrieval.
/// Uses the SYS_SECURITY_THREATS table for storing alert history.
/// </summary>
public class AlertRepository : IAlertRepository
{
    private readonly OracleDbContext _dbContext;
    private readonly ILogger<AlertRepository> _logger;

    public AlertRepository(OracleDbContext dbContext, ILogger<AlertRepository> logger)
    {
        _dbContext = dbContext ?? throw new ArgumentNullException(nameof(dbContext));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <summary>
    /// Save an alert to the database.
    /// </summary>
    public async Task<Alert> SaveAlertAsync(Alert alert, CancellationToken cancellationToken = default)
    {
        if (alert == null)
        {
            throw new ArgumentNullException(nameof(alert));
        }

        try
        {
            var entity = new SysSecurityThreat
            {
                ThreatType = alert.AlertType,
                Severity = alert.Severity,
                IpAddress = alert.IpAddress,
                UserId = alert.UserId,
                CompanyId = alert.CompanyId,
                Description = $"{alert.Title}\n{alert.Description}",
                DetectionDate = alert.TriggeredAt,
                Status = "Active",
                Metadata = alert.Metadata
            };

            _dbContext.SysSecurityThreats.Add(entity);
            await _dbContext.SaveChangesAsync(cancellationToken);

            alert.Id = entity.Id;

            _logger.LogInformation(
                "Saved alert to database: Id={AlertId}, Type={AlertType}, Severity={Severity}",
                alert.Id, alert.AlertType, alert.Severity);

            return alert;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex,
                "Error saving alert to database: Type={AlertType}, Severity={Severity}",
                alert.AlertType, alert.Severity);
            throw;
        }
    }

    /// <summary>
    /// Get alert history with pagination.
    /// </summary>
    public async Task<PagedResult<AlertHistory>> GetAlertHistoryAsync(
        PaginationOptions pagination,
        CancellationToken cancellationToken = default)
    {
        if (pagination == null)
        {
            throw new ArgumentNullException(nameof(pagination));
        }

        try
        {
            var totalCount = await _dbContext.SysSecurityThreats.CountAsync(cancellationToken);

            var query = from t in _dbContext.SysSecurityThreats
                        join u in _dbContext.SysUsers on t.AcknowledgedBy equals (long?)u.Id into ackJoin
                        from u_ack in ackJoin.DefaultIfEmpty()
                        orderby t.DetectionDate descending
                        select new AlertHistory
                        {
                            Id = t.Id,
                            AlertType = t.ThreatType,
                            Severity = t.Severity,
                            Title = ExtractTitle(t.Description),
                            Description = t.Description,
                            TriggeredAt = t.DetectionDate,
                            AcknowledgedAt = t.AcknowledgedDate,
                            AcknowledgedByUsername = u_ack != null ? u_ack.UserName : null,
                            ResolvedAt = t.ResolvedDate,
                            Metadata = t.Metadata,
                            NotificationSuccess = true
                        };

            var alerts = await query
                .Skip(pagination.Skip)
                .Take(pagination.PageSize)
                .ToListAsync(cancellationToken);

            _logger.LogDebug(
                "Retrieved alert history: Page={PageNumber}, PageSize={PageSize}, TotalCount={TotalCount}",
                pagination.PageNumber, pagination.PageSize, totalCount);

            return new PagedResult<AlertHistory>
            {
                Items = alerts,
                TotalCount = totalCount,
                Page = pagination.PageNumber,
                PageSize = pagination.PageSize
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving alert history");
            throw;
        }
    }

    /// <summary>
    /// Get an alert by ID.
    /// </summary>
    public async Task<Alert?> GetAlertByIdAsync(long alertId, CancellationToken cancellationToken = default)
    {
        try
        {
            var entity = await _dbContext.SysSecurityThreats
                .FirstOrDefaultAsync(t => t.Id == alertId, cancellationToken);

            if (entity == null)
            {
                return null;
            }

            return new Alert
            {
                Id = entity.Id,
                AlertType = entity.ThreatType,
                Severity = entity.Severity,
                Title = ExtractTitle(entity.Description),
                Description = entity.Description,
                IpAddress = entity.IpAddress,
                UserId = entity.UserId,
                CompanyId = entity.CompanyId,
                Metadata = entity.Metadata,
                TriggeredAt = entity.DetectionDate,
                AcknowledgedBy = entity.AcknowledgedBy,
                AcknowledgedAt = entity.AcknowledgedDate,
                ResolvedAt = entity.ResolvedDate
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving alert by ID: AlertId={AlertId}", alertId);
            throw;
        }
    }

    /// <summary>
    /// Acknowledge an alert.
    /// </summary>
    public async Task<bool> AcknowledgeAlertAsync(
        long alertId,
        long userId,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var entity = await _dbContext.SysSecurityThreats
                .FirstOrDefaultAsync(t => t.Id == alertId && t.Status == "Active", cancellationToken);

            if (entity == null)
            {
                _logger.LogWarning(
                    "Failed to acknowledge alert (not found or already acknowledged): AlertId={AlertId}",
                    alertId);
                return false;
            }

            entity.Status = "Acknowledged";
            entity.AcknowledgedBy = userId;
            entity.AcknowledgedDate = DateTime.UtcNow;

            await _dbContext.SaveChangesAsync(cancellationToken);

            _logger.LogInformation(
                "Acknowledged alert: AlertId={AlertId}, UserId={UserId}",
                alertId, userId);
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex,
                "Error acknowledging alert: AlertId={AlertId}, UserId={UserId}",
                alertId, userId);
            throw;
        }
    }

    /// <summary>
    /// Resolve an alert.
    /// </summary>
    public async Task<bool> ResolveAlertAsync(
        long alertId,
        long userId,
        string? resolutionNotes = null,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var entity = await _dbContext.SysSecurityThreats
                .FirstOrDefaultAsync(t => t.Id == alertId && (t.Status == "Active" || t.Status == "Acknowledged"), cancellationToken);

            if (entity == null)
            {
                _logger.LogWarning("Alert not found: AlertId={AlertId}", alertId);
                return false;
            }

            // Update metadata with resolution information
            var metadata = entity.Metadata ?? "{}";
            if (!string.IsNullOrWhiteSpace(resolutionNotes))
            {
                // Simple JSON append - in production, use proper JSON library
                metadata = metadata.TrimEnd('}') + $", \"resolutionNotes\": \"{resolutionNotes}\", \"resolvedBy\": {userId}}}";
            }

            entity.Status = "Resolved";
            entity.ResolvedDate = DateTime.UtcNow;
            entity.Metadata = metadata;

            await _dbContext.SaveChangesAsync(cancellationToken);

            _logger.LogInformation(
                "Resolved alert: AlertId={AlertId}, UserId={UserId}, Notes={Notes}",
                alertId, userId, resolutionNotes);

            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex,
                "Error resolving alert: AlertId={AlertId}, UserId={UserId}",
                alertId, userId);
            throw;
        }
    }

    /// <summary>
    /// Get active (unresolved) alerts count.
    /// </summary>
    public async Task<int> GetActiveAlertsCountAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            var count = await _dbContext.SysSecurityThreats
                .CountAsync(t => t.Status == "Active" || t.Status == "Acknowledged", cancellationToken);

            _logger.LogDebug("Active alerts count: {Count}", count);

            return count;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting active alerts count");
            throw;
        }
    }

    /// <summary>
    /// Get alerts by status.
    /// </summary>
    public async Task<PagedResult<AlertHistory>> GetAlertsByStatusAsync(
        string status,
        PaginationOptions pagination,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(status))
        {
            throw new ArgumentException("Status is required", nameof(status));
        }

        if (pagination == null)
        {
            throw new ArgumentNullException(nameof(pagination));
        }

        try
        {
            var totalCount = await _dbContext.SysSecurityThreats
                .CountAsync(t => t.Status == status, cancellationToken);

            var query = from t in _dbContext.SysSecurityThreats
                        where t.Status == status
                        join u in _dbContext.SysUsers on t.AcknowledgedBy equals (long?)u.Id into ackJoin
                        from u_ack in ackJoin.DefaultIfEmpty()
                        orderby t.DetectionDate descending
                        select new AlertHistory
                        {
                            Id = t.Id,
                            AlertType = t.ThreatType,
                            Severity = t.Severity,
                            Title = ExtractTitle(t.Description),
                            Description = t.Description,
                            TriggeredAt = t.DetectionDate,
                            AcknowledgedAt = t.AcknowledgedDate,
                            AcknowledgedByUsername = u_ack != null ? u_ack.UserName : null,
                            ResolvedAt = t.ResolvedDate,
                            Metadata = t.Metadata,
                            NotificationSuccess = true
                        };

            var alerts = await query
                .Skip(pagination.Skip)
                .Take(pagination.PageSize)
                .ToListAsync(cancellationToken);

            _logger.LogDebug(
                "Retrieved alerts by status: Status={Status}, Page={PageNumber}, PageSize={PageSize}, TotalCount={TotalCount}",
                status, pagination.PageNumber, pagination.PageSize, totalCount);

            return new PagedResult<AlertHistory>
            {
                Items = alerts,
                TotalCount = totalCount,
                Page = pagination.PageNumber,
                PageSize = pagination.PageSize
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving alerts by status: Status={Status}", status);
            throw;
        }
    }

    /// <summary>
    /// Extract title from description (first line).
    /// </summary>
    private string ExtractTitle(string description)
    {
        if (string.IsNullOrWhiteSpace(description))
        {
            return "Alert";
        }

        var lines = description.Split('\n', StringSplitOptions.RemoveEmptyEntries);
        return lines.Length > 0 ? lines[0] : description;
    }
}
