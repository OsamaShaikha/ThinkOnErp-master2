using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ThinkOnErp.API.Authorization;
using ThinkOnErp.Application.Common;
using ThinkOnErp.Domain.Constants;
using ThinkOnErp.Domain.Interfaces;
using ThinkOnErp.Domain.Models;

namespace ThinkOnErp.API.Controllers;

/// <summary>
/// Controller for compliance reporting operations.
/// Provides REST API endpoints for generating GDPR, SOX, and ISO 27001 compliance reports.
/// Supports multiple export formats (PDF, CSV, JSON) for regulatory audit requirements.
/// </summary>
[ApiController]
[Route("api/compliance")]
[TenantScoped]
[Authorize(Policy = "AdminOnly")]
public class ComplianceController : ControllerBase
{
    private readonly IComplianceReporter _complianceReporter;
    private readonly ILogger<ComplianceController> _logger;

    /// <summary>
    /// Initializes a new instance of the ComplianceController class.
    /// </summary>
    /// <param name="complianceReporter">Compliance reporter service for generating reports</param>
    /// <param name="logger">Logger for controller operations</param>
    public ComplianceController(
        IComplianceReporter complianceReporter,
        ILogger<ComplianceController> logger)
    {
        _complianceReporter = complianceReporter ?? throw new ArgumentNullException(nameof(complianceReporter));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    #region GDPR Reports

    /// <summary>
    /// Generate GDPR data access report for a specific data subject.
    /// Returns a comprehensive report of all access to the data subject's personal data.
    /// Supports GDPR Article 15 (Right of Access) compliance requirements.
    /// </summary>
    [HttpGet("gdpr/access-report")]
    [ProducesResponseType(typeof(ApiResponse<GdprAccessReport>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(FileContentResult), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<GdprAccessReport>), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ApiResponse<GdprAccessReport>), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ApiResponse<GdprAccessReport>), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ApiResponse<GdprAccessReport>), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GenerateGdprAccessReport(
        [FromQuery] long dataSubjectId,
        [FromQuery] DateTime startDate,
        [FromQuery] DateTime endDate,
        [FromQuery] string format = "json")
    {
        try
        {
            _logger.LogInformation(
                "Generating GDPR access report for data subject {DataSubjectId} from {StartDate} to {EndDate}, format: {Format}",
                dataSubjectId, startDate, endDate, format);

            if (startDate > endDate)
            {
                return BadRequest(ApiResponse<GdprAccessReport>.CreateFailure(
                    ErrorCodes.InvalidDateRange,
                    statusCode: 400));
            }

            var report = await _complianceReporter.GenerateGdprAccessReportAsync(
                dataSubjectId,
                startDate,
                endDate,
                HttpContext.RequestAborted);

            return await ExportReportAsync(report, format);
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
    /// Generate GDPR data export report for a specific data subject.
    /// Returns a complete export of all personal data stored in the system for the data subject.
    /// Supports GDPR Article 20 (Right to Data Portability) compliance requirements.
    /// </summary>
    [HttpGet("gdpr/data-export")]
    [ProducesResponseType(typeof(ApiResponse<GdprDataExportReport>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(FileContentResult), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<GdprDataExportReport>), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ApiResponse<GdprDataExportReport>), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ApiResponse<GdprDataExportReport>), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ApiResponse<GdprDataExportReport>), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GenerateGdprDataExport(
        [FromQuery] long dataSubjectId,
        [FromQuery] string format = "json")
    {
        try
        {
            _logger.LogInformation(
                "Generating GDPR data export report for data subject {DataSubjectId}, format: {Format}",
                dataSubjectId, format);

            var report = await _complianceReporter.GenerateGdprDataExportReportAsync(
                dataSubjectId,
                HttpContext.RequestAborted);

            return await ExportReportAsync(report, format);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex,
                "Error generating GDPR data export report for data subject {DataSubjectId}",
                dataSubjectId);
            throw;
        }
    }

    #endregion

    #region SOX Reports

