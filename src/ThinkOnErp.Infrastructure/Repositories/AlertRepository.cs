using System.Data;
using Microsoft.Extensions.Logging;
using Oracle.ManagedDataAccess.Client;
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
            using var connection = _dbContext.CreateConnection();
            await connection.OpenAsync(cancellationToken);

            using var command = connection.CreateCommand();
            command.CommandType = CommandType.Text;
            command.CommandText = @"
                INSERT INTO SYS_SECURITY_THREATS (
                    ROW_ID,
                    THREAT_TYPE,
                    SEVERITY,
                    IP_ADDRESS,
                    USER_ID,
                    COMPANY_ID,
                    DESCRIPTION,
                    DETECTION_DATE,
                    STATUS,
                    METADATA
                ) VALUES (
                    SEQ_SYS_SECURITY_THREATS.NEXTVAL,
                    :ThreatType,
                    :Severity,
                    :IpAddress,
                    :UserId,
                    :CompanyId,
                    :Description,
                    :DetectionDate,
                    :Status,
                    :Metadata
                ) RETURNING ROW_ID INTO :Id";

            command.Parameters.Add(new OracleParameter("ThreatType", OracleDbType.NVarchar2) { Value = alert.AlertType });
            command.Parameters.Add(new OracleParameter("Severity", OracleDbType.NVarchar2) { Value = alert.Severity });
            command.Parameters.Add(new OracleParameter("IpAddress", OracleDbType.NVarchar2) { Value = (object?)alert.IpAddress ?? DBNull.Value });
            command.Parameters.Add(new OracleParameter("UserId", OracleDbType.Int64) { Value = (object?)alert.UserId ?? DBNull.Value });
            command.Parameters.Add(new OracleParameter("CompanyId", OracleDbType.Int64) { Value = (object?)alert.CompanyId ?? DBNull.Value });
            command.Parameters.Add(new OracleParameter("Description", OracleDbType.NVarchar2) { Value = $"{alert.Title}\n{alert.Description}" });
            command.Parameters.Add(new OracleParameter("DetectionDate", OracleDbType.Date) { Value = alert.TriggeredAt });
            command.Parameters.Add(new OracleParameter("Status", OracleDbType.NVarchar2) { Value = "Active" });
            command.Parameters.Add(new OracleParameter("Metadata", OracleDbType.Clob) { Value = (object?)alert.Metadata ?? DBNull.Value });
            
            var idParam = new OracleParameter("Id", OracleDbType.Int64) { Direction = ParameterDirection.Output };
            command.Parameters.Add(idParam);

            await command.ExecuteNonQueryAsync(cancellationToken);

            alert.Id = ((Oracle.ManagedDataAccess.Types.OracleDecimal)idParam.Value).ToInt64();

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
            using var connection = _dbContext.CreateConnection();
            await connection.OpenAsync(cancellationToken);

            // Get total count
            using var countCommand = connection.CreateCommand();
            countCommand.CommandType = CommandType.Text;
            countCommand.CommandText = "SELECT COUNT(*) FROM SYS_SECURITY_THREATS";
            var totalCount = Convert.ToInt32(await countCommand.ExecuteScalarAsync(cancellationToken));

            // Get paginated data
            using var dataCommand = connection.CreateCommand();
            dataCommand.CommandType = CommandType.Text;
            dataCommand.CommandText = @"
                SELECT * FROM (
                    SELECT 
                        t.ROW_ID,
                        t.THREAT_TYPE,
                        t.SEVERITY,
                        t.DESCRIPTION,
                        t.DETECTION_DATE,
                        t.ACKNOWLEDGED_DATE,
                        t.RESOLVED_DATE,
                        t.METADATA,
                        u_ack.USERNAME AS ACKNOWLEDGED_BY_USERNAME,
                        ROW_NUMBER() OVER (ORDER BY t.DETECTION_DATE DESC) AS RN
                    FROM SYS_SECURITY_THREATS t
                    LEFT JOIN SYS_USERS u_ack ON t.ACKNOWLEDGED_BY = u_ack.ROW_ID
                )
                WHERE RN > :Skip AND RN <= :End";

            dataCommand.Parameters.Add(new OracleParameter("Skip", OracleDbType.Int32) { Value = pagination.Skip });
            dataCommand.Parameters.Add(new OracleParameter("End", OracleDbType.Int32) { Value = pagination.Skip + pagination.PageSize });

            var alerts = new List<AlertHistory>();
            using var reader = await dataCommand.ExecuteReaderAsync(cancellationToken);
            while (await reader.ReadAsync(cancellationToken))
            {
                alerts.Add(MapToAlertHistory(reader));
            }

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
            using var connection = _dbContext.CreateConnection();
            await connection.OpenAsync(cancellationToken);

            using var command = connection.CreateCommand();
            command.CommandType = CommandType.Text;
            command.CommandText = @"
                SELECT 
                    ROW_ID,
                    THREAT_TYPE,
                    SEVERITY,
                    IP_ADDRESS,
                    USER_ID,
                    COMPANY_ID,
                    DESCRIPTION,
                    DETECTION_DATE,
                    STATUS,
                    ACKNOWLEDGED_BY,
                    ACKNOWLEDGED_DATE,
                    RESOLVED_DATE,
                    METADATA
                FROM SYS_SECURITY_THREATS
                WHERE ROW_ID = :AlertId";

            command.Parameters.Add(new OracleParameter("AlertId", OracleDbType.Int64) { Value = alertId });

            using var reader = await command.ExecuteReaderAsync(cancellationToken);
            if (await reader.ReadAsync(cancellationToken))
            {
                return MapToAlert(reader);
            }

            return null;
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
            using var connection = _dbContext.CreateConnection();
            await connection.OpenAsync(cancellationToken);

            using var command = connection.CreateCommand();
            command.CommandType = CommandType.Text;
            command.CommandText = @"
                UPDATE SYS_SECURITY_THREATS
                SET 
                    STATUS = 'Acknowledged',
                    ACKNOWLEDGED_BY = :UserId,
                    ACKNOWLEDGED_DATE = SYSDATE
                WHERE ROW_ID = :AlertId
                AND STATUS = 'Active'";

            command.Parameters.Add(new OracleParameter("AlertId", OracleDbType.Int64) { Value = alertId });
            command.Parameters.Add(new OracleParameter("UserId", OracleDbType.Int64) { Value = userId });

            var rowsAffected = await command.ExecuteNonQueryAsync(cancellationToken);

            if (rowsAffected > 0)
            {
                _logger.LogInformation(
                    "Acknowledged alert: AlertId={AlertId}, UserId={UserId}",
                    alertId, userId);
                return true;
            }

            _logger.LogWarning(
                "Failed to acknowledge alert (not found or already acknowledged): AlertId={AlertId}",
                alertId);
            return false;
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
            using var connection = _dbContext.CreateConnection();
            await connection.OpenAsync(cancellationToken);

            // Get current alert to update metadata with resolution notes
            var alert = await GetAlertByIdAsync(alertId, cancellationToken);
            if (alert == null)
            {
                _logger.LogWarning("Alert not found: AlertId={AlertId}", alertId);
                return false;
            }

            // Update metadata with resolution information
            var metadata = alert.Metadata ?? "{}";
            if (!string.IsNullOrWhiteSpace(resolutionNotes))
            {
                // Simple JSON append - in production, use proper JSON library
                metadata = metadata.TrimEnd('}') + $", \"resolutionNotes\": \"{resolutionNotes}\", \"resolvedBy\": {userId}}}";
            }

            using var command = connection.CreateCommand();
            command.CommandType = CommandType.Text;
            command.CommandText = @"
                UPDATE SYS_SECURITY_THREATS
                SET 
                    STATUS = 'Resolved',
                    RESOLVED_DATE = SYSDATE,
                    METADATA = :Metadata
                WHERE ROW_ID = :AlertId
                AND STATUS IN ('Active', 'Acknowledged')";

            command.Parameters.Add(new OracleParameter("AlertId", OracleDbType.Int64) { Value = alertId });
            command.Parameters.Add(new OracleParameter("Metadata", OracleDbType.Clob) { Value = metadata });

            var rowsAffected = await command.ExecuteNonQueryAsync(cancellationToken);

            if (rowsAffected > 0)
            {
                _logger.LogInformation(
                    "Resolved alert: AlertId={AlertId}, UserId={UserId}, Notes={Notes}",
                    alertId, userId, resolutionNotes);
                return true;
            }

            _logger.LogWarning(
                "Failed to resolve alert (not found or already resolved): AlertId={AlertId}",
                alertId);
            return false;
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
            using var connection = _dbContext.CreateConnection();
            await connection.OpenAsync(cancellationToken);

            using var command = connection.CreateCommand();
            command.CommandType = CommandType.Text;
            command.CommandText = @"
                SELECT COUNT(*)
                FROM SYS_SECURITY_THREATS
                WHERE STATUS IN ('Active', 'Acknowledged')";

            var count = Convert.ToInt32(await command.ExecuteScalarAsync(cancellationToken));

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
            using var connection = _dbContext.CreateConnection();
            await connection.OpenAsync(cancellationToken);

            // Get total count for status
            using var countCommand = connection.CreateCommand();
            countCommand.CommandType = CommandType.Text;
            countCommand.CommandText = @"
                SELECT COUNT(*)
                FROM SYS_SECURITY_THREATS
                WHERE STATUS = :Status";
            countCommand.Parameters.Add(new OracleParameter("Status", OracleDbType.NVarchar2) { Value = status });

            var totalCount = Convert.ToInt32(await countCommand.ExecuteScalarAsync(cancellationToken));

            // Get paginated data
            using var dataCommand = connection.CreateCommand();
            dataCommand.CommandType = CommandType.Text;
            dataCommand.CommandText = @"
                SELECT * FROM (
                    SELECT 
                        t.ROW_ID,
                        t.THREAT_TYPE,
                        t.SEVERITY,
                        t.DESCRIPTION,
                        t.DETECTION_DATE,
                        t.ACKNOWLEDGED_DATE,
                        t.RESOLVED_DATE,
                        t.METADATA,
                        u_ack.USERNAME AS ACKNOWLEDGED_BY_USERNAME,
                        ROW_NUMBER() OVER (ORDER BY t.DETECTION_DATE DESC) AS RN
                    FROM SYS_SECURITY_THREATS t
                    LEFT JOIN SYS_USERS u_ack ON t.ACKNOWLEDGED_BY = u_ack.ROW_ID
                    WHERE t.STATUS = :Status
                )
                WHERE RN > :Skip AND RN <= :End";

            dataCommand.Parameters.Add(new OracleParameter("Status", OracleDbType.NVarchar2) { Value = status });
            dataCommand.Parameters.Add(new OracleParameter("Skip", OracleDbType.Int32) { Value = pagination.Skip });
            dataCommand.Parameters.Add(new OracleParameter("End", OracleDbType.Int32) { Value = pagination.Skip + pagination.PageSize });

            var alerts = new List<AlertHistory>();
            using var reader = await dataCommand.ExecuteReaderAsync(cancellationToken);
            while (await reader.ReadAsync(cancellationToken))
            {
                alerts.Add(MapToAlertHistory(reader));
            }

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
    /// Map database reader to Alert model.
    /// </summary>
    private Alert MapToAlert(OracleDataReader reader)
    {
        return new Alert
        {
            Id = reader.GetInt64(reader.GetOrdinal("ROW_ID")),
            AlertType = reader.GetString(reader.GetOrdinal("THREAT_TYPE")),
            Severity = reader.GetString(reader.GetOrdinal("SEVERITY")),
            Title = ExtractTitle(reader.GetString(reader.GetOrdinal("DESCRIPTION"))),
            Description = reader.GetString(reader.GetOrdinal("DESCRIPTION")),
            IpAddress = reader.IsDBNull(reader.GetOrdinal("IP_ADDRESS")) ? null : reader.GetString(reader.GetOrdinal("IP_ADDRESS")),
            UserId = reader.IsDBNull(reader.GetOrdinal("USER_ID")) ? null : reader.GetInt64(reader.GetOrdinal("USER_ID")),
            CompanyId = reader.IsDBNull(reader.GetOrdinal("COMPANY_ID")) ? null : reader.GetInt64(reader.GetOrdinal("COMPANY_ID")),
            Metadata = reader.IsDBNull(reader.GetOrdinal("METADATA")) ? null : reader.GetString(reader.GetOrdinal("METADATA")),
            TriggeredAt = reader.GetDateTime(reader.GetOrdinal("DETECTION_DATE")),
            AcknowledgedBy = reader.IsDBNull(reader.GetOrdinal("ACKNOWLEDGED_BY")) ? null : reader.GetInt64(reader.GetOrdinal("ACKNOWLEDGED_BY")),
            AcknowledgedAt = reader.IsDBNull(reader.GetOrdinal("ACKNOWLEDGED_DATE")) ? null : reader.GetDateTime(reader.GetOrdinal("ACKNOWLEDGED_DATE")),
            ResolvedAt = reader.IsDBNull(reader.GetOrdinal("RESOLVED_DATE")) ? null : reader.GetDateTime(reader.GetOrdinal("RESOLVED_DATE"))
        };
    }

    /// <summary>
    /// Map database reader to AlertHistory model.
    /// </summary>
    private AlertHistory MapToAlertHistory(OracleDataReader reader)
    {
        var description = reader.GetString(reader.GetOrdinal("DESCRIPTION"));
        return new AlertHistory
        {
            Id = reader.GetInt64(reader.GetOrdinal("ROW_ID")),
            AlertType = reader.GetString(reader.GetOrdinal("THREAT_TYPE")),
            Severity = reader.GetString(reader.GetOrdinal("SEVERITY")),
            Title = ExtractTitle(description),
            Description = description,
            TriggeredAt = reader.GetDateTime(reader.GetOrdinal("DETECTION_DATE")),
            AcknowledgedAt = reader.IsDBNull(reader.GetOrdinal("ACKNOWLEDGED_DATE")) ? null : reader.GetDateTime(reader.GetOrdinal("ACKNOWLEDGED_DATE")),
            AcknowledgedByUsername = reader.IsDBNull(reader.GetOrdinal("ACKNOWLEDGED_BY_USERNAME")) ? null : reader.GetString(reader.GetOrdinal("ACKNOWLEDGED_BY_USERNAME")),
            ResolvedAt = reader.IsDBNull(reader.GetOrdinal("RESOLVED_DATE")) ? null : reader.GetDateTime(reader.GetOrdinal("RESOLVED_DATE")),
            Metadata = reader.IsDBNull(reader.GetOrdinal("METADATA")) ? null : reader.GetString(reader.GetOrdinal("METADATA")),
            NotificationSuccess = true // Assume success if saved to DB
        };
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
