using Oracle.ManagedDataAccess.Client;
using Oracle.ManagedDataAccess.Types;
using System.Data;
using System.Text;
using System.Text.Json;
using ThinkOnErp.Domain.Entities;
using ThinkOnErp.Domain.Interfaces;
using ThinkOnErp.Infrastructure.Data;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.DependencyInjection;

namespace ThinkOnErp.Infrastructure.Repositories;

/// <summary>
/// Repository implementation for audit log operations using Oracle database.
/// Provides high-performance batch insert capabilities for audit logging.
/// Includes cryptographic signature generation for tamper-evident audit trails.
/// </summary>
public class AuditRepository : IAuditRepository
{
    private readonly OracleDbContext _dbContext;
    private readonly ILogger<AuditRepository> _logger;
    private readonly IServiceProvider _serviceProvider;
    private IAuditLogIntegrityService? _integrityService;

    public AuditRepository(
        OracleDbContext dbContext, 
        ILogger<AuditRepository> logger,
        IServiceProvider serviceProvider)
    {
        _dbContext = dbContext ?? throw new ArgumentNullException(nameof(dbContext));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _serviceProvider = serviceProvider ?? throw new ArgumentNullException(nameof(serviceProvider));
    }

    /// <summary>
    /// Lazily resolves the integrity service to avoid circular dependency.
    /// </summary>
    private IAuditLogIntegrityService? GetIntegrityService()
    {
        if (_integrityService == null)
        {
            try
            {
                _integrityService = _serviceProvider.GetService<IAuditLogIntegrityService>();
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to resolve IAuditLogIntegrityService. Integrity signing will be disabled.");
            }
        }
        return _integrityService;
    }

    /// <summary>
    /// Inserts a single audit log entry into the database.
    /// Uses direct INSERT statement for single entries.
    /// Generates cryptographic signature for tamper-evident audit trail.
    /// </summary>
    public async Task<long> InsertAsync(SysAuditLog auditLog, CancellationToken cancellationToken = default)
    {
        using var connection = _dbContext.CreateConnection();
        await connection.OpenAsync(cancellationToken);

        using var command = connection.CreateCommand();
        command.CommandText = BuildInsertSql();
        AddInsertParameters(command, auditLog);

        // Add output parameter for the generated ID
        var idParam = new OracleParameter
        {
            ParameterName = "P_NEW_ID",
            OracleDbType = OracleDbType.Decimal,
            Direction = ParameterDirection.Output
        };
        command.Parameters.Add(idParam);

        await command.ExecuteNonQueryAsync(cancellationToken);

        var newId = Convert.ToInt64(((OracleDecimal)idParam.Value).Value);

        // Generate and store cryptographic signature if integrity service is available
        var integrityService = GetIntegrityService();
        if (integrityService != null)
        {
            try
            {
                var signature = integrityService.GenerateIntegrityHash(
                    newId,
                    auditLog.ActorId,
                    auditLog.Action,
                    auditLog.EntityType,
                    auditLog.EntityId,
                    auditLog.CreationDate,
                    auditLog.OldValue,
                    auditLog.NewValue);

                // Update the metadata field with the signature
                await UpdateMetadataWithSignatureAsync(connection, newId, auditLog.Metadata, signature, cancellationToken);

                _logger.LogDebug("Generated integrity signature for audit log {AuditLogId}", newId);
            }
            catch (Exception ex)
            {
                // Don't fail the insert if signature generation fails
                _logger.LogWarning(ex, "Failed to generate integrity signature for audit log {AuditLogId}", newId);
            }
        }

        return newId;
    }

    /// <summary>
    /// Inserts multiple audit log entries in a single batch operation.
    /// Uses Oracle array binding for optimal performance.
    /// Generates cryptographic signatures for tamper-evident audit trail.
    /// </summary>
    public async Task<int> InsertBatchAsync(IEnumerable<SysAuditLog> auditLogs, CancellationToken cancellationToken = default)
    {
        var auditLogList = auditLogs.ToList();
        if (!auditLogList.Any())
        {
            return 0;
        }

        try
        {
            using var connection = _dbContext.CreateConnection();
            await connection.OpenAsync(cancellationToken);

            using var command = connection.CreateCommand();
            command.CommandText = BuildBatchInsertSql();
            command.ArrayBindCount = auditLogList.Count;

            // Prepare arrays for batch binding
            AddBatchInsertParameters(command, auditLogList);

            var rowsAffected = await command.ExecuteNonQueryAsync(cancellationToken);
            
            _logger.LogDebug("Batch inserted {Count} audit log entries", rowsAffected);

            // Generate and store cryptographic signatures if integrity service is available
            var integrityService = GetIntegrityService();
            if (integrityService != null && rowsAffected > 0)
            {
                try
                {
                    await GenerateBatchSignaturesAsync(connection, auditLogList, integrityService, cancellationToken);
                }
                catch (Exception ex)
                {
                    // Don't fail the insert if signature generation fails
                    _logger.LogWarning(ex, "Failed to generate integrity signatures for batch of {Count} audit logs", auditLogList.Count);
                }
            }
            
            return rowsAffected;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to batch insert {Count} audit log entries", auditLogList.Count);
            throw;
        }
    }

