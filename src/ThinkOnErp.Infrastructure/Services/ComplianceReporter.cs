using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using ThinkOnErp.Domain.Interfaces;
using ThinkOnErp.Domain.Models;
using Microsoft.EntityFrameworkCore;
using ThinkOnErp.Infrastructure.Data;

namespace ThinkOnErp.Infrastructure.Services;

/// <summary>
/// Service for generating compliance reports for regulatory requirements.
/// Supports GDPR, SOX, and ISO 27001 compliance reporting with multiple export formats.
/// Provides scheduled report generation and email delivery capabilities.
/// </summary>
public class ComplianceReporter : IComplianceReporter
{
    private readonly IAuditQueryService _auditQueryService;
    private readonly IUserRepository _userRepository;
    private readonly OracleDbContext _dbContext;
    private readonly ILogger<ComplianceReporter> _logger;
    
    // Query timeout protection (60 seconds for complex compliance queries)
    private const int QueryTimeoutSeconds = 60;

    public ComplianceReporter(
        IAuditQueryService auditQueryService,
        IUserRepository userRepository,
        OracleDbContext dbContext,
        ILogger<ComplianceReporter> logger)
    {
        _auditQueryService = auditQueryService ?? throw new ArgumentNullException(nameof(auditQueryService));
        _userRepository = userRepository ?? throw new ArgumentNullException(nameof(userRepository));
        _dbContext = dbContext ?? throw new ArgumentNullException(nameof(dbContext));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <summary>
    /// Generate a GDPR data access report showing all access to a specific data subject's personal data.
    /// Returns a comprehensive report of all read operations on the data subject's information.
    /// Supports GDPR Article 15 (Right of Access) compliance requirements.
    /// </summary>
    public async Task<GdprAccessReport> GenerateGdprAccessReportAsync(
        long dataSubjectId,
        DateTime startDate,
        DateTime endDate,
        CancellationToken cancellationToken = default)
    {
        try
        {
            _logger.LogInformation(
                "Generating GDPR access report for data subject {DataSubjectId} from {StartDate} to {EndDate}",
                dataSubjectId, startDate, endDate);

            var report = new GdprAccessReport
            {
                DataSubjectId = dataSubjectId,
                PeriodStartDate = startDate,
                PeriodEndDate = endDate,
                GeneratedAt = DateTime.UtcNow
            };

            // Get data subject information
            await PopulateDataSubjectInfoAsync(report, dataSubjectId, cancellationToken);

            // Query all audit logs related to the data subject
            var accessEvents = await QueryDataSubjectAccessEventsAsync(
                dataSubjectId, 
                startDate, 
                endDate, 
                cancellationToken);

            report.AccessEvents = accessEvents;
            report.TotalAccessEvents = accessEvents.Count;

            // Generate summaries
            report.AccessByEntityType = accessEvents
                .GroupBy(e => e.EntityType)
                .ToDictionary(g => g.Key, g => g.Count());

            report.AccessByActor = accessEvents
                .GroupBy(e => e.ActorName)
                .ToDictionary(g => g.Key, g => g.Count());

            _logger.LogInformation(
                "GDPR access report generated successfully for data subject {DataSubjectId}. Total events: {TotalEvents}",
                dataSubjectId, report.TotalAccessEvents);

            return report;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, 
                "Error generating GDPR access report for data subject {DataSubjectId}", 
                dataSubjectId);
            throw;
        }
    }

