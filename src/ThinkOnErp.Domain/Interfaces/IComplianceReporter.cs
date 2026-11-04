using ThinkOnErp.Domain.Models;

namespace ThinkOnErp.Domain.Interfaces;

/// <summary>
/// Interface for the compliance reporter service that generates compliance reports for regulatory requirements.
/// Generates GDPR, SOX, and ISO 27001 report data. Export and scheduling have separate owners.
/// </summary>
public interface IComplianceReporter
{
    /// <summary>
    /// Generate a GDPR data access report showing all access to a specific data subject's personal data.
    /// Returns a comprehensive report of all read operations on the data subject's information.
    /// Supports GDPR Article 15 (Right of Access) compliance requirements.
    /// </summary>
    /// <param name="dataSubjectId">The unique identifier of the data subject (user ID)</param>
    /// <param name="startDate">Start date of the report period</param>
    /// <param name="endDate">End date of the report period</param>
    /// <param name="cancellationToken">Cancellation token for async operation</param>
    /// <returns>GDPR access report containing all access events for the data subject</returns>
    Task<GdprAccessReport> GenerateGdprAccessReportAsync(
        long dataSubjectId, 
        DateTime startDate, 
        DateTime endDate, 
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Generate a GDPR data export report containing all personal data for a specific data subject.
    /// Returns a complete export of all personal data stored in the system for the data subject.
    /// Supports GDPR Article 20 (Right to Data Portability) compliance requirements.
    /// </summary>
    /// <param name="dataSubjectId">The unique identifier of the data subject (user ID)</param>
    /// <param name="cancellationToken">Cancellation token for async operation</param>
    /// <returns>GDPR data export report containing all personal data for the data subject</returns>
    Task<GdprDataExportReport> GenerateGdprDataExportReportAsync(
        long dataSubjectId, 
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Generate a SOX financial access report showing all access to financial data.
    /// Returns a comprehensive report of all financial data access events for SOX compliance.
    /// Supports SOX Section 404 (Internal Controls) compliance requirements.
    /// </summary>
    /// <param name="startDate">Start date of the report period</param>
    /// <param name="endDate">End date of the report period</param>
    /// <param name="cancellationToken">Cancellation token for async operation</param>
    /// <returns>SOX financial access report containing all financial data access events</returns>
    Task<SoxFinancialAccessReport> GenerateSoxFinancialAccessReportAsync(
        DateTime startDate, 
        DateTime endDate, 
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Generate a SOX segregation of duties report analyzing role and permission assignments.
    /// Returns a report identifying potential segregation of duties violations.
    /// Supports SOX Section 404 (Internal Controls) compliance requirements.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token for async operation</param>
    /// <returns>SOX segregation of duties report containing role conflict analysis</returns>
    Task<SoxSegregationOfDutiesReport> GenerateSoxSegregationReportAsync(
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Generate an ISO 27001 security report showing all security events and incidents.
    /// Returns a comprehensive report of security events for ISO 27001 compliance.
    /// Supports ISO 27001 Annex A.12.4 (Logging and Monitoring) compliance requirements.
    /// </summary>
    /// <param name="startDate">Start date of the report period</param>
    /// <param name="endDate">End date of the report period</param>
    /// <param name="cancellationToken">Cancellation token for async operation</param>
    /// <returns>ISO 27001 security report containing all security events</returns>
    Task<Iso27001SecurityReport> GenerateIso27001SecurityReportAsync(
        DateTime startDate, 
        DateTime endDate, 
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Generate a user activity report showing all actions performed by a specific user.
    /// Returns a chronological report of all user actions within the specified date range.
    /// Useful for user behavior analysis, compliance audits, and security investigations.
    /// </summary>
    /// <param name="userId">The unique identifier of the user</param>
    /// <param name="startDate">Start date of the report period</param>
    /// <param name="endDate">End date of the report period</param>
    /// <param name="cancellationToken">Cancellation token for async operation</param>
    /// <returns>User activity report containing all actions performed by the user</returns>
    Task<UserActivityReport> GenerateUserActivityReportAsync(
        long userId, 
        DateTime startDate, 
        DateTime endDate, 
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Generate a data modification report showing all changes to a specific entity.
    /// Returns a complete audit trail of all modifications (INSERT, UPDATE, DELETE) for the entity.
    /// Useful for data lineage tracking, compliance audits, and debugging.
    /// </summary>
    /// <param name="entityType">The type of entity (e.g., "SysUser", "SysCompany")</param>
    /// <param name="entityId">The unique identifier of the entity</param>
    /// <param name="cancellationToken">Cancellation token for async operation</param>
    /// <returns>Data modification report containing all changes to the entity</returns>
    Task<DataModificationReport> GenerateDataModificationReportAsync(
        string entityType, 
        long entityId, 
        CancellationToken cancellationToken = default);

}
