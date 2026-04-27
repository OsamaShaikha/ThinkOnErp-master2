using ThinkOnErp.Domain.Models;

namespace ThinkOnErp.Domain.Interfaces;

/// <summary>
/// Interface for security monitoring service.
/// Detects suspicious activities, security threats, and triggers security alerts.
/// Provides methods for threat detection, alert management, and security reporting.
/// </summary>
public interface ISecurityMonitor
{
    // Threat detection methods
    
    /// <summary>
    /// Detect failed login patterns from a specific IP address.
    /// Uses Redis sliding window for distributed tracking when available, falls back to database.
    /// Checks if threshold or more failed login attempts occurred from the same IP within the configured time window.
    /// Returns a SecurityThreat if the pattern is detected, null otherwise.
    /// </summary>
    /// <param name="ipAddress">IP address to check for failed login patterns</param>
    /// <returns>SecurityThreat if pattern detected, null otherwise</returns>
    Task<SecurityThreat?> DetectFailedLoginPatternAsync(string ipAddress);

    /// <summary>
    /// Track a failed login attempt in Redis (sliding window) and database.
    /// This method should be called by the authentication service when a login fails.
    /// Supports distributed rate limiting across multiple API instances.
    /// </summary>
    /// <param name="ipAddress">IP address of the failed login attempt</param>
    /// <param name="username">Username that was attempted (optional)</param>
    /// <param name="failureReason">Reason for login failure (optional)</param>
    Task TrackFailedLoginAttemptAsync(string ipAddress, string? username = null, string? failureReason = null);

    /// <summary>
    /// Get the count of failed login attempts for a specific user across all IPs.
    /// Supports per-user rate limiting in addition to per-IP rate limiting.
    /// Uses Redis sliding window when available, falls back to database.
    /// </summary>
    /// <param name="username">Username to check for failed login attempts</param>
    /// <returns>Count of failed login attempts within the configured time window</returns>
    Task<int> GetFailedLoginCountForUserAsync(string username);
    
    /// <summary>
    /// Detect unauthorized access attempts when a user tries to access data outside their assigned company or branch.
    /// Validates that the user has permission to access the specified company and branch.
    /// Returns a SecurityThreat if unauthorized access is detected, null otherwise.
    /// </summary>
    /// <param name="userId">User ID attempting to access data</param>
    /// <param name="companyId">Company ID being accessed</param>
    /// <param name="branchId">Branch ID being accessed</param>
    /// <returns>SecurityThreat if unauthorized access detected, null otherwise</returns>
    Task<SecurityThreat?> DetectUnauthorizedAccessAsync(long userId, long companyId, long branchId);
    
    /// <summary>
    /// Detect SQL injection patterns in request parameters.
    /// Scans input for common SQL injection patterns (UNION, SELECT, DROP, etc.).
    /// Returns a SecurityThreat if SQL injection pattern is detected, null otherwise.
    /// </summary>
    /// <param name="input">Input string to scan for SQL injection patterns</param>
    /// <returns>SecurityThreat if SQL injection detected, null otherwise</returns>
    Task<SecurityThreat?> DetectSqlInjectionAsync(string input);
    
    /// <summary>
    /// Detect cross-site scripting (XSS) patterns in request parameters.
    /// Scans input for common XSS patterns (script tags, event handlers, etc.).
    /// Returns a SecurityThreat if XSS pattern is detected, null otherwise.
    /// </summary>
    /// <param name="input">Input string to scan for XSS patterns</param>
    /// <returns>SecurityThreat if XSS detected, null otherwise</returns>
    Task<SecurityThreat?> DetectXssAsync(string input);
    
    /// <summary>
    /// Detect anomalous activity for a specific user.
    /// Checks for unusual patterns such as:
    /// - Unusually high API request volumes
    /// - Requests at unusual times (outside normal working hours)
    /// - Rapid succession of different operations
    /// Returns a SecurityThreat if anomalous activity is detected, null otherwise.
    /// </summary>
    /// <param name="userId">User ID to check for anomalous activity</param>
    /// <returns>SecurityThreat if anomalous activity detected, null otherwise</returns>
    Task<SecurityThreat?> DetectAnomalousActivityAsync(long userId);
    
    // Alert management methods
    
    /// <summary>
    /// Trigger a security alert for a detected threat.
    /// Persists the threat to the database and sends notifications to administrators.
    /// Alert channels may include email, webhook, SMS depending on threat severity.
    /// </summary>
    /// <param name="threat">Security threat to trigger alert for</param>
    Task TriggerSecurityAlertAsync(SecurityThreat threat);
    
    /// <summary>
    /// Get all active security threats that have not been resolved.
    /// Used by administrators to review and respond to ongoing security issues.
    /// Results are ordered by severity (Critical first) and detection time (newest first).
    /// </summary>
    /// <param name="pagination">Pagination options for the results</param>
    /// <returns>Paged collection of active security threats</returns>
    Task<PagedResult<SecurityThreat>> GetActiveThreatsAsync(PaginationOptions pagination);
    
    // Reporting methods
    
    /// <summary>
    /// Generate a daily security summary report for administrators.
    /// Includes threat counts by type and severity, top threat sources, and resolution statistics.
    /// Used for daily security monitoring and trend analysis.
    /// </summary>
    /// <param name="date">Date to generate the summary report for</param>
    /// <returns>Daily security summary report with threat statistics</returns>
    Task<SecuritySummaryReport> GenerateDailySummaryAsync(DateTime date);
}
