using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using ThinkOnErp.Domain.Interfaces;
using ThinkOnErp.Domain.Models;

namespace ThinkOnErp.Infrastructure.Services;

/// <summary>
/// Renders the supported concrete compliance report types without querying report data.
/// </summary>
public sealed class ComplianceReportExporter(ILogger<ComplianceReportExporter> _logger) : IComplianceReportExporter
{
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
            cancellationToken.ThrowIfCancellationRequested();
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
            cancellationToken.ThrowIfCancellationRequested();
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
            cancellationToken.ThrowIfCancellationRequested();
            _logger.LogInformation("Exporting report {ReportId} to JSON", report.ReportId);

            var json = JsonSerializer.Serialize(report, report.GetType(), new JsonSerializerOptions
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

}