    /// <summary>
    /// Generate SOX financial data access report.
    /// Returns a comprehensive report of all financial data access events for SOX compliance.
    /// Supports SOX Section 404 (Internal Controls) compliance requirements.
    /// </summary>
    [HttpGet("sox/financial-access")]
    [ProducesResponseType(typeof(ApiResponse<SoxFinancialAccessReport>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(FileContentResult), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<SoxFinancialAccessReport>), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ApiResponse<SoxFinancialAccessReport>), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ApiResponse<SoxFinancialAccessReport>), StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> GenerateSoxFinancialAccessReport(
        [FromQuery] DateTime startDate,
        [FromQuery] DateTime endDate,
        [FromQuery] string format = "json")
    {
        try
        {
            _logger.LogInformation(
                "Generating SOX financial access report from {StartDate} to {EndDate}, format: {Format}",
                startDate, endDate, format);

            if (startDate > endDate)
            {
                return BadRequest(ApiResponse<SoxFinancialAccessReport>.CreateFailure(
                    ErrorCodes.InvalidDateRange,
                    statusCode: 400));
            }

            var report = await _complianceReporter.GenerateSoxFinancialAccessReportAsync(
                startDate,
                endDate,
                HttpContext.RequestAborted);

            return await ExportReportAsync(report, format);
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
    /// Generate SOX segregation of duties report.
    /// Returns a report identifying potential segregation of duties violations.
    /// Supports SOX Section 404 (Internal Controls) compliance requirements.
    /// </summary>
    [HttpGet("sox/segregation-of-duties")]
    [ProducesResponseType(typeof(ApiResponse<SoxSegregationOfDutiesReport>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(FileContentResult), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<SoxSegregationOfDutiesReport>), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ApiResponse<SoxSegregationOfDutiesReport>), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ApiResponse<SoxSegregationOfDutiesReport>), StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> GenerateSoxSegregationReport(
        [FromQuery] string format = "json")
    {
        try
        {
            _logger.LogInformation(
                "Generating SOX segregation of duties report, format: {Format}",
                format);

            var report = await _complianceReporter.GenerateSoxSegregationReportAsync(
                HttpContext.RequestAborted);

            return await ExportReportAsync(report, format);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error generating SOX segregation of duties report");
            throw;
        }
    }

    #endregion

    #region ISO 27001 Reports

    /// <summary>
    /// Generate ISO 27001 security event report.
    /// Returns a comprehensive report of security events for ISO 27001 compliance.
    /// Supports ISO 27001 Annex A.12.4 (Logging and Monitoring) compliance requirements.
    /// </summary>
    [HttpGet("iso27001/security-report")]
    [ProducesResponseType(typeof(ApiResponse<Iso27001SecurityReport>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(FileContentResult), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<Iso27001SecurityReport>), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ApiResponse<Iso27001SecurityReport>), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ApiResponse<Iso27001SecurityReport>), StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> GenerateIso27001SecurityReport(
        [FromQuery] DateTime startDate,
        [FromQuery] DateTime endDate,
        [FromQuery] string format = "json")
    {
        try
        {
            _logger.LogInformation(
                "Generating ISO 27001 security report from {StartDate} to {EndDate}, format: {Format}",
                startDate, endDate, format);

            if (startDate > endDate)
            {
                return BadRequest(ApiResponse<Iso27001SecurityReport>.CreateFailure(
                    ErrorCodes.InvalidDateRange,
                    statusCode: 400));
            }

            var report = await _complianceReporter.GenerateIso27001SecurityReportAsync(
                startDate,
                endDate,
                HttpContext.RequestAborted);

            return await ExportReportAsync(report, format);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex,
                "Error generating ISO 27001 security report from {StartDate} to {EndDate}",
                startDate, endDate);
            throw;
        }
    }

    #endregion

    #region General Reports

    /// <summary>
    /// Generate user activity report for a specific user.
    /// Returns a chronological report of all user actions within the specified date range.
    /// Useful for user behavior analysis, compliance audits, and security investigations.
    /// </summary>
    [HttpGet("user-activity")]
    [ProducesResponseType(typeof(ApiResponse<UserActivityReport>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(FileContentResult), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<UserActivityReport>), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ApiResponse<UserActivityReport>), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ApiResponse<UserActivityReport>), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ApiResponse<UserActivityReport>), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GenerateUserActivityReport(
        [FromQuery] long userId,
        [FromQuery] DateTime startDate,
        [FromQuery] DateTime endDate,
        [FromQuery] string format = "json")
    {
        try
        {
            _logger.LogInformation(
                "Generating user activity report for user {UserId} from {StartDate} to {EndDate}, format: {Format}",
                userId, startDate, endDate, format);

            if (startDate > endDate)
            {
                return BadRequest(ApiResponse<UserActivityReport>.CreateFailure(
                    ErrorCodes.InvalidDateRange,
                    statusCode: 400));
            }

            var report = await _complianceReporter.GenerateUserActivityReportAsync(
                userId,
                startDate,
                endDate,
                HttpContext.RequestAborted);

            return await ExportReportAsync(report, format);
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
    /// Generate data modification report for a specific entity.
    /// Returns a complete audit trail of all modifications (INSERT, UPDATE, DELETE) for the entity.
    /// Useful for data lineage tracking, compliance audits, and debugging.
    /// </summary>
    [HttpGet("data-modification")]
    [ProducesResponseType(typeof(ApiResponse<DataModificationReport>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(FileContentResult), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<DataModificationReport>), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ApiResponse<DataModificationReport>), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ApiResponse<DataModificationReport>), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ApiResponse<DataModificationReport>), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GenerateDataModificationReport(
        [FromQuery] string entityType,
        [FromQuery] long entityId,
        [FromQuery] string format = "json")
    {
        try
        {
            _logger.LogInformation(
                "Generating data modification report for entity {EntityType} {EntityId}, format: {Format}",
                entityType, entityId, format);

            if (string.IsNullOrWhiteSpace(entityType))
            {
                return BadRequest(ApiResponse<DataModificationReport>.CreateFailure(
                    ErrorCodes.FieldRequired,
                    statusCode: 400));
            }

            var report = await _complianceReporter.GenerateDataModificationReportAsync(
                entityType,
                entityId,
                HttpContext.RequestAborted);

            return await ExportReportAsync(report, format);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex,
                "Error generating data modification report for entity {EntityType} {EntityId}",
                entityType, entityId);
            throw;
        }
    }

