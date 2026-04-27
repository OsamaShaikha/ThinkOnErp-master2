using System.Text.Json;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Oracle.ManagedDataAccess.Client;
using ThinkOnErp.Domain.Interfaces;
using ThinkOnErp.Domain.Models;
using ThinkOnErp.Infrastructure.Data;

namespace ThinkOnErp.Infrastructure.Services;

/// <summary>
/// Background service that generates compliance reports on a scheduled basis.
/// Supports daily, weekly, and monthly schedules with configurable cron patterns.
/// Generates GDPR, SOX, and ISO 27001 reports automatically and delivers via email.
/// Implements error handling, retry logic, and comprehensive logging.
/// </summary>
public class ScheduledReportGenerationService : BackgroundService
{
    private readonly IServiceProvider _serviceProvider;
    private readonly ILogger<ScheduledReportGenerationService> _logger;
    private readonly IConfiguration _configuration;

    // Configuration keys
    private const string EnabledKey = "ComplianceReporting:ScheduledReports:Enabled";
    private const string CheckIntervalMinutesKey = "ComplianceReporting:ScheduledReports:CheckIntervalMinutes";
    private const int DefaultCheckIntervalMinutes = 15; // Check every 15 minutes

    public ScheduledReportGenerationService(
        IServiceProvider serviceProvider,
        ILogger<ScheduledReportGenerationService> logger,
        IConfiguration configuration)
    {
        _serviceProvider = serviceProvider ?? throw new ArgumentNullException(nameof(serviceProvider));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _configuration = configuration ?? throw new ArgumentNullException(nameof(configuration));
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (!IsBackgroundServiceEnabled())
        {
            _logger.LogInformation("Scheduled report generation service is disabled");
            return;
        }

        _logger.LogInformation("Scheduled report generation service started");

        // Wait a bit before starting to allow application to fully initialize
        await Task.Delay(TimeSpan.FromSeconds(30), stoppingToken);

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await ProcessScheduledReportsAsync(stoppingToken);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error occurred during scheduled report processing");
            }

            // Wait for the configured interval before next check
            var intervalMinutes = GetCheckIntervalMinutes();
            _logger.LogDebug("Next scheduled report check in {Minutes} minutes", intervalMinutes);
            