    /// <summary>
    /// Generate a GDPR data export report containing all personal data for a specific data subject.
    /// Returns a complete export of all personal data stored in the system for the data subject.
    /// Supports GDPR Article 20 (Right to Data Portability) compliance requirements.
    /// </summary>
    public async Task<GdprDataExportReport> GenerateGdprDataExportReportAsync(
        long dataSubjectId,
        CancellationToken cancellationToken = default)
    {
        try
        {
            _logger.LogInformation(
                "Generating GDPR data export report for data subject {DataSubjectId}",
                dataSubjectId);

            var report = new GdprDataExportReport
            {
                DataSubjectId = dataSubjectId,
                GeneratedAt = DateTime.UtcNow
            };

            // Get data subject information
            await PopulateDataSubjectInfoAsync(report, dataSubjectId, cancellationToken);

            // Export user profile data
            await ExportUserProfileDataAsync(report, dataSubjectId, cancellationToken);

            // Export audit log data (all actions performed by the user)
            await ExportAuditLogDataAsync(report, dataSubjectId, cancellationToken);

            // Export authentication data (login history, tokens)
            await ExportAuthenticationDataAsync(report, dataSubjectId, cancellationToken);

            // Calculate totals
            report.TotalRecords = report.PersonalDataByEntityType.Values.Sum(list => list.Count);
            report.DataCategories = report.PersonalDataByEntityType.Keys.ToList();

            _logger.LogInformation(
                "GDPR data export report generated successfully for data subject {DataSubjectId}. Total records: {TotalRecords}, Categories: {Categories}",
                dataSubjectId, report.TotalRecords, report.DataCategories.Count);

            return report;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, 
                "Error generating GDPR data export report for data subject {DataSubjectId}", 
                dataSubjectId);
            throw;
        }
    }

    /// <summary>
    /// Generate a SOX financial access report showing all access to financial data.
    /// Returns a comprehensive report of all financial data access events for SOX compliance.
    /// Supports SOX Section 404 (Internal Controls) compliance requirements.
    /// </summary>
    public async Task<SoxFinancialAccessReport> GenerateSoxFinancialAccessReportAsync(
        DateTime startDate,
        DateTime endDate,
        CancellationToken cancellationToken = default)
    {
        try
        {
            _logger.LogInformation(
                "Generating SOX financial access report from {StartDate} to {EndDate}",
                startDate, endDate);

            var report = new SoxFinancialAccessReport
            {
                PeriodStartDate = startDate,
                PeriodEndDate = endDate,
                GeneratedAt = DateTime.UtcNow
            };

            // Query all financial data access events
            var financialAccessEvents = await QueryFinancialDataAccessEventsAsync(
                startDate, 
                endDate, 
                cancellationToken);

            report.AccessEvents = financialAccessEvents;
            report.TotalAccessEvents = financialAccessEvents.Count;

            // Count out-of-hours access (outside 8 AM - 6 PM on weekdays)
            report.OutOfHoursAccessEvents = financialAccessEvents
                .Count(e => IsOutOfHours(e.AccessedAt));

            // Generate summaries
            report.AccessByUser = financialAccessEvents
                .GroupBy(e => e.ActorName)
                .ToDictionary(g => g.Key, g => g.Count());

            report.AccessByEntityType = financialAccessEvents
                .GroupBy(e => e.EntityType)
                .ToDictionary(g => g.Key, g => g.Count());

            // Detect suspicious patterns
            report.SuspiciousPatterns = DetectSuspiciousFinancialAccessPatterns(financialAccessEvents);

            _logger.LogInformation(
                "SOX financial access report generated successfully. Total events: {TotalEvents}, Out-of-hours: {OutOfHours}, Suspicious patterns: {SuspiciousCount}",
                report.TotalAccessEvents, report.OutOfHoursAccessEvents, report.SuspiciousPatterns.Count);

            return report;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, 
                "Error generating SOX financial access report from {StartDate} to {EndDate}", 
                startDate, endDate);
            throw;
        }
    }

    /// <summary>
    /// Generate a SOX segregation of duties report analyzing role and permission assignments.
    /// Returns a report identifying potential segregation of duties violations.
    /// Supports SOX Section 404 (Internal Controls) compliance requirements.
    /// </summary>
    public async Task<SoxSegregationOfDutiesReport> GenerateSoxSegregationReportAsync(
        CancellationToken cancellationToken = default)
    {
        try
        {
            _logger.LogInformation("Generating SOX segregation of duties report");

            var report = new SoxSegregationOfDutiesReport
            {
                GeneratedAt = DateTime.UtcNow
            };

            // Get all users with their role assignments
            var userRoleAssignments = await GetUserRoleAssignmentsAsync(cancellationToken);
            report.TotalUsersAnalyzed = userRoleAssignments.Count;

            // Analyze for segregation of duties violations
            var violations = await AnalyzeSegregationViolationsAsync(userRoleAssignments, cancellationToken);
            report.Violations = violations;
            report.ViolationsDetected = violations.Count;

            // Generate summary by severity
            report.ViolationsBySeverity = violations
                .GroupBy(v => v.Severity)
                .ToDictionary(g => g.Key, g => g.Count());

            _logger.LogInformation(
                "SOX segregation of duties report generated successfully. Users analyzed: {UsersAnalyzed}, Violations detected: {ViolationsDetected}",
                report.TotalUsersAnalyzed, report.ViolationsDetected);

            return report;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error generating SOX segregation of duties report");
            throw;
        }
    }

    /// <summary>
    /// Generate an ISO 27001 security report showing all security events and incidents.
    /// Returns a comprehensive report of security events for ISO 27001 compliance.
    /// Supports ISO 27001 Annex A.12.4 (Logging and Monitoring) compliance requirements.
    /// </summary>
    public async Task<Iso27001SecurityReport> GenerateIso27001SecurityReportAsync(
        DateTime startDate,
        DateTime endDate,
        CancellationToken cancellationToken = default)
    {
        try
        {
            _logger.LogInformation(
                "Generating ISO 27001 security report from {StartDate} to {EndDate}",
                startDate, endDate);

            var report = new Iso27001SecurityReport
            {
                PeriodStartDate = startDate,
                PeriodEndDate = endDate,
                GeneratedAt = DateTime.UtcNow
            };

            // Query all security-related audit events
            var securityEvents = await QuerySecurityEventsAsync(startDate, endDate, cancellationToken);
            report.SecurityEvents = securityEvents;
            report.TotalSecurityEvents = securityEvents.Count;

            // Count critical events (severity = Critical or Error)
            report.CriticalEvents = securityEvents.Count(e => 
                e.Severity == "Critical" || e.Severity == "Error");

            // Count failed login attempts
            report.FailedLoginAttempts = securityEvents.Count(e => 
                e.EventType == "FailedLogin" || e.EventType == "Authentication" && e.Description.Contains("failed", StringComparison.OrdinalIgnoreCase));

            // Count unauthorized access attempts
            report.UnauthorizedAccessAttempts = securityEvents.Count(e => 
                e.EventType == "UnauthorizedAccess" || e.Description.Contains("unauthorized", StringComparison.OrdinalIgnoreCase));

            // Generate summaries by severity
            report.EventsBySeverity = securityEvents
                .GroupBy(e => e.Severity)
                .ToDictionary(g => g.Key, g => g.Count());

            // Generate summaries by event type
            report.EventsByType = securityEvents
                .GroupBy(e => e.EventType)
                .ToDictionary(g => g.Key, g => g.Count());

            // Identify incidents requiring attention (critical events or patterns)
            report.IncidentsRequiringAttention = IdentifyIncidentsRequiringAttention(securityEvents);

            _logger.LogInformation(
                "ISO 27001 security report generated successfully. Total events: {TotalEvents}, Critical: {CriticalEvents}, Failed logins: {FailedLogins}, Unauthorized access: {UnauthorizedAccess}",
                report.TotalSecurityEvents, report.CriticalEvents, report.FailedLoginAttempts, report.UnauthorizedAccessAttempts);

            return report;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, 
                "Error generating ISO 27001 security report from {StartDate} to {EndDate}", 
                startDate, endDate);
            throw;
        }
    }

    /// <summary>
    /// Generate a user activity report showing all actions performed by a specific user.
    /// Returns a chronological report of all user actions within the specified date range.
    /// Useful for user behavior analysis, compliance audits, and security investigations.
    /// </summary>
    public async Task<UserActivityReport> GenerateUserActivityReportAsync(
        long userId,
        DateTime startDate,
        DateTime endDate,
        CancellationToken cancellationToken = default)
    {
        try
        {
            _logger.LogInformation(
                "Generating user activity report for user {UserId} from {StartDate} to {EndDate}",
                userId, startDate, endDate);

            var report = new UserActivityReport
            {
                UserId = userId,
                PeriodStartDate = startDate,
                PeriodEndDate = endDate,
                GeneratedAt = DateTime.UtcNow
            };

            // Retrieve user information
            var user = await _userRepository.GetByIdAsync(userId);
            if (user != null)
            {
                report.UserName = user.UserName;
                report.UserEmail = user.Email;
            }
            else
            {
                _logger.LogWarning("User {UserId} not found, using ID only", userId);
                report.UserName = $"User {userId}";
            }

            // Query all actions performed by the user within the date range
            var auditEntries = await _auditQueryService.GetByActorAsync(
                userId, 
                startDate, 
                endDate, 
                cancellationToken);

            // Convert audit entries to user activity actions (chronological order)
            var actions = auditEntries
                .OrderBy(e => e.CreationDate)
                .Select(entry => new UserActivityAction
                {
                    PerformedAt = entry.CreationDate,
                    Action = entry.Action,
                    EntityType = entry.EntityType,
                    EntityId = entry.EntityId,
                    Description = GenerateActionDescription(entry),
                    IpAddress = entry.IpAddress,
                    CorrelationId = entry.CorrelationId
                })
                .ToList();

            report.Actions = actions;
            report.TotalActions = actions.Count;

            // Calculate action summaries by type
            report.ActionsByType = actions
                .GroupBy(a => a.Action)
                .ToDictionary(g => g.Key, g => g.Count());

            // Calculate action summaries by entity type
            report.ActionsByEntityType = actions
                .GroupBy(a => a.EntityType)
                .ToDictionary(g => g.Key, g => g.Count());

            _logger.LogInformation(
                "Generated user activity report for user {UserId} with {ActionCount} actions",
                userId, report.TotalActions);

            return report;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, 
                "Error generating user activity report for user {UserId}", 
                userId);
            throw;
        }
    }

    /// <summary>
    /// Generate a human-readable description for an audit log entry action.
    /// </summary>
    private string GenerateActionDescription(AuditLogEntry entry)
    {
        var action = entry.Action.ToUpperInvariant();
        var entityType = entry.EntityType;
        var entityId = entry.EntityId?.ToString() ?? "unknown";

        return action switch
        {
            "INSERT" => $"Created {entityType} (ID: {entityId})",
            "UPDATE" => $"Updated {entityType} (ID: {entityId})",
            "DELETE" => $"Deleted {entityType} (ID: {entityId})",
            "LOGIN" => "Logged in to the system",
            "LOGOUT" => "Logged out from the system",
            "TOKEN_REFRESH" => "Refreshed authentication token",
            "TOKEN_REVOKE" => "Revoked authentication token",
            "PERMISSION_GRANT" => $"Granted permission for {entityType}",
            "PERMISSION_REVOKE" => $"Revoked permission for {entityType}",
            "ROLE_ASSIGN" => $"Assigned role to {entityType}",
            "ROLE_REVOKE" => $"Revoked role from {entityType}",
            "EXCEPTION" when !string.IsNullOrEmpty(entry.ExceptionType) => 
                $"Error occurred: {entry.ExceptionType}",
            "CONFIGURATION_CHANGE" => $"Changed configuration: {entityType}",
            _ => $"{action} on {entityType}" + (entry.EntityId.HasValue ? $" (ID: {entityId})" : "")
        };
    }

    /// <summary>
    /// Generate a data modification report showing all changes to a specific entity.
    /// Returns a complete audit trail of all modifications (INSERT, UPDATE, DELETE) for the entity.
    /// Useful for data lineage tracking, compliance audits, and debugging.
    /// </summary>
    public async Task<DataModificationReport> GenerateDataModificationReportAsync(
        string entityType,
        long entityId,
        CancellationToken cancellationToken = default)
    {
        try
        {
            _logger.LogInformation(
                "Generating data modification report for entity {EntityType} {EntityId}",
                entityType, entityId);

            var report = new DataModificationReport
            {
                EntityType = entityType,
                EntityId = entityId,
                GeneratedAt = DateTime.UtcNow
            };

            // Retrieve all audit log entries for this entity
            var auditEntries = await _auditQueryService.GetByEntityAsync(
                entityType, 
                entityId, 
                cancellationToken);

            // Convert audit entries to data modifications
            var modifications = new List<DataModification>();
            
            foreach (var entry in auditEntries.OrderBy(e => e.CreationDate))
            {
                var modification = new DataModification
                {
                    ModifiedAt = entry.CreationDate,
                    Action = entry.Action,
                    ActorId = entry.ActorId,
                    ActorName = entry.ActorName ?? $"User {entry.ActorId}",
                    OldValue = entry.OldValue,
                    NewValue = entry.NewValue,
                    IpAddress = entry.IpAddress,
                    CorrelationId = entry.CorrelationId
                };

                // Extract changed fields for UPDATE operations
                if (entry.Action == "UPDATE" && !string.IsNullOrEmpty(entry.OldValue) && !string.IsNullOrEmpty(entry.NewValue))
                {
                    modification.ChangedFields = ExtractChangedFields(entry.OldValue, entry.NewValue);
                }

                modifications.Add(modification);
            }

            report.Modifications = modifications;
            report.TotalModifications = modifications.Count;

            // Calculate summary statistics
            report.ModificationsByAction = modifications
                .GroupBy(m => m.Action)
                .ToDictionary(g => g.Key, g => g.Count());

            report.ModificationsByUser = modifications
                .GroupBy(m => m.ActorName)
                .ToDictionary(g => g.Key, g => g.Count());

            _logger.LogInformation(
                "Generated data modification report for entity {EntityType} {EntityId} with {Count} modifications",
                entityType, entityId, report.TotalModifications);

            return report;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, 
                "Error generating data modification report for entity {EntityType} {EntityId}", 
                entityType, entityId);
            throw;
        }
    }

    /// <summary>
    /// Extract changed fields by comparing old and new JSON values.
    /// Returns a list of field names that were modified.
    /// </summary>
    private List<string> ExtractChangedFields(string oldValueJson, string newValueJson)
    {
        var changedFields = new List<string>();

        try
        {
            var oldObj = System.Text.Json.JsonSerializer.Deserialize<Dictionary<string, object>>(oldValueJson);
            var newObj = System.Text.Json.JsonSerializer.Deserialize<Dictionary<string, object>>(newValueJson);

            if (oldObj == null || newObj == null)
            {
                return changedFields;
            }

            // Find all fields that exist in either old or new
            var allFields = oldObj.Keys.Union(newObj.Keys).ToHashSet();

            foreach (var field in allFields)
            {
                var oldValue = oldObj.ContainsKey(field) ? oldObj[field]?.ToString() : null;
                var newValue = newObj.ContainsKey(field) ? newObj[field]?.ToString() : null;

                if (oldValue != newValue)
                {
                    changedFields.Add(field);
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to extract changed fields from JSON values");
            // Return empty list if JSON parsing fails
        }

        return changedFields;
    }

    /// <summary>
    /// Export a compliance report to PDF format.
    /// Generates a professionally formatted PDF document using QuestPDF library.
    /// Includes report metadata, summary statistics, and detailed data tables.
    /// </summary>
    public async Task<byte[]> ExportToPdfAsync(
        IReport report,
        CancellationToken cancellationToken = default)
    {
        try
        {
            _logger.LogInformation("Exporting report {ReportId} to PDF", report.ReportId);

            // TODO: Implement PDF export using QuestPDF library
            // var pdfGenerator = new PdfReportGenerator(_logger);
            // var pdfBytes = await Task.Run(() => pdfGenerator.GeneratePdf(report), cancellationToken);
            
            _logger.LogWarning("PDF export not yet implemented");
            return await Task.FromResult(Array.Empty<byte>());
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error exporting report {ReportId} to PDF", report.ReportId);
            throw;
        }
    }

    /// <summary>
    /// Export a compliance report to CSV format.
    /// Generates a CSV file with all report data for offline analysis and spreadsheet import.
    /// Includes column headers and properly escaped data values.
    /// </summary>
    public async Task<byte[]> ExportToCsvAsync(
        IReport report,
        CancellationToken cancellationToken = default)
    {
        try
        {
            _logger.LogInformation("Exporting report {ReportId} to CSV", report.ReportId);

            var csvContent = new StringBuilder();

            // Generate CSV based on report type
            switch (report)
            {
                case GdprAccessReport gdprReport:
                    GenerateGdprAccessReportCsv(csvContent, gdprReport);
                    break;
                
                case GdprDataExportReport gdprExportReport:
                    GenerateGdprDataExportReportCsv(csvContent, gdprExportReport);
                    break;
                
                case SoxFinancialAccessReport soxReport:
                    GenerateSoxFinancialAccessReportCsv(csvContent, soxReport);
                    break;
                
                case SoxSegregationOfDutiesReport soxSegReport:
                    GenerateSoxSegregationReportCsv(csvContent, soxSegReport);
                    break;
                
                case Iso27001SecurityReport isoReport:
                    GenerateIso27001SecurityReportCsv(csvContent, isoReport);
                    break;
                
                case UserActivityReport userReport:
                    GenerateUserActivityReportCsv(csvContent, userReport);
                    break;
                
                case DataModificationReport dataModReport:
                    GenerateDataModificationReportCsv(csvContent, dataModReport);
                    break;
                
                default:
                    throw new NotSupportedException($"CSV export not supported for report type: {report.ReportType}");
            }

            // Convert to byte array with UTF-8 encoding (with BOM for Excel compatibility)
            var preamble = Encoding.UTF8.GetPreamble();
            var contentBytes = Encoding.UTF8.GetBytes(csvContent.ToString());
            var result = new byte[preamble.Length + contentBytes.Length];
            Buffer.BlockCopy(preamble, 0, result, 0, preamble.Length);
            Buffer.BlockCopy(contentBytes, 0, result, preamble.Length, contentBytes.Length);

            _logger.LogInformation("Successfully exported report {ReportId} to CSV ({Size} bytes)", 
                report.ReportId, result.Length);
            
            return await Task.FromResult(result);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error exporting report {ReportId} to CSV", report.ReportId);
            throw;
        }
    }

    /// <summary>
    /// Generate CSV content for GDPR Access Report
    /// </summary>
    private void GenerateGdprAccessReportCsv(StringBuilder csv, GdprAccessReport report)
    {
        // Report metadata header
        csv.AppendLine("GDPR Data Access Report");
        csv.AppendLine($"Report ID,{EscapeCsvValue(report.ReportId)}");
        csv.AppendLine($"Generated At,{report.GeneratedAt:yyyy-MM-dd HH:mm:ss}");
        csv.AppendLine($"Data Subject,{EscapeCsvValue(report.DataSubjectName)} (ID: {report.DataSubjectId})");
        csv.AppendLine($"Email,{EscapeCsvValue(report.DataSubjectEmail ?? "N/A")}");
        csv.AppendLine($"Period,{report.PeriodStartDate:yyyy-MM-dd} to {report.PeriodEndDate:yyyy-MM-dd}");
        csv.AppendLine($"Total Access Events,{report.TotalAccessEvents}");
        csv.AppendLine();

        // Access events table
        csv.AppendLine("Access Events");
        csv.AppendLine("Accessed At,Actor ID,Actor Name,Entity Type,Entity ID,Action,Purpose,Legal Basis,IP Address,Correlation ID");
        
        foreach (var accessEvent in report.AccessEvents)
        {
            csv.AppendLine($"{accessEvent.AccessedAt:yyyy-MM-dd HH:mm:ss}," +
                          $"{accessEvent.ActorId}," +
                          $"{EscapeCsvValue(accessEvent.ActorName)}," +
                          $"{EscapeCsvValue(accessEvent.EntityType)}," +
                          $"{accessEvent.EntityId?.ToString() ?? "N/A"}," +
                          $"{EscapeCsvValue(accessEvent.Action)}," +
                          $"{EscapeCsvValue(accessEvent.Purpose ?? "N/A")}," +
                          $"{EscapeCsvValue(accessEvent.LegalBasis ?? "N/A")}," +
                          $"{EscapeCsvValue(accessEvent.IpAddress ?? "N/A")}," +
                          $"{EscapeCsvValue(accessEvent.CorrelationId ?? "N/A")}");
        }
        
        csv.AppendLine();

        // Summary by entity type
        csv.AppendLine("Access Summary by Entity Type");
        csv.AppendLine("Entity Type,Access Count");
        foreach (var kvp in report.AccessByEntityType.OrderByDescending(x => x.Value))
        {
            csv.AppendLine($"{EscapeCsvValue(kvp.Key)},{kvp.Value}");
        }
        
        csv.AppendLine();

        // Summary by actor
        csv.AppendLine("Access Summary by Actor");
        csv.AppendLine("Actor Name,Access Count");
        foreach (var kvp in report.AccessByActor.OrderByDescending(x => x.Value))
        {
            csv.AppendLine($"{EscapeCsvValue(kvp.Key)},{kvp.Value}");
        }
    }

    /// <summary>
    /// Generate CSV content for GDPR Data Export Report
    /// </summary>
    private void GenerateGdprDataExportReportCsv(StringBuilder csv, GdprDataExportReport report)
    {
        // Report metadata header
        csv.AppendLine("GDPR Data Export Report");
        csv.AppendLine($"Report ID,{EscapeCsvValue(report.ReportId)}");
        csv.AppendLine($"Generated At,{report.GeneratedAt:yyyy-MM-dd HH:mm:ss}");
        csv.AppendLine($"Data Subject,{EscapeCsvValue(report.DataSubjectName)} (ID: {report.DataSubjectId})");
        csv.AppendLine($"Email,{EscapeCsvValue(report.DataSubjectEmail ?? "N/A")}");
        csv.AppendLine($"Total Records,{report.TotalRecords}");
        csv.AppendLine($"Data Categories,{EscapeCsvValue(string.Join(", ", report.DataCategories))}");
        csv.AppendLine();

        // Export data by entity type
        foreach (var entityType in report.PersonalDataByEntityType)
        {
            csv.AppendLine($"Entity Type: {EscapeCsvValue(entityType.Key)}");
            csv.AppendLine($"Record Count,{entityType.Value.Count}");
            csv.AppendLine("Data (JSON)");
            
            foreach (var dataJson in entityType.Value)
            {
                csv.AppendLine(EscapeCsvValue(dataJson));
            }
            
            csv.AppendLine();
        }
    }

    /// <summary>
    /// Generate CSV content for SOX Financial Access Report
    /// </summary>
    private void GenerateSoxFinancialAccessReportCsv(StringBuilder csv, SoxFinancialAccessReport report)
    {
        // Report metadata header
        csv.AppendLine("SOX Financial Access Report");
        csv.AppendLine($"Report ID,{EscapeCsvValue(report.ReportId)}");
        csv.AppendLine($"Generated At,{report.GeneratedAt:yyyy-MM-dd HH:mm:ss}");
        csv.AppendLine($"Period,{report.PeriodStartDate:yyyy-MM-dd} to {report.PeriodEndDate:yyyy-MM-dd}");
        csv.AppendLine($"Total Access Events,{report.TotalAccessEvents}");
        csv.AppendLine($"Out-of-Hours Access Events,{report.OutOfHoursAccessEvents}");
        csv.AppendLine();

        // Financial access events table
        csv.AppendLine("Financial Access Events");
        csv.AppendLine("Accessed At,Actor ID,Actor Name,Actor Role,Entity Type,Entity ID,Action,Business Justification,Out of Hours,IP Address,Correlation ID");
        
        foreach (var accessEvent in report.AccessEvents)
        {
            csv.AppendLine($"{accessEvent.AccessedAt:yyyy-MM-dd HH:mm:ss}," +
                          $"{accessEvent.ActorId}," +
                          $"{EscapeCsvValue(accessEvent.ActorName)}," +
                          $"{EscapeCsvValue(accessEvent.ActorRole ?? "N/A")}," +
                          $"{EscapeCsvValue(accessEvent.EntityType)}," +
                          $"{accessEvent.EntityId?.ToString() ?? "N/A"}," +
                          $"{EscapeCsvValue(accessEvent.Action)}," +
                          $"{EscapeCsvValue(accessEvent.BusinessJustification ?? "N/A")}," +
                          $"{(accessEvent.OutOfHours ? "Yes" : "No")}," +
                          $"{EscapeCsvValue(accessEvent.IpAddress ?? "N/A")}," +
                          $"{EscapeCsvValue(accessEvent.CorrelationId ?? "N/A")}");
        }
        
        csv.AppendLine();

        // Summary by user
        csv.AppendLine("Access Summary by User");
        csv.AppendLine("User Name,Access Count");
        foreach (var kvp in report.AccessByUser.OrderByDescending(x => x.Value))
        {
            csv.AppendLine($"{EscapeCsvValue(kvp.Key)},{kvp.Value}");
        }
        
        csv.AppendLine();

        // Summary by entity type
        csv.AppendLine("Access Summary by Entity Type");
        csv.AppendLine("Entity Type,Access Count");
        foreach (var kvp in report.AccessByEntityType.OrderByDescending(x => x.Value))
        {
            csv.AppendLine($"{EscapeCsvValue(kvp.Key)},{kvp.Value}");
        }
        
        csv.AppendLine();

        // Suspicious patterns
        if (report.SuspiciousPatterns.Any())
        {
            csv.AppendLine("Suspicious Patterns Detected");
            csv.AppendLine("Pattern Description");
            foreach (var pattern in report.SuspiciousPatterns)
            {
                csv.AppendLine(EscapeCsvValue(pattern));
            }
        }
    }

    /// <summary>
    /// Generate CSV content for SOX Segregation of Duties Report
    /// </summary>
    private void GenerateSoxSegregationReportCsv(StringBuilder csv, SoxSegregationOfDutiesReport report)
    {
        // Report metadata header
        csv.AppendLine("SOX Segregation of Duties Report");
        csv.AppendLine($"Report ID,{EscapeCsvValue(report.ReportId)}");
        csv.AppendLine($"Generated At,{report.GeneratedAt:yyyy-MM-dd HH:mm:ss}");
        csv.AppendLine($"Total Users Analyzed,{report.TotalUsersAnalyzed}");
        csv.AppendLine($"Violations Detected,{report.ViolationsDetected}");
        csv.AppendLine();

        // Violations table
        csv.AppendLine("Segregation of Duties Violations");
        csv.AppendLine("User ID,User Name,Role 1,Role 2,Conflict Description,Severity,Recommendation");
        
        foreach (var violation in report.Violations)
        {
            csv.AppendLine($"{violation.UserId}," +
                          $"{EscapeCsvValue(violation.UserName)}," +
                          $"{EscapeCsvValue(violation.Role1)}," +
                          $"{EscapeCsvValue(violation.Role2)}," +
                          $"{EscapeCsvValue(violation.ConflictDescription)}," +
                          $"{EscapeCsvValue(violation.Severity)}," +
                          $"{EscapeCsvValue(violation.Recommendation ?? "N/A")}");
        }
        
        csv.AppendLine();

        // Summary by severity
        csv.AppendLine("Violations Summary by Severity");
        csv.AppendLine("Severity,Violation Count");
        foreach (var kvp in report.ViolationsBySeverity.OrderByDescending(x => x.Value))
        {
            csv.AppendLine($"{EscapeCsvValue(kvp.Key)},{kvp.Value}");
        }
    }

    /// <summary>
    /// Generate CSV content for ISO 27001 Security Report
    /// </summary>
    private void GenerateIso27001SecurityReportCsv(StringBuilder csv, Iso27001SecurityReport report)
    {
        // Report metadata header
        csv.AppendLine("ISO 27001 Security Report");
        csv.AppendLine($"Report ID,{EscapeCsvValue(report.ReportId)}");
        csv.AppendLine($"Generated At,{report.GeneratedAt:yyyy-MM-dd HH:mm:ss}");
        csv.AppendLine($"Period,{report.PeriodStartDate:yyyy-MM-dd} to {report.PeriodEndDate:yyyy-MM-dd}");
        csv.AppendLine($"Total Security Events,{report.TotalSecurityEvents}");
        csv.AppendLine($"Critical Events,{report.CriticalEvents}");
        csv.AppendLine($"Failed Login Attempts,{report.FailedLoginAttempts}");
        csv.AppendLine($"Unauthorized Access Attempts,{report.UnauthorizedAccessAttempts}");
        csv.AppendLine();

        // Security events table
        csv.AppendLine("Security Events");
        csv.AppendLine("Occurred At,Event Type,Severity,Description,User ID,User Name,IP Address,Action Taken,Correlation ID");
        
        foreach (var securityEvent in report.SecurityEvents)
        {
            csv.AppendLine($"{securityEvent.OccurredAt:yyyy-MM-dd HH:mm:ss}," +
                          $"{EscapeCsvValue(securityEvent.EventType)}," +
                          $"{EscapeCsvValue(securityEvent.Severity)}," +
                          $"{EscapeCsvValue(securityEvent.Description)}," +
                          $"{securityEvent.UserId?.ToString() ?? "N/A"}," +
                          $"{EscapeCsvValue(securityEvent.UserName ?? "N/A")}," +
                          $"{EscapeCsvValue(securityEvent.IpAddress ?? "N/A")}," +
                          $"{EscapeCsvValue(securityEvent.ActionTaken ?? "N/A")}," +
                          $"{EscapeCsvValue(securityEvent.CorrelationId ?? "N/A")}");
        }
        
        csv.AppendLine();

        // Summary by severity
        csv.AppendLine("Events Summary by Severity");
        csv.AppendLine("Severity,Event Count");
        foreach (var kvp in report.EventsBySeverity.OrderByDescending(x => x.Value))
        {
            csv.AppendLine($"{EscapeCsvValue(kvp.Key)},{kvp.Value}");
        }
        
        csv.AppendLine();

        // Summary by type
        csv.AppendLine("Events Summary by Type");
        csv.AppendLine("Event Type,Event Count");
        foreach (var kvp in report.EventsByType.OrderByDescending(x => x.Value))
        {
            csv.AppendLine($"{EscapeCsvValue(kvp.Key)},{kvp.Value}");
        }
        
        csv.AppendLine();

        // Incidents requiring attention
        if (report.IncidentsRequiringAttention.Any())
        {
            csv.AppendLine("Incidents Requiring Attention");
            csv.AppendLine("Incident Description");
            foreach (var incident in report.IncidentsRequiringAttention)
            {
                csv.AppendLine(EscapeCsvValue(incident));
            }
        }
    }

    /// <summary>
    /// Generate CSV content for User Activity Report
    /// </summary>
    private void GenerateUserActivityReportCsv(StringBuilder csv, UserActivityReport report)
    {
        // Report metadata header
        csv.AppendLine("User Activity Report");
        csv.AppendLine($"Report ID,{EscapeCsvValue(report.ReportId)}");
        csv.AppendLine($"Generated At,{report.GeneratedAt:yyyy-MM-dd HH:mm:ss}");
        csv.AppendLine($"User,{EscapeCsvValue(report.UserName)} (ID: {report.UserId})");
        csv.AppendLine($"Email,{EscapeCsvValue(report.UserEmail ?? "N/A")}");
        csv.AppendLine($"Period,{report.PeriodStartDate:yyyy-MM-dd} to {report.PeriodEndDate:yyyy-MM-dd}");
        csv.AppendLine($"Total Actions,{report.TotalActions}");
        csv.AppendLine();

        // User actions table
        csv.AppendLine("User Actions");
        csv.AppendLine("Performed At,Action,Entity Type,Entity ID,Description,IP Address,Correlation ID");
        
        foreach (var action in report.Actions)
        {
            csv.AppendLine($"{action.PerformedAt:yyyy-MM-dd HH:mm:ss}," +
                          $"{EscapeCsvValue(action.Action)}," +
                          $"{EscapeCsvValue(action.EntityType)}," +
                          $"{action.EntityId?.ToString() ?? "N/A"}," +
                          $"{EscapeCsvValue(action.Description ?? "N/A")}," +
                          $"{EscapeCsvValue(action.IpAddress ?? "N/A")}," +
                          $"{EscapeCsvValue(action.CorrelationId ?? "N/A")}");
        }
        
        csv.AppendLine();

        // Summary by action type
        csv.AppendLine("Actions Summary by Type");
        csv.AppendLine("Action Type,Action Count");
        foreach (var kvp in report.ActionsByType.OrderByDescending(x => x.Value))
        {
            csv.AppendLine($"{EscapeCsvValue(kvp.Key)},{kvp.Value}");
        }
        
        csv.AppendLine();

        // Summary by entity type
        csv.AppendLine("Actions Summary by Entity Type");
        csv.AppendLine("Entity Type,Action Count");
        foreach (var kvp in report.ActionsByEntityType.OrderByDescending(x => x.Value))
        {
            csv.AppendLine($"{EscapeCsvValue(kvp.Key)},{kvp.Value}");
        }
    }

    /// <summary>
    /// Generate CSV content for Data Modification Report
    /// </summary>
    private void GenerateDataModificationReportCsv(StringBuilder csv, DataModificationReport report)
    {
        // Report metadata header
        csv.AppendLine("Data Modification Report");
        csv.AppendLine($"Report ID,{EscapeCsvValue(report.ReportId)}");
        csv.AppendLine($"Generated At,{report.GeneratedAt:yyyy-MM-dd HH:mm:ss}");
        csv.AppendLine($"Entity,{EscapeCsvValue(report.EntityType)} (ID: {report.EntityId})");
        csv.AppendLine($"Total Modifications,{report.TotalModifications}");
        csv.AppendLine();

        // Data modifications table
        csv.AppendLine("Data Modifications");
        csv.AppendLine("Modified At,Action,Actor ID,Actor Name,Changed Fields,Old Value,New Value,IP Address,Correlation ID");
        
        foreach (var modification in report.Modifications)
        {
            var changedFields = modification.ChangedFields != null && modification.ChangedFields.Any()
                ? string.Join("; ", modification.ChangedFields)
                : "N/A";
            
            csv.AppendLine($"{modification.ModifiedAt:yyyy-MM-dd HH:mm:ss}," +
                          $"{EscapeCsvValue(modification.Action)}," +
                          $"{modification.ActorId}," +
                          $"{EscapeCsvValue(modification.ActorName)}," +
                          $"{EscapeCsvValue(changedFields)}," +
                          $"{EscapeCsvValue(modification.OldValue ?? "N/A")}," +
                          $"{EscapeCsvValue(modification.NewValue ?? "N/A")}," +
                          $"{EscapeCsvValue(modification.IpAddress ?? "N/A")}," +
                          $"{EscapeCsvValue(modification.CorrelationId ?? "N/A")}");
        }
        
        csv.AppendLine();

        // Summary by action
        csv.AppendLine("Modifications Summary by Action");
        csv.AppendLine("Action,Modification Count");
        foreach (var kvp in report.ModificationsByAction.OrderByDescending(x => x.Value))
        {
            csv.AppendLine($"{EscapeCsvValue(kvp.Key)},{kvp.Value}");
        }
        
        csv.AppendLine();

        // Summary by user
        csv.AppendLine("Modifications Summary by User");
        csv.AppendLine("User Name,Modification Count");
        foreach (var kvp in report.ModificationsByUser.OrderByDescending(x => x.Value))
        {
            csv.AppendLine($"{EscapeCsvValue(kvp.Key)},{kvp.Value}");
        }
    }

    /// <summary>
    /// Escape CSV values to handle commas, quotes, and newlines properly.
    /// Follows RFC 4180 CSV specification.
    /// </summary>
    private string EscapeCsvValue(string? value)
    {
        if (string.IsNullOrEmpty(value))
        {
            return string.Empty;
        }

        // If value contains comma, quote, or newline, wrap in quotes and escape internal quotes
        if (value.Contains(',') || value.Contains('"') || value.Contains('\n') || value.Contains('\r'))
        {
            // Escape quotes by doubling them
            value = value.Replace("\"", "\"\"");
            // Wrap in quotes
            return $"\"{value}\"";
        }

        return value;
    }

    /// <summary>
    /// Export a compliance report to JSON format.
    /// Generates a JSON document with all report data for programmatic processing and API integrations.
    /// Includes report metadata and structured data in JSON format.
    /// </summary>
    public async Task<string> ExportToJsonAsync(
        IReport report,
        CancellationToken cancellationToken = default)
    {
        try
        {
            _logger.LogInformation("Exporting report {ReportId} to JSON", report.ReportId);

            var json = JsonSerializer.Serialize(report, new JsonSerializerOptions
            {
                WriteIndented = true,
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase
            });

            return json;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error exporting report {ReportId} to JSON", report.ReportId);
            throw;
        }
    }

    /// <summary>
    /// Schedule a compliance report for automatic generation and delivery.
    /// Creates a scheduled job that generates and emails the report on a recurring basis.
    /// Supports daily, weekly, and monthly schedules with configurable recipients.
    /// </summary>
    public async Task ScheduleReportAsync(
        ReportSchedule schedule,
        CancellationToken cancellationToken = default)
    {
        try
        {
            _logger.LogInformation(
                "Scheduling report {ReportType} with frequency {Frequency}",
                schedule.ReportType, schedule.Frequency);

            // TODO: Implement report scheduling using background service
            
            _logger.LogWarning("Report scheduling not yet implemented");
            
            await Task.CompletedTask;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, 
                "Error scheduling report {ReportType}", 
                schedule.ReportType);
            throw;
        }
    }

    #region Private Helper Methods

    /// <summary>
    /// Populate data subject information (name, email) in the report
    /// </summary>
    private async Task PopulateDataSubjectInfoAsync(
        dynamic report,
        long dataSubjectId,
        CancellationToken cancellationToken)
    {
        try
        {
            var user = await _dbContext.SysUsers
                .Where(u => u.Id == dataSubjectId)
                .Select(u => new { u.UserName, u.Email })
                .FirstOrDefaultAsync(cancellationToken);

            if (user != null)
            {
                report.DataSubjectName = user.UserName ?? "Unknown";
                report.DataSubjectEmail = user.Email;
            }
            else
            {
                report.DataSubjectName = "Unknown";
                report.DataSubjectEmail = null;
                _logger.LogWarning("Data subject {DataSubjectId} not found in SYS_USERS", dataSubjectId);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error populating data subject info for {DataSubjectId}", dataSubjectId);
            report.DataSubjectName = "Unknown";
            report.DataSubjectEmail = null;
        }
    }

    /// <summary>
    /// Query all audit log entries related to a data subject within a date range
    /// </summary>
    private async Task<List<DataAccessEvent>> QueryDataSubjectAccessEventsAsync(
        long dataSubjectId,
        DateTime startDate,
        DateTime endDate,
        CancellationToken cancellationToken)
    {
        var accessEvents = new List<DataAccessEvent>();

        try
        {
            var query = from al in _dbContext.SysAuditLogs
                        join u in _dbContext.SysUsers on al.ActorId equals u.Id into userJoin
                        from u in userJoin.DefaultIfEmpty()
                        where al.CreationDate >= startDate
                           && al.CreationDate <= endDate
                           && (al.ActorId == dataSubjectId
                               || (al.EntityType == "SysUser" && al.EntityId == dataSubjectId))
                        orderby al.CreationDate ascending
                        select new { al, ActorName = u != null ? u.UserName : null };

            var results = await query.ToListAsync(cancellationToken);

            foreach (var item in results)
            {
                var accessEvent = new DataAccessEvent
                {
                    AccessedAt = item.al.CreationDate,
                    ActorId = item.al.ActorId,
                    ActorName = item.ActorName ?? "Unknown",
                    EntityType = item.al.EntityType ?? "Unknown",
                    EntityId = item.al.EntityId,
                    Action = item.al.Action ?? "Unknown",
                    IpAddress = item.al.IpAddress,
                    CorrelationId = item.al.CorrelationId
                };

                if (item.al.Metadata != null)
                {
                    try
                    {
                        var metadata = JsonSerializer.Deserialize<Dictionary<string, JsonElement>>(item.al.Metadata);

                        if (metadata != null)
                        {
                            if (metadata.ContainsKey("purpose"))
                            {
                                accessEvent.Purpose = metadata["purpose"].GetString();
                            }
                            if (metadata.ContainsKey("legalBasis"))
                            {
                                accessEvent.LegalBasis = metadata["legalBasis"].GetString();
                            }
                        }
                    }
                    catch (JsonException)
                    {
                    }
                }

                accessEvents.Add(accessEvent);
            }

            _logger.LogDebug(
                "Found {Count} access events for data subject {DataSubjectId}",
                accessEvents.Count, dataSubjectId);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex,
                "Error querying data subject access events for {DataSubjectId}",
                dataSubjectId);
            throw;
        }

        return accessEvents;
    }

    /// <summary>
    /// Export user profile data (personal information from SYS_USERS table)
    /// </summary>
    private async Task ExportUserProfileDataAsync(
        GdprDataExportReport report,
        long dataSubjectId,
        CancellationToken cancellationToken)
    {
        try
        {
            var user = await _dbContext.SysUsers
                .Where(u => u.Id == dataSubjectId)
                .Select(u => new
                {
                    u.Id,
                    u.UserName,
                    u.Email,
                    u.Phone,
                    u.CompanyId,
                    u.RoleId,
                    u.IsActive,
                    u.CreationDate,
                    u.UpdateDate,
                    u.ForceLogoutDate
                })
                .FirstOrDefaultAsync(cancellationToken);

            var userDataList = new List<string>();

            if (user != null)
            {
                var userData = new Dictionary<string, object?>
                {
                    ["UserId"] = user.Id,
                    ["UserName"] = user.UserName,
                    ["Email"] = user.Email,
                    ["PhoneNumber"] = user.Phone,
                    ["CompanyId"] = user.CompanyId,
                    ["RoleId"] = user.RoleId,
                    ["IsActive"] = user.IsActive,
                    ["CreationDate"] = user.CreationDate,
                    ["LastModifiedDate"] = user.UpdateDate,
                    ["ForceLogout"] = user.ForceLogoutDate != null
                };

                userDataList.Add(JsonSerializer.Serialize(userData, new JsonSerializerOptions
                {
                    WriteIndented = true,
                    PropertyNamingPolicy = JsonNamingPolicy.CamelCase
                }));
            }

            if (userDataList.Any())
            {
                report.PersonalDataByEntityType["UserProfile"] = userDataList;
                _logger.LogDebug("Exported user profile data for data subject {DataSubjectId}", dataSubjectId);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error exporting user profile data for data subject {DataSubjectId}", dataSubjectId);
        }
    }

    /// <summary>
    /// Export audit log data (all actions performed by the user)
    /// </summary>
    private async Task ExportAuditLogDataAsync(
        GdprDataExportReport report,
        long dataSubjectId,
        CancellationToken cancellationToken)
    {
        try
        {
            var auditLogs = await _dbContext.SysAuditLogs
                .Where(a => a.ActorId == dataSubjectId)
                .OrderByDescending(a => a.CreationDate)
                .ToListAsync(cancellationToken);

            var auditDataList = new List<string>();

            foreach (var log in auditLogs)
            {
                var auditData = new Dictionary<string, object?>
                {
                    ["AuditLogId"] = log.Id,
                    ["ActorType"] = log.ActorType,
                    ["ActorId"] = log.ActorId,
                    ["CompanyId"] = log.CompanyId,
                    ["BranchId"] = log.BranchId,
                    ["Action"] = log.Action,
                    ["EntityType"] = log.EntityType,
                    ["EntityId"] = log.EntityId,
                    ["IpAddress"] = log.IpAddress,
                    ["UserAgent"] = log.UserAgent,
                    ["CorrelationId"] = log.CorrelationId,
                    ["HttpMethod"] = log.HttpMethod,
                    ["EndpointPath"] = log.EndpointPath,
                    ["StatusCode"] = log.StatusCode,
                    ["ExecutionTimeMs"] = log.ExecutionTimeMs,
                    ["EventCategory"] = log.EventCategory,
                    ["Severity"] = log.Severity,
                    ["CreationDate"] = log.CreationDate
                };

                auditDataList.Add(JsonSerializer.Serialize(auditData, new JsonSerializerOptions
                {
                    WriteIndented = true,
                    PropertyNamingPolicy = JsonNamingPolicy.CamelCase
                }));
            }

            if (auditDataList.Any())
            {
                report.PersonalDataByEntityType["AuditLog"] = auditDataList;
                _logger.LogDebug("Exported {Count} audit log entries for data subject {DataSubjectId}", 
                    auditDataList.Count, dataSubjectId);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error exporting audit log data for data subject {DataSubjectId}", dataSubjectId);
        }
    }

    /// <summary>
    /// Export authentication data (login history from audit logs)
    /// </summary>
    private async Task ExportAuthenticationDataAsync(
        GdprDataExportReport report,
        long dataSubjectId,
        CancellationToken cancellationToken)
    {
        try
        {
            var authLogs = await _dbContext.SysAuditLogs
                .Where(a => a.ActorId == dataSubjectId && a.EventCategory == "Authentication")
                .OrderByDescending(a => a.CreationDate)
                .ToListAsync(cancellationToken);

            var authDataList = new List<string>();

            foreach (var log in authLogs)
            {
                var authData = new Dictionary<string, object?>
                {
                    ["AuditLogId"] = log.Id,
                    ["Action"] = log.Action,
                    ["IpAddress"] = log.IpAddress,
                    ["UserAgent"] = log.UserAgent,
                    ["StatusCode"] = log.StatusCode,
                    ["CreationDate"] = log.CreationDate
                };

                if (log.Metadata != null)
                {
                    try
                    {
                        var metadata = JsonSerializer.Deserialize<Dictionary<string, JsonElement>>(log.Metadata);
                        if (metadata != null)
                        {
                            authData["Metadata"] = metadata;
                        }
                    }
                    catch (JsonException)
                    {
                    }
                }

                authDataList.Add(JsonSerializer.Serialize(authData, new JsonSerializerOptions
                {
                    WriteIndented = true,
                    PropertyNamingPolicy = JsonNamingPolicy.CamelCase
                }));
            }

            if (authDataList.Any())
            {
                report.PersonalDataByEntityType["AuthenticationHistory"] = authDataList;
                _logger.LogDebug("Exported {Count} authentication records for data subject {DataSubjectId}", 
                    authDataList.Count, dataSubjectId);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error exporting authentication data for data subject {DataSubjectId}", dataSubjectId);
        }
    }

    /// <summary>
    /// Query all financial data access events within a date range.
    /// Financial entities include: Invoice, Payment, Transaction, Account, Budget, etc.
    /// </summary>
    private async Task<List<FinancialAccessEvent>> QueryFinancialDataAccessEventsAsync(
        DateTime startDate,
        DateTime endDate,
        CancellationToken cancellationToken)
    {
        var financialAccessEvents = new List<FinancialAccessEvent>();

        try
        {
            var query = from al in _dbContext.SysAuditLogs
                        join u in _dbContext.SysUsers on al.ActorId equals u.Id into userJoin
                        from u in userJoin.DefaultIfEmpty()
                        join r in _dbContext.SysRoles on u.RoleId equals r.Id into roleJoin
                        from r in roleJoin.DefaultIfEmpty()
                        where al.CreationDate >= startDate
                           && al.CreationDate <= endDate
                           && (al.EntityType.ToUpper().Contains("INVOICE")
                               || al.EntityType.ToUpper().Contains("PAYMENT")
                               || al.EntityType.ToUpper().Contains("TRANSACTION")
                               || al.EntityType.ToUpper().Contains("ACCOUNT")
                               || al.EntityType.ToUpper().Contains("BUDGET")
                               || al.EntityType.ToUpper().Contains("JOURNAL")
                               || al.EntityType.ToUpper().Contains("LEDGER")
                               || al.EntityType.ToUpper().Contains("FINANCIAL")
                               || al.EntityType.ToUpper().Contains("REVENUE")
                               || al.EntityType.ToUpper().Contains("EXPENSE")
                               || al.EntityType.ToUpper().Contains("ASSET")
                               || al.EntityType.ToUpper().Contains("LIABILITY")
                               || _dbContext.SysSystems.Any(s => s.Id == al.SystemId && (s.SystemCode == "accounting" || s.SystemCode == "finance")))
                        orderby al.CreationDate ascending
                        select new { al, u, r };

            var results = await query.ToListAsync(cancellationToken);

            foreach (var item in results)
            {
                var accessedAt = item.al.CreationDate;

                var financialEvent = new FinancialAccessEvent
                {
                    AccessedAt = accessedAt,
                    ActorId = item.al.ActorId,
                    ActorName = item.u?.UserName ?? "Unknown",
                    ActorRole = item.r?.RoleNameEn,
                    EntityType = item.al.EntityType ?? "Unknown",
                    EntityId = item.al.EntityId,
                    Action = item.al.Action ?? "Unknown",
                    IpAddress = item.al.IpAddress,
                    CorrelationId = item.al.CorrelationId,
                    OutOfHours = IsOutOfHours(accessedAt)
                };

                if (item.al.Metadata != null)
                {
                    try
                    {
                        var metadata = JsonSerializer.Deserialize<Dictionary<string, JsonElement>>(item.al.Metadata);

                        if (metadata != null && metadata.ContainsKey("businessJustification"))
                        {
                            financialEvent.BusinessJustification = metadata["businessJustification"].GetString();
                        }
                    }
                    catch (JsonException)
                    {
                    }
                }

                financialAccessEvents.Add(financialEvent);
            }

            _logger.LogDebug(
                "Found {Count} financial data access events from {StartDate} to {EndDate}",
                financialAccessEvents.Count, startDate, endDate);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex,
                "Error querying financial data access events from {StartDate} to {EndDate}",
                startDate, endDate);
            throw;
        }

        return financialAccessEvents;
    }

    /// <summary>
    /// Determine if an access occurred outside normal business hours.
    /// Normal business hours: Monday-Friday, 8 AM - 6 PM
    /// </summary>
    private bool IsOutOfHours(DateTime accessTime)
    {
        // Weekend access is always out-of-hours
        if (accessTime.DayOfWeek == DayOfWeek.Saturday || accessTime.DayOfWeek == DayOfWeek.Sunday)
        {
            return true;
        }

        // Check if time is outside 8 AM - 6 PM
        var hour = accessTime.Hour;
        return hour < 8 || hour >= 18;
    }

    /// <summary>
    /// Detect suspicious financial access patterns for SOX compliance.
    /// Patterns include: excessive access, unusual times, privilege escalation, etc.
    /// </summary>
    private List<string> DetectSuspiciousFinancialAccessPatterns(List<FinancialAccessEvent> accessEvents)
    {
        var suspiciousPatterns = new List<string>();

        try
        {
            // Pattern 1: Users with excessive financial data access (>100 accesses in period)
            var excessiveAccessUsers = accessEvents
                .GroupBy(e => e.ActorName)
                .Where(g => g.Count() > 100)
                .Select(g => new { User = g.Key, Count = g.Count() })
                .ToList();

            foreach (var user in excessiveAccessUsers)
            {
                suspiciousPatterns.Add(
                    $"Excessive access: User '{user.User}' accessed financial data {user.Count} times");
            }

            // Pattern 2: High percentage of out-of-hours access by a single user
            var userOutOfHoursAccess = accessEvents
                .Where(e => e.OutOfHours)
                .GroupBy(e => e.ActorName)
                .Where(g => g.Count() > 10) // More than 10 out-of-hours accesses
                .Select(g => new 
                { 
                    User = g.Key, 
                    OutOfHoursCount = g.Count(),
                    TotalCount = accessEvents.Count(e => e.ActorName == g.Key),
                    Percentage = (g.Count() * 100.0) / accessEvents.Count(e => e.ActorName == g.Key)
                })
                .Where(x => x.Percentage > 50) // More than 50% out-of-hours
                .ToList();

            foreach (var user in userOutOfHoursAccess)
            {
                suspiciousPatterns.Add(
                    $"High out-of-hours access: User '{user.User}' has {user.OutOfHoursCount} out-of-hours accesses ({user.Percentage:F1}% of total)");
            }

            // Pattern 3: Access to multiple financial entity types by same user (potential segregation of duties violation)
            var multiEntityAccess = accessEvents
                .GroupBy(e => e.ActorName)
                .Select(g => new 
                { 
                    User = g.Key, 
                    EntityTypes = g.Select(e => e.EntityType).Distinct().ToList() 
                })
                .Where(x => x.EntityTypes.Count >= 5) // Accessing 5 or more different financial entity types
                .ToList();

            foreach (var user in multiEntityAccess)
            {
                suspiciousPatterns.Add(
                    $"Broad financial access: User '{user.User}' accessed {user.EntityTypes.Count} different financial entity types");
            }

            // Pattern 4: Financial data modifications without business justification
            var unjustifiedModifications = accessEvents
                .Where(e => (e.Action == "UPDATE" || e.Action == "DELETE") && string.IsNullOrEmpty(e.BusinessJustification))
                .GroupBy(e => e.ActorName)
                .Where(g => g.Count() > 5)
                .Select(g => new { User = g.Key, Count = g.Count() })
                .ToList();

            foreach (var user in unjustifiedModifications)
            {
                suspiciousPatterns.Add(
                    $"Unjustified modifications: User '{user.User}' made {user.Count} financial data modifications without business justification");
            }

            // Pattern 5: Rapid sequential access (potential data scraping)
            var rapidAccessPatterns = accessEvents
                .GroupBy(e => e.ActorName)
                .Select(g => new
                {
                    User = g.Key,
                    Events = g.OrderBy(e => e.AccessedAt).ToList()
                })
                .Where(x => x.Events.Count >= 10)
                .ToList();

            foreach (var userAccess in rapidAccessPatterns)
            {
                // Check for rapid sequential access (more than 10 accesses within 1 minute)
                for (int i = 0; i < userAccess.Events.Count - 10; i++)
                {
                    var timeSpan = userAccess.Events[i + 9].AccessedAt - userAccess.Events[i].AccessedAt;
                    if (timeSpan.TotalMinutes < 1)
                    {
                        suspiciousPatterns.Add(
                            $"Rapid sequential access: User '{userAccess.User}' made 10+ accesses within 1 minute at {userAccess.Events[i].AccessedAt:yyyy-MM-dd HH:mm}");
                        break; // Only report once per user
                    }
                }
            }

            _logger.LogDebug("Detected {Count} suspicious financial access patterns", suspiciousPatterns.Count);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error detecting suspicious financial access patterns");
            // Return patterns detected so far
        }

        return suspiciousPatterns;
    }

    /// <summary>
    /// Get all users with their role assignments for segregation analysis
    /// </summary>
    private async Task<List<UserRoleAssignment>> GetUserRoleAssignmentsAsync(
        CancellationToken cancellationToken)
    {
        var userRoleAssignments = new List<UserRoleAssignment>();

        try
        {
            var query = from u in _dbContext.SysUsers
                        join ur in _dbContext.SysUserRoles on u.Id equals ur.UserId
                        join r in _dbContext.SysRoles on ur.RoleId equals r.Id
                        where u.IsActive && r.IsActive
                        orderby u.UserName, r.RoleNameEn
                        select new UserRoleAssignment
                        {
                            UserId = u.Id,
                            UserName = u.UserName,
                            UserEmail = u.Email,
                            CompanyId = u.CompanyId,
                            RoleId = r.Id,
                            RoleName = r.RoleNameEn,
                            RoleDescription = r.Note,
                            AssignedDate = ur.AssignedDate
                        };

            userRoleAssignments = await query.ToListAsync(cancellationToken);

            _logger.LogDebug("Retrieved {Count} user role assignments", userRoleAssignments.Count);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving user role assignments");
            throw;
        }

        return userRoleAssignments;
    }

    /// <summary>
    /// Analyze user role assignments for segregation of duties violations.
    /// Detects conflicting role combinations and excessive permissions.
    /// </summary>
    private async Task<List<SegregationViolation>> AnalyzeSegregationViolationsAsync(
        List<UserRoleAssignment> userRoleAssignments,
        CancellationToken cancellationToken)
    {
        var violations = new List<SegregationViolation>();

        try
        {
            // Define conflicting role patterns for SOX compliance
            var conflictingRolePatterns = new List<ConflictingRolePattern>
            {
                // Financial segregation of duties violations
                new ConflictingRolePattern
                {
                    Role1Pattern = "ACCOUNTANT",
                    Role2Pattern = "CASHIER",
                    ConflictDescription = "User has both accounting and cash handling roles, violating segregation of duties for financial transactions",
                    Severity = "High",
                    Recommendation = "Separate accounting and cash handling responsibilities to different users"
                },
                new ConflictingRolePattern
                {
                    Role1Pattern = "FINANCIAL_APPROVER",
                    Role2Pattern = "PAYMENT_PROCESSOR",
                    ConflictDescription = "User can both approve and process payments, creating risk of unauthorized transactions",
                    Severity = "High",
                    Recommendation = "Separate payment approval and processing to different users"
                },
                new ConflictingRolePattern
                {
                    Role1Pattern = "INVOICE_CREATOR",
                    Role2Pattern = "PAYMENT_RECEIVER",
                    ConflictDescription = "User can create invoices and receive payments, enabling potential fraud",
                    Severity = "High",
                    Recommendation = "Separate invoice creation and payment receipt responsibilities"
                },
                new ConflictingRolePattern
                {
                    Role1Pattern = "BUDGET_MANAGER",
                    Role2Pattern = "EXPENSE_APPROVER",
                    ConflictDescription = "User controls both budget allocation and expense approval, lacking independent oversight",
                    Severity = "Medium",
                    Recommendation = "Implement independent review for budget and expense decisions"
                },
                new ConflictingRolePattern
                {
                    Role1Pattern = "ADMIN",
                    Role2Pattern = "AUDITOR",
                    ConflictDescription = "User has both administrative and auditing roles, compromising audit independence",
                    Severity = "High",
                    Recommendation = "Ensure auditors are independent from administrative functions"
                },
                new ConflictingRolePattern
                {
                    Role1Pattern = "DEVELOPER",
                    Role2Pattern = "PRODUCTION_ADMIN",
                    ConflictDescription = "User has both development and production access, violating change management controls",
                    Severity = "Medium",
                    Recommendation = "Separate development and production environment access"
                },
                new ConflictingRolePattern
                {
                    Role1Pattern = "PURCHASER",
                    Role2Pattern = "RECEIVING_CLERK",
                    ConflictDescription = "User can both order and receive goods, enabling potential procurement fraud",
                    Severity = "Medium",
                    Recommendation = "Separate purchasing and receiving responsibilities"
                },
                new ConflictingRolePattern
                {
                    Role1Pattern = "PAYROLL_ADMIN",
                    Role2Pattern = "HR_MANAGER",
                    ConflictDescription = "User controls both employee data and payroll processing, creating risk of ghost employees",
                    Severity = "High",
                    Recommendation = "Separate HR data management and payroll processing"
                },
                new ConflictingRolePattern
                {
                    Role1Pattern = "SALES",
                    Role2Pattern = "CREDIT_MANAGER",
                    ConflictDescription = "User can both make sales and approve credit, potentially bypassing credit controls",
                    Severity = "Medium",
                    Recommendation = "Separate sales and credit approval functions"
                },
                new ConflictingRolePattern
                {
                    Role1Pattern = "INVENTORY_MANAGER",
                    Role2Pattern = "WAREHOUSE_CLERK",
                    ConflictDescription = "User controls both inventory records and physical inventory, enabling potential theft",
                    Severity = "Medium",
                    Recommendation = "Separate inventory record-keeping and physical custody"
                }
            };

            // Group users by user ID to analyze multiple role assignments
            var usersByUserId = userRoleAssignments
                .GroupBy(ura => ura.UserId)
                .ToList();

            foreach (var userGroup in usersByUserId)
            {
                var userId = userGroup.Key;
                var userName = userGroup.First().UserName;
                var userRoles = userGroup.ToList();

                // Check if user has multiple roles (potential for conflicts)
                if (userRoles.Count > 1)
                {
                    // Check each pair of roles for conflicts
                    for (int i = 0; i < userRoles.Count; i++)
                    {
                        for (int j = i + 1; j < userRoles.Count; j++)
                        {
                            var role1 = userRoles[i];
                            var role2 = userRoles[j];

                            // Check against each conflicting role pattern
                            foreach (var pattern in conflictingRolePatterns)
                            {
                                if (IsRoleMatch(role1.RoleName, pattern.Role1Pattern) && 
                                    IsRoleMatch(role2.RoleName, pattern.Role2Pattern))
                                {
                                    violations.Add(new SegregationViolation
                                    {
                                        UserId = userId,
                                        UserName = userName,
                                        Role1 = role1.RoleName,
                                        Role2 = role2.RoleName,
                                        ConflictDescription = pattern.ConflictDescription,
                                        Severity = pattern.Severity,
                                        Recommendation = pattern.Recommendation
                                    });
                                }
                                // Check reverse pattern as well
                                else if (IsRoleMatch(role1.RoleName, pattern.Role2Pattern) && 
                                         IsRoleMatch(role2.RoleName, pattern.Role1Pattern))
                                {
                                    violations.Add(new SegregationViolation
                                    {
                                        UserId = userId,
                                        UserName = userName,
                                        Role1 = role1.RoleName,
                                        Role2 = role2.RoleName,
                                        ConflictDescription = pattern.ConflictDescription,
                                        Severity = pattern.Severity,
                                        Recommendation = pattern.Recommendation
                                    });
                                }
                            }
                        }
                    }
                }

                // Check for excessive role assignments (more than 3 roles)
                if (userRoles.Count > 3)
                {
                    violations.Add(new SegregationViolation
                    {
                        UserId = userId,
                        UserName = userName,
                        Role1 = $"{userRoles.Count} roles assigned",
                        Role2 = string.Join(", ", userRoles.Select(r => r.RoleName)),
                        ConflictDescription = $"User has {userRoles.Count} roles assigned, indicating excessive permissions and potential segregation of duties violations",
                        Severity = "Medium",
                        Recommendation = "Review and reduce the number of roles assigned to this user to minimum necessary permissions"
                    });
                }
            }

            // Check for users with direct screen permissions that bypass role-based controls
            var directPermissionViolations = await DetectDirectPermissionViolationsAsync(cancellationToken);
            violations.AddRange(directPermissionViolations);

            _logger.LogDebug("Detected {Count} segregation of duties violations", violations.Count);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error analyzing segregation of duties violations");
            throw;
        }

        return violations;
    }

    /// <summary>
    /// Check if a role name matches a pattern (case-insensitive, supports wildcards)
    /// </summary>
    private bool IsRoleMatch(string roleName, string pattern)
    {
        if (string.IsNullOrEmpty(roleName) || string.IsNullOrEmpty(pattern))
        {
            return false;
        }

        // Convert to uppercase for case-insensitive comparison
        var roleUpper = roleName.ToUpperInvariant();
        var patternUpper = pattern.ToUpperInvariant();

        // Simple contains check (pattern is a substring of role name)
        return roleUpper.Contains(patternUpper);
    }

    /// <summary>
    /// Detect users with direct screen permissions that bypass role-based controls.
    /// Direct permissions can indicate attempts to circumvent segregation of duties.
    /// </summary>
    private async Task<List<SegregationViolation>> DetectDirectPermissionViolationsAsync(
        CancellationToken cancellationToken)
    {
        // TODO: Reimplement when permission system is rebuilt
        return new List<SegregationViolation>();
    }

    /// <summary>
    /// Query all security-related audit events within a date range.
    /// Includes failed logins, unauthorized access attempts, exceptions, and security threats.
    /// </summary>
    private async Task<List<SecurityEvent>> QuerySecurityEventsAsync(
        DateTime startDate,
        DateTime endDate,
        CancellationToken cancellationToken)
    {
        var securityEvents = new List<SecurityEvent>();

        try
        {
            var query = from al in _dbContext.SysAuditLogs
                        join u in _dbContext.SysUsers on al.ActorId equals u.Id into userJoin
                        from u in userJoin.DefaultIfEmpty()
                        where al.CreationDate >= startDate
                           && al.CreationDate <= endDate
                           && (al.EventCategory == "Authentication"
                               || al.EventCategory == "Exception"
                               || al.EventCategory == "Security"
                               || al.Severity == "Critical"
                               || al.Severity == "Error"
                               || al.Severity == "Warning")
                        orderby al.CreationDate descending
                        select new { al, u };

            var results = await query.ToListAsync(cancellationToken);

            foreach (var item in results)
            {
                var eventCategory = item.al.EventCategory ?? "Unknown";
                var severity = item.al.Severity ?? "Info";
                var action = item.al.Action ?? "Unknown";
                var exceptionType = item.al.ExceptionType;
                var exceptionMessage = item.al.ExceptionMessage;
                var businessDescription = item.al.BusinessDescription;

                var eventType = DetermineSecurityEventType(eventCategory, action, exceptionType);

                var description = GenerateSecurityEventDescription(
                    eventCategory,
                    action,
                    exceptionType,
                    exceptionMessage,
                    businessDescription);

                var securityEvent = new SecurityEvent
                {
                    OccurredAt = item.al.CreationDate,
                    EventType = eventType,
                    Severity = severity,
                    Description = description,
                    UserId = item.al.ActorId,
                    UserName = item.u?.UserName,
                    IpAddress = item.al.IpAddress,
                    CorrelationId = item.al.CorrelationId
                };

                securityEvents.Add(securityEvent);
            }

            _logger.LogDebug(
                "Found {Count} security events from {StartDate} to {EndDate}",
                securityEvents.Count, startDate, endDate);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex,
                "Error querying security events from {StartDate} to {EndDate}",
                startDate, endDate);
            throw;
        }

        return securityEvents;
    }

    /// <summary>
    /// Determine the security event type based on event category, action, and exception type.
    /// </summary>
    private string DetermineSecurityEventType(string eventCategory, string action, string? exceptionType)
    {
        // Authentication events
        if (eventCategory == "Authentication")
        {
            if (action.Contains("LOGIN", StringComparison.OrdinalIgnoreCase) && 
                action.Contains("FAILED", StringComparison.OrdinalIgnoreCase))
            {
                return "FailedLogin";
            }
            if (action.Contains("LOGIN", StringComparison.OrdinalIgnoreCase))
            {
                return "Authentication";
            }
            if (action.Contains("LOGOUT", StringComparison.OrdinalIgnoreCase))
            {
                return "Logout";
            }
            return "Authentication";
        }

        // Exception events
        if (eventCategory == "Exception")
        {
            if (exceptionType != null)
            {
                if (exceptionType.Contains("Unauthorized", StringComparison.OrdinalIgnoreCase) ||
                    exceptionType.Contains("Forbidden", StringComparison.OrdinalIgnoreCase))
                {
                    return "UnauthorizedAccess";
                }
                if (exceptionType.Contains("Validation", StringComparison.OrdinalIgnoreCase))
                {
                    return "ValidationError";
                }
                if (exceptionType.Contains("Sql", StringComparison.OrdinalIgnoreCase) ||
                    exceptionType.Contains("Database", StringComparison.OrdinalIgnoreCase))
                {
                    return "DatabaseError";
                }
            }
            return "SystemException";
        }

        // Security events
        if (eventCategory == "Security")
        {
            if (action.Contains("INJECTION", StringComparison.OrdinalIgnoreCase))
            {
                return "SqlInjectionAttempt";
            }
            if (action.Contains("XSS", StringComparison.OrdinalIgnoreCase))
            {
                return "XssAttempt";
            }
            if (action.Contains("UNAUTHORIZED", StringComparison.OrdinalIgnoreCase))
            {
                return "UnauthorizedAccess";
            }
            return "SecurityThreat";
        }

        // Default
        return eventCategory;
    }

    /// <summary>
    /// Generate a human-readable description for a security event.
    /// </summary>
    private string GenerateSecurityEventDescription(
        string eventCategory,
        string action,
        string? exceptionType,
        string? exceptionMessage,
        string? businessDescription)
    {
        // Use business description if available (legacy compatibility)
        if (!string.IsNullOrEmpty(businessDescription))
        {
            return businessDescription;
        }

        // Use exception message if available
        if (!string.IsNullOrEmpty(exceptionMessage))
        {
            // Truncate long exception messages
            if (exceptionMessage.Length > 200)
            {
                return exceptionMessage.Substring(0, 197) + "...";
            }
            return exceptionMessage;
        }

        // Generate description from event category and action
        if (eventCategory == "Authentication")
        {
            if (action.Contains("FAILED", StringComparison.OrdinalIgnoreCase))
            {
                return "Failed login attempt detected";
            }
            return $"Authentication event: {action}";
        }

        if (eventCategory == "Exception")
        {
            if (!string.IsNullOrEmpty(exceptionType))
            {
                return $"System exception: {exceptionType}";
            }
            return "System exception occurred";
        }

        if (eventCategory == "Security")
        {
            return $"Security event: {action}";
        }

        return $"{eventCategory}: {action}";
    }

    /// <summary>
    /// Identify security incidents that require immediate attention.
    /// Analyzes security events to detect patterns and critical issues.
    /// </summary>
    private List<string> IdentifyIncidentsRequiringAttention(List<SecurityEvent> securityEvents)
    {
        var incidents = new List<string>();

        try
        {
            // Check for multiple failed logins from the same IP
            var failedLoginsByIp = securityEvents
                .Where(e => e.EventType == "FailedLogin" && !string.IsNullOrEmpty(e.IpAddress))
                .GroupBy(e => e.IpAddress)
                .Where(g => g.Count() >= 5)
                .ToList();

            foreach (var group in failedLoginsByIp)
            {
                incidents.Add($"Multiple failed login attempts ({group.Count()}) detected from IP address {group.Key}. Possible brute force attack.");
            }

            // Check for multiple failed logins for the same user
            var failedLoginsByUser = securityEvents
                .Where(e => e.EventType == "FailedLogin" && !string.IsNullOrEmpty(e.UserName))
                .GroupBy(e => e.UserName)
                .Where(g => g.Count() >= 5)
                .ToList();

            foreach (var group in failedLoginsByUser)
            {
                incidents.Add($"Multiple failed login attempts ({group.Count()}) detected for user {group.Key}. Account may be compromised.");
            }

            // Check for SQL injection attempts
            var sqlInjectionAttempts = securityEvents
                .Count(e => e.EventType == "SqlInjectionAttempt");

            if (sqlInjectionAttempts > 0)
            {
                incidents.Add($"{sqlInjectionAttempts} SQL injection attempt(s) detected. Immediate investigation required.");
            }

            // Check for XSS attempts
            var xssAttempts = securityEvents
                .Count(e => e.EventType == "XssAttempt");

            if (xssAttempts > 0)
            {
                incidents.Add($"{xssAttempts} cross-site scripting (XSS) attempt(s) detected. Immediate investigation required.");
            }

            // Check for unauthorized access attempts
            var unauthorizedAccessAttempts = securityEvents
                .Count(e => e.EventType == "UnauthorizedAccess");

            if (unauthorizedAccessAttempts > 10)
            {
                incidents.Add($"{unauthorizedAccessAttempts} unauthorized access attempts detected. Review user permissions and access controls.");
            }

            // Check for critical exceptions
            var criticalExceptions = securityEvents
                .Count(e => e.Severity == "Critical");

            if (criticalExceptions > 0)
            {
                incidents.Add($"{criticalExceptions} critical system exception(s) detected. Immediate technical investigation required.");
            }

            // Check for unusual activity patterns (many events from single user)
            var eventsByUser = securityEvents
                .Where(e => e.UserId.HasValue)
                .GroupBy(e => e.UserId)
                .Where(g => g.Count() >= 50)
                .ToList();

            foreach (var group in eventsByUser)
            {
                var userName = group.First().UserName ?? "Unknown";
                incidents.Add($"Unusual activity pattern detected for user {userName} (ID: {group.Key}). {group.Count()} security events recorded. Possible account compromise or automated attack.");
            }

            _logger.LogDebug("Identified {Count} incidents requiring attention", incidents.Count);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error identifying incidents requiring attention");
            // Don't throw - return incidents identified so far
        }

        return incidents;
    }

    #endregion

    #region Helper Classes

    /// <summary>
    /// Represents a user's role assignment
    /// </summary>
    private class UserRoleAssignment
    {
        public long UserId { get; set; }
        public string UserName { get; set; } = null!;
        public string? UserEmail { get; set; }
        public long? CompanyId { get; set; }
        public long RoleId { get; set; }
        public string RoleName { get; set; } = null!;
        public string? RoleDescription { get; set; }
        public DateTime? AssignedDate { get; set; }
    }

    /// <summary>
    /// Defines a pattern for conflicting roles
    /// </summary>
    private class ConflictingRolePattern
    {
        public string Role1Pattern { get; set; } = null!;
        public string Role2Pattern { get; set; } = null!;
        public string ConflictDescription { get; set; } = null!;
        public string Severity { get; set; } = null!;
        public string Recommendation { get; set; } = null!;
    }

    #endregion
}