    /// <summary>
    /// Retrieves audit logs by correlation ID for request tracing.
    /// </summary>
    public async Task<IEnumerable<SysAuditLog>> GetByCorrelationIdAsync(string correlationId, CancellationToken cancellationToken = default)
    {
        var auditLogs = new List<SysAuditLog>();

        using var connection = _dbContext.CreateConnection();
        await connection.OpenAsync(cancellationToken);

        using var command = connection.CreateCommand();
        command.CommandText = @"
            SELECT * FROM SYS_AUDIT_LOG 
            WHERE CORRELATION_ID = :correlationId 
            ORDER BY CREATION_DATE ASC";

        command.Parameters.Add(new OracleParameter("correlationId", correlationId));

        using var reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            auditLogs.Add(MapToEntity(reader));
        }

        return auditLogs;
    }

    /// <summary>
    /// Retrieves audit logs for a specific entity.
    /// </summary>
    public async Task<IEnumerable<SysAuditLog>> GetByEntityAsync(string entityType, long entityId, CancellationToken cancellationToken = default)
    {
        var auditLogs = new List<SysAuditLog>();

        using var connection = _dbContext.CreateConnection();
        await connection.OpenAsync(cancellationToken);

        using var command = connection.CreateCommand();
        command.CommandText = @"
            SELECT * FROM SYS_AUDIT_LOG 
            WHERE ENTITY_TYPE = :entityType AND ENTITY_ID = :entityId 
            ORDER BY CREATION_DATE DESC";

        command.Parameters.Add(new OracleParameter("entityType", entityType));
        command.Parameters.Add(new OracleParameter("entityId", entityId));

        using var reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            auditLogs.Add(MapToEntity(reader));
        }

        return auditLogs;
    }

    /// <summary>
    /// Checks if the audit repository is healthy and can accept writes.
    /// </summary>
    public async Task<bool> IsHealthyAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            using var connection = _dbContext.CreateConnection();
            await connection.OpenAsync(cancellationToken);

            using var command = connection.CreateCommand();
            command.CommandText = "SELECT 1 FROM DUAL";
            
            await command.ExecuteScalarAsync(cancellationToken);
            
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
    /// </summary>
    public async Task<SysAuditLog?> GetByIdAsync(long auditLogId, CancellationToken cancellationToken = default)
    {
        using var connection = _dbContext.CreateConnection();
        await connection.OpenAsync(cancellationToken);

        using var command = connection.CreateCommand();
        command.CommandText = @"
            SELECT * FROM SYS_AUDIT_LOG 
            WHERE ROW_ID = :auditLogId";

        command.Parameters.Add(new OracleParameter("auditLogId", auditLogId));

        using var reader = await command.ExecuteReaderAsync(cancellationToken);
        if (await reader.ReadAsync(cancellationToken))
        {
            return MapToEntity(reader);
        }

        return null;
    }

    /// <summary>
    /// Retrieves all audit log IDs within a specified date range.
    /// Used for batch integrity verification and tampering detection.
    /// </summary>
    public async Task<IEnumerable<long>> GetAuditLogIdsByDateRangeAsync(
        DateTime startDate,
        DateTime endDate,
        CancellationToken cancellationToken = default)
    {
        var ids = new List<long>();

        using var connection = _dbContext.CreateConnection();
        await connection.OpenAsync(cancellationToken);

        using var command = connection.CreateCommand();
        command.CommandText = @"
            SELECT ROW_ID FROM SYS_AUDIT_LOG 
            WHERE CREATION_DATE >= :startDate AND CREATION_DATE <= :endDate 
            ORDER BY CREATION_DATE ASC";

        command.Parameters.Add(new OracleParameter("startDate", startDate));
        command.Parameters.Add(new OracleParameter("endDate", endDate));

        using var reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            ids.Add(reader.GetInt64(0));
        }

        _logger.LogDebug(
            "Retrieved {Count} audit log IDs for date range {StartDate} to {EndDate}",
            ids.Count, startDate, endDate);

        return ids;
    }

    /// <summary>
    /// Builds the INSERT SQL statement for single audit log entry.
    /// </summary>
    private string BuildInsertSql()
    {
        return @"
            INSERT INTO SYS_AUDIT_LOG (
                ROW_ID, ACTOR_TYPE, ACTOR_ID, COMPANY_ID, BRANCH_ID, ACTION, 
                ENTITY_TYPE, ENTITY_ID, OLD_VALUE, NEW_VALUE, IP_ADDRESS, USER_AGENT,
                CORRELATION_ID, HTTP_METHOD, ENDPOINT_PATH, REQUEST_PAYLOAD, RESPONSE_PAYLOAD,
                EXECUTION_TIME_MS, STATUS_CODE, EXCEPTION_TYPE, EXCEPTION_MESSAGE, STACK_TRACE,
                SEVERITY, EVENT_CATEGORY, METADATA, BUSINESS_MODULE, DEVICE_IDENTIFIER,
                ERROR_CODE, BUSINESS_DESCRIPTION, CREATION_DATE
            ) VALUES (
                SEQ_SYS_AUDIT_LOG.NEXTVAL, :actorType, :actorId, :companyId, :branchId, :action,
                :entityType, :entityId, :oldValue, :newValue, :ipAddress, :userAgent,
                :correlationId, :httpMethod, :endpointPath, :requestPayload, :responsePayload,
                :executionTimeMs, :statusCode, :exceptionType, :exceptionMessage, :stackTrace,
                :severity, :eventCategory, :metadata, :businessModule, :deviceIdentifier,
                :errorCode, :businessDescription, :creationDate
            ) RETURNING ROW_ID INTO :P_NEW_ID";
    }

    /// <summary>
    /// Builds the batch INSERT SQL statement for multiple audit log entries.
    /// </summary>
    private string BuildBatchInsertSql()
    {
        return @"
            INSERT INTO SYS_AUDIT_LOG (
                ROW_ID, ACTOR_TYPE, ACTOR_ID, COMPANY_ID, BRANCH_ID, ACTION, 
                ENTITY_TYPE, ENTITY_ID, OLD_VALUE, NEW_VALUE, IP_ADDRESS, USER_AGENT,
                CORRELATION_ID, HTTP_METHOD, ENDPOINT_PATH, REQUEST_PAYLOAD, RESPONSE_PAYLOAD,
                EXECUTION_TIME_MS, STATUS_CODE, EXCEPTION_TYPE, EXCEPTION_MESSAGE, STACK_TRACE,
                SEVERITY, EVENT_CATEGORY, METADATA, BUSINESS_MODULE, DEVICE_IDENTIFIER,
                ERROR_CODE, BUSINESS_DESCRIPTION, CREATION_DATE
            ) VALUES (
                SEQ_SYS_AUDIT_LOG.NEXTVAL, :actorType, :actorId, :companyId, :branchId, :action,
                :entityType, :entityId, :oldValue, :newValue, :ipAddress, :userAgent,
                :correlationId, :httpMethod, :endpointPath, :requestPayload, :responsePayload,
                :executionTimeMs, :statusCode, :exceptionType, :exceptionMessage, :stackTrace,
                :severity, :eventCategory, :metadata, :businessModule, :deviceIdentifier,
                :errorCode, :businessDescription, :creationDate
            )";
    }

    /// <summary>
    /// Adds parameters for single insert operation.
    /// </summary>
    private void AddInsertParameters(OracleCommand command, SysAuditLog auditLog)
    {
        command.Parameters.Add(new OracleParameter("actorType", OracleDbType.Varchar2) { Value = auditLog.ActorType });
        command.Parameters.Add(new OracleParameter("actorId", OracleDbType.Decimal) { Value = auditLog.ActorId });
        command.Parameters.Add(new OracleParameter("companyId", OracleDbType.Decimal) { Value = (object?)auditLog.CompanyId ?? DBNull.Value });
        command.Parameters.Add(new OracleParameter("branchId", OracleDbType.Decimal) { Value = (object?)auditLog.BranchId ?? DBNull.Value });
        command.Parameters.Add(new OracleParameter("action", OracleDbType.Varchar2) { Value = auditLog.Action });
        command.Parameters.Add(new OracleParameter("entityType", OracleDbType.Varchar2) { Value = auditLog.EntityType });
        command.Parameters.Add(new OracleParameter("entityId", OracleDbType.Decimal) { Value = (object?)auditLog.EntityId ?? DBNull.Value });
        command.Parameters.Add(new OracleParameter("oldValue", OracleDbType.Clob) { Value = (object?)auditLog.OldValue ?? DBNull.Value });
        command.Parameters.Add(new OracleParameter("newValue", OracleDbType.Clob) { Value = (object?)auditLog.NewValue ?? DBNull.Value });
        command.Parameters.Add(new OracleParameter("ipAddress", OracleDbType.Varchar2) { Value = (object?)auditLog.IpAddress ?? DBNull.Value });
        command.Parameters.Add(new OracleParameter("userAgent", OracleDbType.Varchar2) { Value = (object?)auditLog.UserAgent ?? DBNull.Value });
        command.Parameters.Add(new OracleParameter("correlationId", OracleDbType.Varchar2) { Value = (object?)auditLog.CorrelationId ?? DBNull.Value });
        command.Parameters.Add(new OracleParameter("httpMethod", OracleDbType.Varchar2) { Value = (object?)auditLog.HttpMethod ?? DBNull.Value });
        command.Parameters.Add(new OracleParameter("endpointPath", OracleDbType.Varchar2) { Value = (object?)auditLog.EndpointPath ?? DBNull.Value });
        command.Parameters.Add(new OracleParameter("requestPayload", OracleDbType.Clob) { Value = (object?)auditLog.RequestPayload ?? DBNull.Value });
        command.Parameters.Add(new OracleParameter("responsePayload", OracleDbType.Clob) { Value = (object?)auditLog.ResponsePayload ?? DBNull.Value });
        command.Parameters.Add(new OracleParameter("executionTimeMs", OracleDbType.Decimal) { Value = (object?)auditLog.ExecutionTimeMs ?? DBNull.Value });
        command.Parameters.Add(new OracleParameter("statusCode", OracleDbType.Decimal) { Value = (object?)auditLog.StatusCode ?? DBNull.Value });
        command.Parameters.Add(new OracleParameter("exceptionType", OracleDbType.Varchar2) { Value = (object?)auditLog.ExceptionType ?? DBNull.Value });
        command.Parameters.Add(new OracleParameter("exceptionMessage", OracleDbType.Varchar2) { Value = (object?)auditLog.ExceptionMessage ?? DBNull.Value });
        command.Parameters.Add(new OracleParameter("stackTrace", OracleDbType.Clob) { Value = (object?)auditLog.StackTrace ?? DBNull.Value });
        command.Parameters.Add(new OracleParameter("severity", OracleDbType.Varchar2) { Value = auditLog.Severity });
        command.Parameters.Add(new OracleParameter("eventCategory", OracleDbType.Varchar2) { Value = auditLog.EventCategory });
        command.Parameters.Add(new OracleParameter("metadata", OracleDbType.Clob) { Value = (object?)auditLog.Metadata ?? DBNull.Value });
        command.Parameters.Add(new OracleParameter("businessModule", OracleDbType.Varchar2) { Value = (object?)auditLog.BusinessModule ?? DBNull.Value });
        command.Parameters.Add(new OracleParameter("deviceIdentifier", OracleDbType.Varchar2) { Value = (object?)auditLog.DeviceIdentifier ?? DBNull.Value });
        command.Parameters.Add(new OracleParameter("errorCode", OracleDbType.Varchar2) { Value = (object?)auditLog.ErrorCode ?? DBNull.Value });
        command.Parameters.Add(new OracleParameter("businessDescription", OracleDbType.Varchar2) { Value = (object?)auditLog.BusinessDescription ?? DBNull.Value });
        command.Parameters.Add(new OracleParameter("creationDate", OracleDbType.Date) { Value = auditLog.CreationDate });
    }

    /// <summary>
    /// Adds parameters for batch insert operation using Oracle array binding.
    /// </summary>
    private void AddBatchInsertParameters(OracleCommand command, List<SysAuditLog> auditLogs)
    {
        var count = auditLogs.Count;

        // Prepare arrays for each parameter
        var actorTypes = new string[count];
        var actorIds = new long[count];
        var companyIds = new object[count];
        var branchIds = new object[count];
        var actions = new string[count];
        var entityTypes = new string[count];
        var entityIds = new object[count];
        var oldValues = new object[count];
        var newValues = new object[count];
        var ipAddresses = new object[count];
        var userAgents = new object[count];
        var correlationIds = new object[count];
        var httpMethods = new object[count];
        var endpointPaths = new object[count];
        var requestPayloads = new object[count];
        var responsePayloads = new object[count];
        var executionTimes = new object[count];
        var statusCodes = new object[count];
        var exceptionTypes = new object[count];
        var exceptionMessages = new object[count];
        var stackTraces = new object[count];
        var severities = new string[count];
        var eventCategories = new string[count];
        var metadatas = new object[count];
        var businessModules = new object[count];
        var deviceIdentifiers = new object[count];
        var errorCodes = new object[count];
        var businessDescriptions = new object[count];
        var creationDates = new DateTime[count];

        // Populate arrays
        for (int i = 0; i < count; i++)
        {
            var log = auditLogs[i];
            actorTypes[i] = log.ActorType;
            actorIds[i] = log.ActorId;
            companyIds[i] = (object?)log.CompanyId ?? DBNull.Value;
            branchIds[i] = (object?)log.BranchId ?? DBNull.Value;
            actions[i] = log.Action;
            entityTypes[i] = log.EntityType;
            entityIds[i] = (object?)log.EntityId ?? DBNull.Value;
            oldValues[i] = (object?)log.OldValue ?? DBNull.Value;
            newValues[i] = (object?)log.NewValue ?? DBNull.Value;
            ipAddresses[i] = (object?)log.IpAddress ?? DBNull.Value;
            userAgents[i] = (object?)log.UserAgent ?? DBNull.Value;
            correlationIds[i] = (object?)log.CorrelationId ?? DBNull.Value;
            httpMethods[i] = (object?)log.HttpMethod ?? DBNull.Value;
            endpointPaths[i] = (object?)log.EndpointPath ?? DBNull.Value;
            requestPayloads[i] = (object?)log.RequestPayload ?? DBNull.Value;
            responsePayloads[i] = (object?)log.ResponsePayload ?? DBNull.Value;
            executionTimes[i] = (object?)log.ExecutionTimeMs ?? DBNull.Value;
            statusCodes[i] = (object?)log.StatusCode ?? DBNull.Value;
            exceptionTypes[i] = (object?)log.ExceptionType ?? DBNull.Value;
            exceptionMessages[i] = (object?)log.ExceptionMessage ?? DBNull.Value;
            stackTraces[i] = (object?)log.StackTrace ?? DBNull.Value;
            severities[i] = log.Severity;
            eventCategories[i] = log.EventCategory;
            metadatas[i] = (object?)log.Metadata ?? DBNull.Value;
            businessModules[i] = (object?)log.BusinessModule ?? DBNull.Value;
            deviceIdentifiers[i] = (object?)log.DeviceIdentifier ?? DBNull.Value;
            errorCodes[i] = (object?)log.ErrorCode ?? DBNull.Value;
            businessDescriptions[i] = (object?)log.BusinessDescription ?? DBNull.Value;
            creationDates[i] = log.CreationDate;
        }

        // Add array parameters
        command.Parameters.Add(new OracleParameter("actorType", OracleDbType.Varchar2) { Value = actorTypes });
        command.Parameters.Add(new OracleParameter("actorId", OracleDbType.Decimal) { Value = actorIds });
        command.Parameters.Add(new OracleParameter("companyId", OracleDbType.Decimal) { Value = companyIds });
        command.Parameters.Add(new OracleParameter("branchId", OracleDbType.Decimal) { Value = branchIds });
        command.Parameters.Add(new OracleParameter("action", OracleDbType.Varchar2) { Value = actions });
        command.Parameters.Add(new OracleParameter("entityType", OracleDbType.Varchar2) { Value = entityTypes });
        command.Parameters.Add(new OracleParameter("entityId", OracleDbType.Decimal) { Value = entityIds });
        command.Parameters.Add(new OracleParameter("oldValue", OracleDbType.Clob) { Value = oldValues });
        command.Parameters.Add(new OracleParameter("newValue", OracleDbType.Clob) { Value = newValues });
        command.Parameters.Add(new OracleParameter("ipAddress", OracleDbType.Varchar2) { Value = ipAddresses });
        command.Parameters.Add(new OracleParameter("userAgent", OracleDbType.Varchar2) { Value = userAgents });
        command.Parameters.Add(new OracleParameter("correlationId", OracleDbType.Varchar2) { Value = correlationIds });
        command.Parameters.Add(new OracleParameter("httpMethod", OracleDbType.Varchar2) { Value = httpMethods });
        command.Parameters.Add(new OracleParameter("endpointPath", OracleDbType.Varchar2) { Value = endpointPaths });
        command.Parameters.Add(new OracleParameter("requestPayload", OracleDbType.Clob) { Value = requestPayloads });
        command.Parameters.Add(new OracleParameter("responsePayload", OracleDbType.Clob) { Value = responsePayloads });
        command.Parameters.Add(new OracleParameter("executionTimeMs", OracleDbType.Decimal) { Value = executionTimes });
        command.Parameters.Add(new OracleParameter("statusCode", OracleDbType.Decimal) { Value = statusCodes });
        command.Parameters.Add(new OracleParameter("exceptionType", OracleDbType.Varchar2) { Value = exceptionTypes });
        command.Parameters.Add(new OracleParameter("exceptionMessage", OracleDbType.Varchar2) { Value = exceptionMessages });
        command.Parameters.Add(new OracleParameter("stackTrace", OracleDbType.Clob) { Value = stackTraces });
        command.Parameters.Add(new OracleParameter("severity", OracleDbType.Varchar2) { Value = severities });
        command.Parameters.Add(new OracleParameter("eventCategory", OracleDbType.Varchar2) { Value = eventCategories });
        command.Parameters.Add(new OracleParameter("metadata", OracleDbType.Clob) { Value = metadatas });
        command.Parameters.Add(new OracleParameter("businessModule", OracleDbType.Varchar2) { Value = businessModules });
        command.Parameters.Add(new OracleParameter("deviceIdentifier", OracleDbType.Varchar2) { Value = deviceIdentifiers });
        command.Parameters.Add(new OracleParameter("errorCode", OracleDbType.Varchar2) { Value = errorCodes });
        command.Parameters.Add(new OracleParameter("businessDescription", OracleDbType.Varchar2) { Value = businessDescriptions });
        command.Parameters.Add(new OracleParameter("creationDate", OracleDbType.Date) { Value = creationDates });
    }

    /// <summary>
    /// Maps an OracleDataReader row to a SysAuditLog entity.
    /// </summary>
    private SysAuditLog MapToEntity(OracleDataReader reader)
    {
        return new SysAuditLog
        {
            RowId = reader.GetInt64(reader.GetOrdinal("ROW_ID")),
            ActorType = reader.GetString(reader.GetOrdinal("ACTOR_TYPE")),
            ActorId = reader.GetInt64(reader.GetOrdinal("ACTOR_ID")),
            CompanyId = reader.IsDBNull(reader.GetOrdinal("COMPANY_ID")) ? null : reader.GetInt64(reader.GetOrdinal("COMPANY_ID")),
            BranchId = reader.IsDBNull(reader.GetOrdinal("BRANCH_ID")) ? null : reader.GetInt64(reader.GetOrdinal("BRANCH_ID")),
            Action = reader.GetString(reader.GetOrdinal("ACTION")),
            EntityType = reader.GetString(reader.GetOrdinal("ENTITY_TYPE")),
            EntityId = reader.IsDBNull(reader.GetOrdinal("ENTITY_ID")) ? null : reader.GetInt64(reader.GetOrdinal("ENTITY_ID")),
            OldValue = reader.IsDBNull(reader.GetOrdinal("OLD_VALUE")) ? null : reader.GetString(reader.GetOrdinal("OLD_VALUE")),
            NewValue = reader.IsDBNull(reader.GetOrdinal("NEW_VALUE")) ? null : reader.GetString(reader.GetOrdinal("NEW_VALUE")),
            IpAddress = reader.IsDBNull(reader.GetOrdinal("IP_ADDRESS")) ? null : reader.GetString(reader.GetOrdinal("IP_ADDRESS")),
            UserAgent = reader.IsDBNull(reader.GetOrdinal("USER_AGENT")) ? null : reader.GetString(reader.GetOrdinal("USER_AGENT")),
            CorrelationId = reader.IsDBNull(reader.GetOrdinal("CORRELATION_ID")) ? null : reader.GetString(reader.GetOrdinal("CORRELATION_ID")),
            HttpMethod = reader.IsDBNull(reader.GetOrdinal("HTTP_METHOD")) ? null : reader.GetString(reader.GetOrdinal("HTTP_METHOD")),
            EndpointPath = reader.IsDBNull(reader.GetOrdinal("ENDPOINT_PATH")) ? null : reader.GetString(reader.GetOrdinal("ENDPOINT_PATH")),
            RequestPayload = reader.IsDBNull(reader.GetOrdinal("REQUEST_PAYLOAD")) ? null : reader.GetString(reader.GetOrdinal("REQUEST_PAYLOAD")),
            ResponsePayload = reader.IsDBNull(reader.GetOrdinal("RESPONSE_PAYLOAD")) ? null : reader.GetString(reader.GetOrdinal("RESPONSE_PAYLOAD")),
            ExecutionTimeMs = reader.IsDBNull(reader.GetOrdinal("EXECUTION_TIME_MS")) ? null : reader.GetInt64(reader.GetOrdinal("EXECUTION_TIME_MS")),
            StatusCode = reader.IsDBNull(reader.GetOrdinal("STATUS_CODE")) ? null : reader.GetInt32(reader.GetOrdinal("STATUS_CODE")),
            ExceptionType = reader.IsDBNull(reader.GetOrdinal("EXCEPTION_TYPE")) ? null : reader.GetString(reader.GetOrdinal("EXCEPTION_TYPE")),
            ExceptionMessage = reader.IsDBNull(reader.GetOrdinal("EXCEPTION_MESSAGE")) ? null : reader.GetString(reader.GetOrdinal("EXCEPTION_MESSAGE")),
            StackTrace = reader.IsDBNull(reader.GetOrdinal("STACK_TRACE")) ? null : reader.GetString(reader.GetOrdinal("STACK_TRACE")),
            Severity = reader.GetString(reader.GetOrdinal("SEVERITY")),
            EventCategory = reader.GetString(reader.GetOrdinal("EVENT_CATEGORY")),
            Metadata = reader.IsDBNull(reader.GetOrdinal("METADATA")) ? null : reader.GetString(reader.GetOrdinal("METADATA")),
            BusinessModule = reader.IsDBNull(reader.GetOrdinal("BUSINESS_MODULE")) ? null : reader.GetString(reader.GetOrdinal("BUSINESS_MODULE")),
            DeviceIdentifier = reader.IsDBNull(reader.GetOrdinal("DEVICE_IDENTIFIER")) ? null : reader.GetString(reader.GetOrdinal("DEVICE_IDENTIFIER")),
            ErrorCode = reader.IsDBNull(reader.GetOrdinal("ERROR_CODE")) ? null : reader.GetString(reader.GetOrdinal("ERROR_CODE")),
            BusinessDescription = reader.IsDBNull(reader.GetOrdinal("BUSINESS_DESCRIPTION")) ? null : reader.GetString(reader.GetOrdinal("BUSINESS_DESCRIPTION")),
            CreationDate = reader.GetDateTime(reader.GetOrdinal("CREATION_DATE"))
        };
    }

    /// <summary>
    /// Updates the metadata field of an audit log entry with the cryptographic signature.
    /// Merges the signature into existing metadata JSON or creates new metadata.
    /// </summary>
    private async Task UpdateMetadataWithSignatureAsync(
        OracleConnection connection,
        long auditLogId,
        string? existingMetadata,
        string signature,
        CancellationToken cancellationToken)
    {
        // Parse existing metadata or create new object
        Dictionary<string, object> metadata;
        if (!string.IsNullOrWhiteSpace(existingMetadata))
        {
            try
            {
                metadata = JsonSerializer.Deserialize<Dictionary<string, object>>(existingMetadata) 
                    ?? new Dictionary<string, object>();
            }
            catch
            {
                // If parsing fails, create new metadata
                metadata = new Dictionary<string, object>();
            }
        }
        else
        {
            metadata = new Dictionary<string, object>();
        }

        // Add or update the integrity_hash field
        metadata["integrity_hash"] = signature;

        // Serialize back to JSON
        var updatedMetadata = JsonSerializer.Serialize(metadata);

        // Update the database
        using var updateCommand = connection.CreateCommand();
        updateCommand.CommandText = @"
            UPDATE SYS_AUDIT_LOG 
            SET METADATA = :metadata 
            WHERE ROW_ID = :rowId";

        updateCommand.Parameters.Add(new OracleParameter("metadata", OracleDbType.Clob) { Value = updatedMetadata });
        updateCommand.Parameters.Add(new OracleParameter("rowId", OracleDbType.Decimal) { Value = auditLogId });

        await updateCommand.ExecuteNonQueryAsync(cancellationToken);
    }

    /// <summary>
    /// Generates cryptographic signatures for a batch of audit log entries.
    /// Retrieves the generated ROW_IDs and updates each entry with its signature.
    /// </summary>
    private async Task GenerateBatchSignaturesAsync(
        OracleConnection connection,
        List<SysAuditLog> auditLogs,
        IAuditLogIntegrityService integrityService,
        CancellationToken cancellationToken)
    {
        // Get the most recent audit log IDs that match our batch
        // We assume they were just inserted and are the most recent entries
        using var selectCommand = connection.CreateCommand();
        selectCommand.CommandText = @"
            SELECT ROW_ID, ACTOR_ID, ACTION, ENTITY_TYPE, ENTITY_ID, CREATION_DATE, OLD_VALUE, NEW_VALUE, METADATA
            FROM (
                SELECT * FROM SYS_AUDIT_LOG 
                ORDER BY ROW_ID DESC
            )
            WHERE ROWNUM <= :batchSize";

        selectCommand.Parameters.Add(new OracleParameter("batchSize", OracleDbType.Decimal) { Value = auditLogs.Count });

        var retrievedEntries = new List<(long RowId, long ActorId, string Action, string EntityType, long? EntityId, DateTime CreationDate, string? OldValue, string? NewValue, string? Metadata)>();

        using var reader = await selectCommand.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            retrievedEntries.Add((
                reader.GetInt64(0),
                reader.GetInt64(1),
                reader.GetString(2),
                reader.GetString(3),
                reader.IsDBNull(4) ? null : reader.GetInt64(4),
                reader.GetDateTime(5),
                reader.IsDBNull(6) ? null : reader.GetString(6),
                reader.IsDBNull(7) ? null : reader.GetString(7),
                reader.IsDBNull(8) ? null : reader.GetString(8)
            ));
        }

        // Reverse to match insertion order
        retrievedEntries.Reverse();

        // Generate signatures and update in batch
        var updateBatch = new List<(long RowId, string UpdatedMetadata)>();

        for (int i = 0; i < Math.Min(retrievedEntries.Count, auditLogs.Count); i++)
        {
            var entry = retrievedEntries[i];
            
            try
            {
                var signature = integrityService.GenerateIntegrityHash(
                    entry.RowId,
                    entry.ActorId,
                    entry.Action,
                    entry.EntityType,
                    entry.EntityId,
                    entry.CreationDate,
                    entry.OldValue,
                    entry.NewValue);

                // Parse existing metadata or create new object
                Dictionary<string, object> metadata;
                if (!string.IsNullOrWhiteSpace(entry.Metadata))
                {
                    try
                    {
                        metadata = JsonSerializer.Deserialize<Dictionary<string, object>>(entry.Metadata) 
                            ?? new Dictionary<string, object>();
                    }
                    catch
                    {
                        metadata = new Dictionary<string, object>();
                    }
                }
                else
                {
                    metadata = new Dictionary<string, object>();
                }

                metadata["integrity_hash"] = signature;
                var updatedMetadata = JsonSerializer.Serialize(metadata);

                updateBatch.Add((entry.RowId, updatedMetadata));
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to generate signature for audit log {RowId}", entry.RowId);
            }
        }

        // Perform batch update of metadata
        if (updateBatch.Any())
        {
            using var updateCommand = connection.CreateCommand();
            updateCommand.CommandText = @"
                UPDATE SYS_AUDIT_LOG 
                SET METADATA = :metadata 
                WHERE ROW_ID = :rowId";

            updateCommand.ArrayBindCount = updateBatch.Count;

            var rowIds = updateBatch.Select(x => (object)x.RowId).ToArray();
            var metadatas = updateBatch.Select(x => (object)x.UpdatedMetadata).ToArray();

            updateCommand.Parameters.Add(new OracleParameter("metadata", OracleDbType.Clob) { Value = metadatas });
            updateCommand.Parameters.Add(new OracleParameter("rowId", OracleDbType.Decimal) { Value = rowIds });

            await updateCommand.ExecuteNonQueryAsync(cancellationToken);

            _logger.LogDebug("Generated and stored {Count} integrity signatures for batch", updateBatch.Count);
        }
    }
}