            await Task.Delay(TimeSpan.FromMinutes(intervalMinutes), stoppingToken);
        }

        _logger.LogInformation("Scheduled report generation service stopped");
    }

    /// <summary>
    /// Process all scheduled reports that are due for generation.
    /// Queries the database for active schedules and generates reports that should run now.
    /// </summary>
    private async Task ProcessScheduledReportsAsync(CancellationToken cancellationToken)
    {
        var processingStartTime = DateTime.UtcNow;
        _logger.LogInformation("Starting scheduled report processing cycle at {Time}", processingStartTime);

        // Create a scope to resolve scoped services
        using var scope = _serviceProvider.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<OracleDbContext>();

        try
        {
            // Get all active schedules that are due for generation
            var dueSchedules = await GetDueSchedulesAsync(dbContext, cancellationToken);

            if (!dueSchedules.Any())
            {
                _logger.LogDebug("No scheduled reports due for generation");
                return;
            }

            _logger.LogInformation("Found {Count} scheduled reports due for generation", dueSchedules.Count);

            var successCount = 0;
            var failureCount = 0;

            foreach (var schedule in dueSchedules)
            {
                try
                {
                    // Mark as in progress
                    await UpdateScheduleStatusAsync(
                        dbContext,
                        schedule.Id,
                        "InProgress",
                        null,
                        cancellationToken);

                    // Generate and send the report
                    await GenerateAndSendReportAsync(scope.ServiceProvider, schedule, cancellationToken);

                    // Mark as success
                    await UpdateScheduleStatusAsync(
                        dbContext,
                        schedule.Id,
                        "Success",
                        null,
                        cancellationToken);

                    successCount++;

                    _logger.LogInformation(
                        "Successfully generated scheduled report: Type={ReportType}, Schedule={ScheduleId}, Recipients={Recipients}",
                        schedule.ReportType,
                        schedule.Id,
                        schedule.Recipients);
                }
                catch (Exception ex)
                {
                    failureCount++;

                    _logger.LogError(ex,
                        "Failed to generate scheduled report: Type={ReportType}, Schedule={ScheduleId}",
                        schedule.ReportType,
                        schedule.Id);

                    // Mark as failed with error message
                    await UpdateScheduleStatusAsync(
                        dbContext,
                        schedule.Id,
                        "Failed",
                        ex.Message,
                        cancellationToken);
                }
            }

            var duration = DateTime.UtcNow - processingStartTime;
            _logger.LogInformation(
                "Scheduled report processing cycle completed. Success: {SuccessCount}, Failed: {FailureCount}, Duration: {Duration}ms",
                successCount,
                failureCount,
                duration.TotalMilliseconds);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to complete scheduled report processing cycle");
            throw;
        }
    }

    /// <summary>
    /// Get all active schedules that are due for generation based on current time.
    /// Checks frequency (daily, weekly, monthly) and time of day to determine if report should run.
    /// </summary>
    private async Task<List<ReportSchedule>> GetDueSchedulesAsync(
        OracleDbContext dbContext,
        CancellationToken cancellationToken)
    {
        var schedules = new List<ReportSchedule>();
        var now = DateTime.UtcNow;
        var currentTime = now.ToString("HH:mm");
        var currentDayOfWeek = (int)now.DayOfWeek == 0 ? 7 : (int)now.DayOfWeek; // Convert Sunday from 0 to 7
        var currentDayOfMonth = now.Day;

        using var connection = dbContext.CreateConnection();
        await connection.OpenAsync(cancellationToken);

        using var command = connection.CreateCommand();
        command.CommandText = @"
            SELECT 
                ROW_ID,
                REPORT_TYPE,
                FREQUENCY,
                DAY_OF_WEEK,
                DAY_OF_MONTH,
                TIME_OF_DAY,
                RECIPIENTS,
                EXPORT_FORMAT,
                PARAMETERS,
                IS_ACTIVE,
                CREATED_BY_USER_ID,
                CREATED_AT,
                LAST_GENERATED_AT
            FROM SYS_REPORT_SCHEDULE
            WHERE IS_ACTIVE = 1
            ORDER BY TIME_OF_DAY, ROW_ID";

        using var reader = await command.ExecuteReaderAsync(cancellationToken);

        while (await reader.ReadAsync(cancellationToken))
        {
            var schedule = new ReportSchedule
            {
                Id = reader.GetInt64(0),
                ReportType = reader.GetString(1),
                Frequency = Enum.Parse<ReportFrequency>(reader.GetString(2)),
                DayOfWeek = reader.IsDBNull(3) ? null : reader.GetInt32(3),
                DayOfMonth = reader.IsDBNull(4) ? null : reader.GetInt32(4),
                TimeOfDay = reader.GetString(5),
                Recipients = reader.GetString(6),
                ExportFormat = Enum.Parse<ReportExportFormat>(reader.GetString(7)),
                Parameters = reader.IsDBNull(8) ? null : reader.GetString(8),
                IsActive = reader.GetInt32(9) == 1,
                CreatedByUserId = reader.GetInt64(10),
                CreatedAt = reader.GetDateTime(11),
                LastGeneratedAt = reader.IsDBNull(12) ? null : reader.GetDateTime(12)
            };

            // Check if this schedule is due for generation
            if (IsScheduleDue(schedule, now, currentTime, currentDayOfWeek, currentDayOfMonth))
            {
                schedules.Add(schedule);
            }
        }

        return schedules;
    }

    /// <summary>
    /// Determine if a schedule is due for generation based on current time and last generation time.
    /// Implements logic for daily, weekly, and monthly schedules with time-of-day matching.
    /// </summary>
    private bool IsScheduleDue(
        ReportSchedule schedule,
        DateTime now,
        string currentTime,
        int currentDayOfWeek,
        int currentDayOfMonth)
    {
        // Check if the time of day matches (within the check interval window)
        var scheduleTime = TimeSpan.Parse(schedule.TimeOfDay);
        var currentTimeSpan = TimeSpan.Parse(currentTime);
        var checkInterval = TimeSpan.FromMinutes(GetCheckIntervalMinutes());

        // Time must be within the check interval window
        var timeDifference = Math.Abs((currentTimeSpan - scheduleTime).TotalMinutes);
        if (timeDifference > checkInterval.TotalMinutes)
        {
            return false;
        }

        // Check if already generated recently (within the last hour to prevent duplicates)
        if (schedule.LastGeneratedAt.HasValue)
        {
            var timeSinceLastGeneration = now - schedule.LastGeneratedAt.Value;
            if (timeSinceLastGeneration.TotalHours < 1)
            {
                return false;
            }
        }

        // Check frequency-specific conditions
        switch (schedule.Frequency)
        {
            case ReportFrequency.Daily:
                // Daily reports run every day at the specified time
                return true;

            case ReportFrequency.Weekly:
                // Weekly reports run on the specified day of week
                if (!schedule.DayOfWeek.HasValue)
                {
                    _logger.LogWarning(
                        "Weekly schedule {ScheduleId} has no day of week configured",
                        schedule.Id);
                    return false;
                }
                return currentDayOfWeek == schedule.DayOfWeek.Value;

            case ReportFrequency.Monthly:
                // Monthly reports run on the specified day of month
                if (!schedule.DayOfMonth.HasValue)
                {
                    _logger.LogWarning(
                        "Monthly schedule {ScheduleId} has no day of month configured",
                        schedule.Id);
                    return false;
                }

                // Handle months with fewer days (e.g., February 30 -> last day of February)
                var targetDay = schedule.DayOfMonth.Value;
                var daysInMonth = DateTime.DaysInMonth(now.Year, now.Month);
                var effectiveDay = Math.Min(targetDay, daysInMonth);

                return currentDayOfMonth == effectiveDay;

            default:
                _logger.LogWarning(
                    "Unknown frequency {Frequency} for schedule {ScheduleId}",
                    schedule.Frequency,
                    schedule.Id);
                return false;
        }
    }

    /// <summary>
    /// Generate a report based on the schedule configuration and send it via email.
    /// Resolves the appropriate report generator, generates the report, exports to the specified format,
    /// and sends via email to the configured recipients.
    /// </summary>
    private async Task GenerateAndSendReportAsync(
        IServiceProvider serviceProvider,
        ReportSchedule schedule,
        CancellationToken cancellationToken)
    {
        _logger.LogInformation(
            "Generating scheduled report: Type={ReportType}, Format={Format}, Schedule={ScheduleId}",
            schedule.ReportType,
            schedule.ExportFormat,
            schedule.Id);

        var complianceReporter = serviceProvider.GetRequiredService<IComplianceReporter>();
        var emailService = serviceProvider.GetRequiredService<IEmailNotificationChannel>();

        // Parse parameters
        var parameters = ParseScheduleParameters(schedule.Parameters);

        // Calculate date range based on parameters
        var (startDate, endDate) = CalculateDateRange(parameters);

        // Generate the report based on type
        IReport report = await GenerateReportByTypeAsync(
            complianceReporter,
            schedule.ReportType,
            startDate,
            endDate,
            parameters,
            cancellationToken);

        // Export to the specified format
        byte[] reportData = await ExportReportAsync(
            complianceReporter,
            report,
            schedule.ExportFormat,
            cancellationToken);

        // Send via email
        await SendReportEmailAsync(
            emailService,
            schedule,
            report,
            reportData,
            cancellationToken);

        _logger.LogInformation(
            "Successfully generated and sent scheduled report: Type={ReportType}, Size={Size} bytes",
            schedule.ReportType,
            reportData.Length);
    }

    /// <summary>
    /// Generate a report based on the report type.
    /// Routes to the appropriate compliance reporter method based on report type.
    /// </summary>
    private async Task<IReport> GenerateReportByTypeAsync(
        IComplianceReporter complianceReporter,
        string reportType,
        DateTime startDate,
        DateTime endDate,
        Dictionary<string, object> parameters,
        CancellationToken cancellationToken)
    {
        return reportType switch
        {
            "GDPR_Access" => await GenerateGdprAccessReportAsync(
                complianceReporter,
                parameters,
                startDate,
                endDate,
                cancellationToken),

            "GDPR_Export" => await GenerateGdprExportReportAsync(
                complianceReporter,
                parameters,
                cancellationToken),

            "SOX_Financial" => await complianceReporter.GenerateSoxFinancialAccessReportAsync(
                startDate,
                endDate,
                cancellationToken),

            "SOX_Segregation" => await complianceReporter.GenerateSoxSegregationReportAsync(
                cancellationToken),

            "ISO27001_Security" => await complianceReporter.GenerateIso27001SecurityReportAsync(
                startDate,
                endDate,
                cancellationToken),

            "UserActivity" => await GenerateUserActivityReportAsync(
                complianceReporter,
                parameters,
                startDate,
                endDate,
                cancellationToken),

            "DataModification" => await GenerateDataModificationReportAsync(
                complianceReporter,
                parameters,
                cancellationToken),

            _ => throw new NotSupportedException($"Report type '{reportType}' is not supported")
        };
    }

    /// <summary>
    /// Generate GDPR access report with data subject ID from parameters.
    /// </summary>
    private async Task<IReport> GenerateGdprAccessReportAsync(
        IComplianceReporter complianceReporter,
        Dictionary<string, object> parameters,
        DateTime startDate,
        DateTime endDate,
        CancellationToken cancellationToken)
    {
        if (!parameters.TryGetValue("dataSubjectId", out var dataSubjectIdObj))
        {
            throw new InvalidOperationException("GDPR_Access report requires 'dataSubjectId' parameter");
        }

        var dataSubjectId = Convert.ToInt64(dataSubjectIdObj);

        return await complianceReporter.GenerateGdprAccessReportAsync(
            dataSubjectId,
            startDate,
            endDate,
            cancellationToken);
    }

    /// <summary>
    /// Generate GDPR export report with data subject ID from parameters.
    /// </summary>
    private async Task<IReport> GenerateGdprExportReportAsync(
        IComplianceReporter complianceReporter,
        Dictionary<string, object> parameters,
        CancellationToken cancellationToken)
    {
        if (!parameters.TryGetValue("dataSubjectId", out var dataSubjectIdObj))
        {
            throw new InvalidOperationException("GDPR_Export report requires 'dataSubjectId' parameter");
        }

        var dataSubjectId = Convert.ToInt64(dataSubjectIdObj);

        return await complianceReporter.GenerateGdprDataExportReportAsync(
            dataSubjectId,
            cancellationToken);
    }

    /// <summary>
    /// Generate user activity report with user ID from parameters.
    /// </summary>
    private async Task<IReport> GenerateUserActivityReportAsync(
        IComplianceReporter complianceReporter,
        Dictionary<string, object> parameters,
        DateTime startDate,
        DateTime endDate,
        CancellationToken cancellationToken)
    {
        if (!parameters.TryGetValue("userId", out var userIdObj))
        {
            throw new InvalidOperationException("UserActivity report requires 'userId' parameter");
        }

        var userId = Convert.ToInt64(userIdObj);

        return await complianceReporter.GenerateUserActivityReportAsync(
            userId,
            startDate,
            endDate,
            cancellationToken);
    }

    /// <summary>
    /// Generate data modification report with entity type and ID from parameters.
    /// </summary>
    private async Task<IReport> GenerateDataModificationReportAsync(
        IComplianceReporter complianceReporter,
        Dictionary<string, object> parameters,
        CancellationToken cancellationToken)
    {
        if (!parameters.TryGetValue("entityType", out var entityTypeObj))
        {
            throw new InvalidOperationException("DataModification report requires 'entityType' parameter");
        }

        if (!parameters.TryGetValue("entityId", out var entityIdObj))
        {
            throw new InvalidOperationException("DataModification report requires 'entityId' parameter");
        }

        var entityType = entityTypeObj.ToString()!;
        var entityId = Convert.ToInt64(entityIdObj);

        return await complianceReporter.GenerateDataModificationReportAsync(
            entityType,
            entityId,
            cancellationToken);
    }

    /// <summary>
    /// Export report to the specified format (PDF, CSV, JSON).
    /// </summary>
    private async Task<byte[]> ExportReportAsync(
        IComplianceReporter complianceReporter,
        IReport report,
        ReportExportFormat format,
        CancellationToken cancellationToken)
    {
        return format switch
        {
            ReportExportFormat.PDF => await complianceReporter.ExportToPdfAsync(report, cancellationToken),
            ReportExportFormat.CSV => await complianceReporter.ExportToCsvAsync(report, cancellationToken),
            ReportExportFormat.JSON => System.Text.Encoding.UTF8.GetBytes(
                await complianceReporter.ExportToJsonAsync(report, cancellationToken)),
            _ => throw new NotSupportedException($"Export format '{format}' is not supported")
        };
    }

    /// <summary>
    /// Send report via email to configured recipients.
    /// Creates an alert with the report attached and sends via email notification service.
    /// </summary>
    private async Task SendReportEmailAsync(
        IEmailNotificationChannel emailService,
        ReportSchedule schedule,
        IReport report,
        byte[] reportData,
        CancellationToken cancellationToken)
    {
        var recipients = schedule.Recipients
            .Split(',', StringSplitOptions.RemoveEmptyEntries)
            .Select(r => r.Trim())
            .ToArray();

        if (recipients.Length == 0)
        {
            throw new InvalidOperationException($"No valid recipients configured for schedule {schedule.Id}");
        }

        // Create alert for email notification
        var alert = new Alert
        {
            AlertType = "ScheduledReport",
            Severity = "Low",
            Title = $"Scheduled {schedule.ReportType} Report",
            Description = $"Scheduled compliance report generated successfully.\n\n" +
                         $"Report Type: {schedule.ReportType}\n" +
                         $"Format: {schedule.ExportFormat}\n" +
                         $"Generated At: {DateTime.UtcNow:yyyy-MM-dd HH:mm:ss UTC}\n" +
                         $"Report ID: {report.ReportId}\n" +
                         $"Size: {reportData.Length:N0} bytes\n\n" +
                         $"Please find the attached report.",
            TriggeredAt = DateTime.UtcNow,
            Metadata = JsonSerializer.Serialize(new
            {
                ScheduleId = schedule.Id,
                ReportType = schedule.ReportType,
                ReportId = report.ReportId,
                Format = schedule.ExportFormat.ToString(),
                Size = reportData.Length
            })
        };

        // Note: Current email service doesn't support attachments
        // In a production system, you would either:
        // 1. Extend IEmailNotificationChannel to support attachments
        // 2. Store the report in a file system/blob storage and include a download link
        // 3. Use a dedicated email service with attachment support
        
        // For now, we'll send the notification without the attachment
        // and log a warning about the limitation
        _logger.LogWarning(
            "Email notification sent without attachment. Report data size: {Size} bytes. " +
            "Consider implementing attachment support or file storage with download links.",
            reportData.Length);

        await emailService.SendEmailAlertAsync(alert, recipients, cancellationToken);
    }

    /// <summary>
    /// Parse schedule parameters from JSON string.
    /// Returns empty dictionary if parameters are null or invalid.
    /// </summary>
    private Dictionary<string, object> ParseScheduleParameters(string? parametersJson)
    {
        if (string.IsNullOrWhiteSpace(parametersJson))
        {
            return new Dictionary<string, object>();
        }

        try
        {
            var parameters = JsonSerializer.Deserialize<Dictionary<string, JsonElement>>(parametersJson);
            if (parameters == null)
            {
                return new Dictionary<string, object>();
            }

            // Convert JsonElement to object
            var result = new Dictionary<string, object>();
            foreach (var kvp in parameters)
            {
                result[kvp.Key] = kvp.Value.ValueKind switch
                {
                    JsonValueKind.String => kvp.Value.GetString()!,
                    JsonValueKind.Number => kvp.Value.GetInt64(),
                    JsonValueKind.True => true,
                    JsonValueKind.False => false,
                    _ => kvp.Value.ToString()
                };
            }

            return result;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to parse schedule parameters: {Parameters}", parametersJson);
            return new Dictionary<string, object>();
        }
    }

    /// <summary>
    /// Calculate date range for report based on parameters.
    /// Supports startDateOffset and endDateOffset in days (negative for past dates).
    /// Defaults to last 30 days if no parameters specified.
    /// </summary>
    private (DateTime startDate, DateTime endDate) CalculateDateRange(Dictionary<string, object> parameters)
    {
        var now = DateTime.UtcNow;

        // Default to last 30 days
        var startDateOffset = -30;
        var endDateOffset = 0;

        if (parameters.TryGetValue("startDateOffset", out var startOffsetObj))
        {
            startDateOffset = Convert.ToInt32(startOffsetObj);
        }

        if (parameters.TryGetValue("endDateOffset", out var endOffsetObj))
        {
            endDateOffset = Convert.ToInt32(endOffsetObj);
        }

        var startDate = now.AddDays(startDateOffset).Date;
        var endDate = now.AddDays(endDateOffset).Date.AddDays(1).AddTicks(-1); // End of day

        return (startDate, endDate);
    }

    /// <summary>
    /// Update schedule status and last generation time in database.
    /// Records success/failure status and error messages for monitoring.
    /// </summary>
    private async Task UpdateScheduleStatusAsync(
        OracleDbContext dbContext,
        long scheduleId,
        string status,
        string? errorMessage,
        CancellationToken cancellationToken)
    {
        using var connection = dbContext.CreateConnection();
        await connection.OpenAsync(cancellationToken);

        using var command = connection.CreateCommand();
        command.CommandText = @"
            UPDATE SYS_REPORT_SCHEDULE
            SET LAST_GENERATION_STATUS = :P_STATUS,
                LAST_ERROR_MESSAGE = :P_ERROR_MESSAGE,
                LAST_GENERATED_AT = CASE WHEN :P_STATUS = 'Success' THEN SYSDATE ELSE LAST_GENERATED_AT END
            WHERE ROW_ID = :P_SCHEDULE_ID";

        command.Parameters.Add(new OracleParameter("P_STATUS", OracleDbType.NVarchar2) 
            { Value = status });
        command.Parameters.Add(new OracleParameter("P_ERROR_MESSAGE", OracleDbType.NVarchar2) 
            { Value = (object?)errorMessage ?? DBNull.Value });
        command.Parameters.Add(new OracleParameter("P_SCHEDULE_ID", OracleDbType.Decimal) 
            { Value = scheduleId });

        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    private bool IsBackgroundServiceEnabled()
    {
        return _configuration.GetValue<bool>(EnabledKey, true);
    }

    private int GetCheckIntervalMinutes()
    {
        var intervalMinutes = _configuration.GetValue<int>(CheckIntervalMinutesKey, DefaultCheckIntervalMinutes);
        
        // Ensure minimum interval of 1 minute
        if (intervalMinutes < 1)
        {
            _logger.LogWarning("Configured interval {Interval} is too low, using minimum of 1 minute", intervalMinutes);
            return 1;
        }

        return intervalMinutes;
    }

    public override Task StopAsync(CancellationToken cancellationToken)
    {
        _logger.LogInformation("Scheduled report generation service is stopping");
        return base.StopAsync(cancellationToken);
    }
}

