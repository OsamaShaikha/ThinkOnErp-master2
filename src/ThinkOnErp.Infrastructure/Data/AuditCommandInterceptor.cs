using Oracle.ManagedDataAccess.Client;
using System.Data;
using System.Text.RegularExpressions;
using ThinkOnErp.Domain.Entities.Audit;
using ThinkOnErp.Domain.Interfaces;
using ThinkOnErp.Infrastructure.Services;
using Microsoft.Extensions.Logging;

namespace ThinkOnErp.Infrastructure.Data;

/// <summary>
/// ADO.NET command interceptor for automatic data change audit logging.
/// Intercepts INSERT, UPDATE, DELETE operations and logs them to the audit trail.
/// Integrates with Oracle database context and IAuditLogger service.
/// </summary>
public class AuditCommandInterceptor
{
    private readonly IAuditLogger _auditLogger;
    private readonly IAuditContextProvider _contextProvider;
    private readonly ILogger<AuditCommandInterceptor> _logger;

    // Regex patterns for SQL command detection
    private static readonly Regex InsertPattern = new(@"^\s*INSERT\s+INTO\s+(\w+)", RegexOptions.IgnoreCase | RegexOptions.Compiled);
    private static readonly Regex UpdatePattern = new(@"^\s*UPDATE\s+(\w+)", RegexOptions.IgnoreCase | RegexOptions.Compiled);
    private static readonly Regex DeletePattern = new(@"^\s*DELETE\s+FROM\s+(\w+)", RegexOptions.IgnoreCase | RegexOptions.Compiled);

    public AuditCommandInterceptor(
        IAuditLogger auditLogger,
        IAuditContextProvider contextProvider,
        ILogger<AuditCommandInterceptor> logger)
    {
        _auditLogger = auditLogger;
        _contextProvider = contextProvider;
        _logger = logger;
    }

    /// <summary>
    /// Intercepts command execution and logs auditable operations.
    /// Called after a command is executed successfully.
    /// </summary>
    /// <param name="command">The Oracle command that was executed</param>
    /// <param name="rowsAffected">Number of rows affected by the command</param>
    /// <param name="cancellationToken">Cancellation token</param>
    public async Task OnCommandExecutedAsync(
        OracleCommand command,
        int rowsAffected,
        CancellationToken cancellationToken = default)
    {
        // Only audit INSERT, UPDATE, DELETE commands
        if (!IsAuditableCommand(command.CommandText))
        {
            return;
        }

        try
        {
            await LogDatabaseChangeAsync(command, rowsAffected, cancellationToken);
        }
        catch (Exception ex)
        {
            // Don't let audit logging failure break the database operation
            // Log the error but continue
            _logger.LogError(ex, "Failed to log audit event for command: {CommandText}", 
                command.CommandText.Substring(0, Math.Min(100, command.CommandText.Length)));
        }
    }

    /// <summary>
    /// Determines if a SQL command should be audited.
    /// Returns true for INSERT, UPDATE, DELETE operations.
    /// </summary>
    private bool IsAuditableCommand(string commandText)
    {
        if (string.IsNullOrWhiteSpace(commandText))
        {
            return false;
        }

        var trimmedCommand = commandText.Trim();
        
        return InsertPattern.IsMatch(trimmedCommand) ||
               UpdatePattern.IsMatch(trimmedCommand) ||
               DeletePattern.IsMatch(trimmedCommand);
    }

    /// <summary>
    /// Logs a database change to the audit trail.
    /// Extracts table name, action type, and context information.
    /// </summary>
    private async Task LogDatabaseChangeAsync(
        OracleCommand command,
        int rowsAffected,
        CancellationToken cancellationToken)
    {
        var action = DetermineActionFromSql(command.CommandText);
        var tableName = ExtractTableName(command.CommandText);

        // Skip audit log table itself to prevent infinite recursion
        if (tableName?.Equals("SYS_AUDIT_LOG", StringComparison.OrdinalIgnoreCase) == true ||
            tableName?.Equals("SYS_AUDIT_LOG_ARCHIVE", StringComparison.OrdinalIgnoreCase) == true ||
            tableName?.Equals("SYS_AUDIT_STATUS_TRACKING", StringComparison.OrdinalIgnoreCase) == true)
        {
            return;
        }

        var auditEvent = new DataChangeAuditEvent
        {
            CorrelationId = _contextProvider.GetCorrelationId(),
            ActorType = _contextProvider.GetActorType(),
            ActorId = _contextProvider.GetActorId(),
            CompanyId = _contextProvider.GetCompanyId(),
            BranchId = _contextProvider.GetBranchId(),
            Action = action,
            EntityType = tableName ?? "UNKNOWN",
            EntityId = null, // Entity ID is better captured at repository level
            OldValue = null, // Old/new values are better captured at repository level
            NewValue = null,
            IpAddress = _contextProvider.GetIpAddress(),
            UserAgent = _contextProvider.GetUserAgent(),
            Timestamp = DateTime.UtcNow
        };

        // Log asynchronously without blocking the database operation
        await _auditLogger.LogDataChangeAsync(auditEvent, cancellationToken);
    }

    /// <summary>
    /// Determines the action type (INSERT, UPDATE, DELETE) from SQL command text.
    /// </summary>
    private string DetermineActionFromSql(string commandText)
    {
        var trimmedCommand = commandText.Trim();

        if (InsertPattern.IsMatch(trimmedCommand))
        {
            return "INSERT";
        }
        else if (UpdatePattern.IsMatch(trimmedCommand))
        {
            return "UPDATE";
        }
        else if (DeletePattern.IsMatch(trimmedCommand))
        {
            return "DELETE";
        }

        return "UNKNOWN";
    }

    /// <summary>
    /// Extracts the table name from SQL command text.
    /// Handles INSERT INTO, UPDATE, and DELETE FROM patterns.
    /// </summary>
    private string? ExtractTableName(string commandText)
    {
        var trimmedCommand = commandText.Trim();

        // Try INSERT pattern
        var insertMatch = InsertPattern.Match(trimmedCommand);
        if (insertMatch.Success && insertMatch.Groups.Count > 1)
        {
            return insertMatch.Groups[1].Value;
        }

        // Try UPDATE pattern
        var updateMatch = UpdatePattern.Match(trimmedCommand);
        if (updateMatch.Success && updateMatch.Groups.Count > 1)
        {
            return updateMatch.Groups[1].Value;
        }

        // Try DELETE pattern
        var deleteMatch = DeletePattern.Match(trimmedCommand);
        if (deleteMatch.Success && deleteMatch.Groups.Count > 1)
        {
            return deleteMatch.Groups[1].Value;
        }

        return null;
    }
}
