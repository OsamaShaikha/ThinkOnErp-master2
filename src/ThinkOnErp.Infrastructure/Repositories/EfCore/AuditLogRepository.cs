using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using ThinkOnErp.Domain.Entities;
using ThinkOnErp.Domain.Interfaces;
using ThinkOnErp.Infrastructure.Data;
using ThinkOnErp.Infrastructure.Exceptions;

namespace ThinkOnErp.Infrastructure.Repositories.EfCore;

/// <summary>
/// EF Core repository implementation for SysAuditLog entity using LINQ queries.
/// Implements IAuditRepository interface from the Domain layer.
/// Uses ThinkOnErpDbContext for database operations with bulk insert support for high-performance logging.
/// </summary>
public class AuditLogRepository : IAuditRepository
{
    private readonly ThinkOnErpDbContext _context;
    private readonly ILogger<AuditLogRepository> _logger;

    /// <summary>
    /// Initializes a new instance of the AuditLogRepository class.
    /// </summary>
    /// <param name="context">The EF Core database context for entity management.</param>
    /// <param name="logger">Logger for recording operations and errors.</param>
    /// <exception cref="ArgumentNullException">Thrown when context or logger is null.</exception>
    public AuditLogRepository(
        ThinkOnErpDbContext context,
        ILogger<AuditLogRepository> logger)
    {
        _context = context ?? throw new ArgumentNullException(nameof(context));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <summary>
    /// Inserts a single audit log entry into the database.
    /// Uses EF Core Add() and SaveChangesAsync() for single record insertion.
    /// </summary>
    /// <param name="auditLog">The audit log entry to insert</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>The generated audit log ID</returns>
    public async Task<long> InsertAsync(SysAuditLog auditLog, CancellationToken cancellationToken = default)
    {
        return await RepositoryExceptionHandler.ExecuteWithExceptionHandlingAsync(
            async () =>
            {
                _logger.LogDebug("Inserting audit log entry for {EntityType} {EntityId}", 
                    auditLog.EntityType, auditLog.EntityId);

                // Set creation date if not already set
                if (auditLog.CreationDate == default)
                {
                    auditLog.CreationDate = DateTime.UtcNow;
                }

                // Add the entity to the context
                _context.AuditLogs.Add(auditLog);

                // Save changes - EF Core will automatically retrieve the generated ID from the sequence
                await _context.SaveChangesAsync(cancellationToken);

                _logger.LogDebug("Inserted audit log entry with ID: {RowId}", auditLog.RowId);

                return auditLog.RowId;
            },
            "InsertAuditLog",
            _logger);
    }

    /// <summary>
    /// Inserts multiple audit log entries in a single batch operation.
    /// Uses EF Core AddRangeAsync() and SaveChangesAsync() for optimized bulk insertion.
    /// This method is optimized for high-volume logging scenarios.
    /// </summary>
    /// <param name="auditLogs">Collection of audit log entries to insert</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>The number of rows inserted</returns>
    public async Task<int> InsertBatchAsync(IEnumerable<SysAuditLog> auditLogs, CancellationToken cancellationToken = default)
    {
        return await RepositoryExceptionHandler.ExecuteWithExceptionHandlingAsync(
            async () =>
            {
                var auditLogList = auditLogs.ToList();
                _logger.LogDebug("Inserting batch of {Count} audit log entries", auditLogList.Count);

                if (!auditLogList.Any())
                {
                    _logger.LogDebug("No audit logs to insert");
                    return 0;
                }

                // Set creation date for all entries if not already set
                foreach (var auditLog in auditLogList)
                {
                    if (auditLog.CreationDate == default)
                    {
                        auditLog.CreationDate = DateTime.UtcNow;
                    }
                }

                // Add all entities to the context in a single operation
                await _context.AuditLogs.AddRangeAsync(auditLogList, cancellationToken);

                // Save all changes in a single database round-trip
                var rowsAffected = await _context.SaveChangesAsync(cancellationToken);

                _logger.LogInformation("Inserted batch of {Count} audit log entries", rowsAffected);

                return rowsAffected;
            },
            "InsertBatchAuditLog",
            _logger);
    }

    /// <summary>
    /// Retrieves audit logs by correlation ID for request tracing.
    /// Uses LINQ Where() clause with AsNoTracking() for read-only query optimization.
    /// </summary>
    /// <param name="correlationId">The correlation ID to search for</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Collection of audit log entries with the specified correlation ID</returns>
    public async Task<IEnumerable<SysAuditLog>> GetByCorrelationIdAsync(string correlationId, CancellationToken cancellationToken = default)
    {
        return await RepositoryExceptionHandler.ExecuteWithExceptionHandlingAsync(
            async () =>
            {
                _logger.LogDebug("Retrieving audit logs for correlation ID: {CorrelationId}", correlationId);

                var auditLogs = await _context.AuditLogs
                    .AsNoTracking()
                    .Where(a => a.CorrelationId == correlationId)
                    .OrderBy(a => a.CreationDate)
                    .ToListAsync(cancellationToken);

                _logger.LogDebug("Retrieved {Count} audit logs for correlation ID: {CorrelationId}", 
                    auditLogs.Count, correlationId);

                return auditLogs;
            },
            "GetAuditLogsByCorrelationId",
            _logger);
    }

    /// <summary>
    /// Retrieves audit logs for a specific entity.
    /// Uses LINQ Where() clause with multiple conditions and AsNoTracking() for read-only query optimization.
    /// </summary>
    /// <param name="entityType">The type of entity</param>
    /// <param name="entityId">The entity ID</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Collection of audit log entries for the specified entity</returns>
    public async Task<IEnumerable<SysAuditLog>> GetByEntityAsync(string entityType, long entityId, CancellationToken cancellationToken = default)
    {
        return await RepositoryExceptionHandler.ExecuteWithExceptionHandlingAsync(
            async () =>
            {
                _logger.LogDebug("Retrieving audit logs for entity: {EntityType} {EntityId}", entityType, entityId);

                var auditLogs = await _context.AuditLogs
                    .AsNoTracking()
                    .Where(a => a.EntityType == entityType && a.EntityId == entityId)
                    .OrderBy(a => a.CreationDate)
                    .ToListAsync(cancellationToken);

                _logger.LogDebug("Retrieved {Count} audit logs for entity: {EntityType} {EntityId}", 
                    auditLogs.Count, entityType, entityId);

                return auditLogs;
            },
            "GetAuditLogsByEntity",
            _logger);
    }

    /// <summary>
    /// Checks if the audit repository is healthy and can accept writes.
    /// Performs a simple query to verify database connectivity.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>True if healthy, false otherwise</returns>
    public async Task<bool> IsHealthyAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            _logger.LogDebug("Checking audit repository health");

            // Simple query to verify database connectivity
            await _context.AuditLogs
                .AsNoTracking()
                .Take(1)
                .ToListAsync(cancellationToken);

            _logger.LogDebug("Audit repository is healthy");
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Audit repository health check failed");
            return false;
        }
    }

    /// <summary>
    /// Retrieves a single audit log entry by its ID.
    /// Uses LINQ FirstOrDefaultAsync() with AsNoTracking() for read-only query optimization.
    /// </summary>
    /// <param name="auditLogId">The audit log entry ID</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>The audit log entry, or null if not found</returns>
    public async Task<SysAuditLog?> GetByIdAsync(long auditLogId, CancellationToken cancellationToken = default)
    {
        return await RepositoryExceptionHandler.ExecuteWithExceptionHandlingAsync(
            async () =>
            {
                _logger.LogDebug("Retrieving audit log with ID: {AuditLogId}", auditLogId);

                var auditLog = await _context.AuditLogs
                    .AsNoTracking()
                    .FirstOrDefaultAsync(a => a.RowId == auditLogId, cancellationToken);

                if (auditLog == null)
                {
                    _logger.LogDebug("Audit log with ID {AuditLogId} not found", auditLogId);
                }
                else
                {
                    _logger.LogDebug("Retrieved audit log: {EntityType} {EntityId}", 
                        auditLog.EntityType, auditLog.EntityId);
                }

                return auditLog;
            },
            "GetAuditLogById",
            _logger);
    }

    /// <summary>
    /// Retrieves all audit log IDs within a specified date range.
    /// Uses LINQ Where() clause with date range filtering and AsNoTracking() for read-only query optimization.
    /// Used for batch integrity verification and tampering detection.
    /// </summary>
    /// <param name="startDate">Start date of the range</param>
    /// <param name="endDate">End date of the range</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Collection of audit log IDs in the date range</returns>
    public async Task<IEnumerable<long>> GetAuditLogIdsByDateRangeAsync(DateTime startDate, DateTime endDate, CancellationToken cancellationToken = default)
    {
        return await RepositoryExceptionHandler.ExecuteWithExceptionHandlingAsync(
            async () =>
            {
                _logger.LogDebug("Retrieving audit log IDs for date range: {StartDate} to {EndDate}", 
                    startDate, endDate);

                var auditLogIds = await _context.AuditLogs
                    .AsNoTracking()
                    .Where(a => a.CreationDate >= startDate && a.CreationDate <= endDate)
                    .OrderBy(a => a.CreationDate)
                    .Select(a => a.RowId)
                    .ToListAsync(cancellationToken);

                _logger.LogDebug("Retrieved {Count} audit log IDs for date range", auditLogIds.Count);

                return auditLogIds;
            },
            "GetAuditLogIdsByDateRange",
            _logger);
    }
}