    #endregion

    #region Helper Methods

    /// <summary>
    /// Export a report in the requested format (JSON, CSV, or PDF).
    /// Supports content negotiation via query parameter or Accept header.
    /// </summary>
    private async Task<IActionResult> ExportReportAsync(IReport report, string format)
    {
        format = format?.ToLowerInvariant() ?? "json";

        if (format == "json" && Request.Headers.ContainsKey("Accept"))
        {
            var acceptHeader = Request.Headers["Accept"].ToString().ToLowerInvariant();
            if (acceptHeader.Contains("text/csv"))
            {
                format = "csv";
            }
            else if (acceptHeader.Contains("application/pdf"))
            {
                format = "pdf";
            }
        }

        try
        {
            switch (format)
            {
                case "csv":
                    var csvBytes = await _complianceReporter.ExportToCsvAsync(report, HttpContext.RequestAborted);
                    var csvFileName = $"{report.ReportType}_{report.ReportId}_{DateTime.UtcNow:yyyyMMdd_HHmmss}.csv";
                    return File(csvBytes, "text/csv", csvFileName);

                case "pdf":
                    var pdfBytes = await _complianceReporter.ExportToPdfAsync(report, HttpContext.RequestAborted);
                    
                    if (pdfBytes == null || pdfBytes.Length == 0)
                    {
                        return BadRequest(ApiResponse<IReport>.CreateFailure(
                            ErrorCodes.OperationFailed,
                            statusCode: 400));
                    }
                    
                    var pdfFileName = $"{report.ReportType}_{report.ReportId}_{DateTime.UtcNow:yyyyMMdd_HHmmss}.pdf";
                    return File(pdfBytes, "application/pdf", pdfFileName);

                case "json":
                default:
                    return Ok(ApiResponse<IReport>.CreateSuccess(
                        report,
                        ResponseCodes.ComplianceReportGenerated,
                        200));
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error exporting report {ReportId} to format {Format}", report.ReportId, format);
            throw;
        }
    }

    #endregion
}
