namespace ThinkOnErp.Domain.Models;

/// <summary>
/// Represents a detected security threat or suspicious activity
/// Used by the SecurityMonitor service for threat detection and alerting
/// </summary>
public class SecurityThreat
{
    /// <summary>
    /// Unique identifier for this security threat
    /// </summary>
    public long Id { get; set; }
    
    /// <summary>
    /// Type of security threat detected
    /// </summary>
    public ThreatType ThreatType { get; set; }
    
    /// <summary>
    /// Severity level of the threat
    /// </summary>
    public ThreatSeverity Severity { get; set; }
    
    /// <summary>
    /// Description of the detected threat
    /// </summary>
    public string Description { get; set; } = null!;
    
    /// <summary>
    /// IP address associated with the threat (if applicable)
    /// </summary>
    public string? IpAddress { get; set; }
    
    /// <summary>
    /// User ID associated with the threat (if applicable)
    /// </summary>
    public long? UserId { get; set; }
    
    /// <summary>
    /// Company ID associated with the threat (if applicable)
    /// </summary>
    public long? CompanyId { get; set; }
    
    /// <summary>
    /// Branch ID associated with the threat (if applicable)
    /// </summary>
    public long? BranchId { get; set; }
    
    /// <summary>
    /// Correlation ID linking this threat to the originating request
    /// </summary>
    public string? CorrelationId { get; set; }
    
    /// <summary>
    /// Endpoint path where the threat was detected (if applicable)
    /// </summary>
    public string? EndpointPath { get; set; }
    
    /// <summary>
    /// Input data that triggered the threat detection (masked for security)
    /// </summary>
    public string? TriggerData { get; set; }
    
    /// <summary>
    /// Additional metadata about the threat in JSON format
    /// </summary>
    public string? Metadata { get; set; }
    
    /// <summary>
    /// When the threat was detected
    /// </summary>
    public DateTime DetectedAt { get; set; }
    
    /// <summary>
    /// Whether the threat is currently active or has been resolved
    /// </summary>
    public bool IsActive { get; set; }
    
    /// <summary>
    /// When the threat was resolved (if applicable)
    /// </summary>
    public DateTime? ResolvedAt { get; set; }
    
    /// <summary>
    /// User ID who resolved the threat (if applicable)
    /// </summary>
    public long? ResolvedByUserId { get; set; }
    
    /// <summary>
    /// Notes about the threat resolution
    /// </summary>
    public string? ResolutionNotes { get; set; }
}

/// <summary>
/// Types of security threats that can be detected
/// </summary>
public enum ThreatType
{
    /// <summary>
    /// Multiple failed login attempts from the same IP address
    /// </summary>
    FailedLoginPattern = 1,
    
    /// <summary>
    /// User attempting to access data outside their assigned company or branch
    /// </summary>
    UnauthorizedAccess = 2,
    
    /// <summary>
    /// SQL injection pattern detected in request parameters
    /// </summary>
    SqlInjection = 3,
    
    /// <summary>
    /// Cross-site scripting (XSS) pattern detected in request parameters
    /// </summary>
    XssAttempt = 4,
    
    /// <summary>
    /// Unusual activity pattern detected for a user (e.g., high request volume, unusual timing)
    /// </summary>
    AnomalousActivity = 5,
    
    /// <summary>
    /// API request from an unusual geographic location
    /// </summary>
    GeographicAnomaly = 6,
    
    /// <summary>
    /// Unusually high API request volume from a single user or IP
    /// </summary>
    RateLimitExceeded = 7,
    
    /// <summary>
    /// Unauthorized permission elevation attempt
    /// </summary>
    PrivilegeEscalation = 8
}

/// <summary>
/// Severity levels for security threats
/// </summary>
public enum ThreatSeverity
{
    /// <summary>
    /// Low severity - informational, may be false positive
    /// </summary>
    Low = 1,
    
    /// <summary>
    /// Medium severity - suspicious activity that should be monitored
    /// </summary>
    Medium = 2,
    
    /// <summary>
    /// High severity - likely security threat requiring investigation
    /// </summary>
    High = 3,
    
    /// <summary>
    /// Critical severity - active security threat requiring immediate action
    /// </summary>
    Critical = 4
}

/// <summary>
/// Daily security summary report for administrators
/// Provides overview of security events and threats detected in a 24-hour period
/// </summary>
public class SecuritySummaryReport
{
    /// <summary>
    /// Date this report covers
    /// </summary>
    public DateTime ReportDate { get; set; }
    
    /// <summary>
    /// Total number of security threats detected
    /// </summary>
    public int TotalThreatsDetected { get; set; }
    
    /// <summary>
    /// Number of critical severity threats
    /// </summary>
    public int CriticalThreats { get; set; }
    
    /// <summary>
    /// Number of high severity threats
    /// </summary>
    public int HighThreats { get; set; }
    
    /// <summary>
    /// Number of medium severity threats
    /// </summary>
    public int MediumThreats { get; set; }
    
    /// <summary>
    /// Number of low severity threats
    /// </summary>
    public int LowThreats { get; set; }
    
    /// <summary>
    /// Breakdown of threats by type
    /// </summary>
    public Dictionary<ThreatType, int> ThreatsByType { get; set; } = new();
    
    /// <summary>
    /// Total number of failed login attempts
    /// </summary>
    public int TotalFailedLogins { get; set; }
    
    /// <summary>
    /// Number of unique IP addresses flagged as suspicious
    /// </summary>
    public int SuspiciousIpAddresses { get; set; }
    
    /// <summary>
    /// Number of unauthorized access attempts
    /// </summary>
    public int UnauthorizedAccessAttempts { get; set; }
    
    /// <summary>
    /// Number of SQL injection attempts blocked
    /// </summary>
    public int SqlInjectionAttempts { get; set; }
    
    /// <summary>
    /// Number of XSS attempts blocked
    /// </summary>
    public int XssAttempts { get; set; }
    
    /// <summary>
    /// Number of users flagged for anomalous activity
    /// </summary>
    public int AnomalousActivityUsers { get; set; }
    
    /// <summary>
    /// Number of threats that were resolved
    /// </summary>
    public int ResolvedThreats { get; set; }
    
    /// <summary>
    /// Number of threats still active
    /// </summary>
    public int ActiveThreats { get; set; }
    
    /// <summary>
    /// Top 10 IP addresses by threat count
    /// </summary>
    public List<IpThreatSummary> TopThreatIpAddresses { get; set; } = new();
    
    /// <summary>
    /// Top 10 users by threat count
    /// </summary>
    public List<UserThreatSummary> TopThreatUsers { get; set; } = new();
    
    /// <summary>
    /// When this report was generated
    /// </summary>
    public DateTime GeneratedAt { get; set; }
}

/// <summary>
/// Summary of threats from a specific IP address
/// </summary>
public class IpThreatSummary
{
    /// <summary>
    /// IP address
    /// </summary>
    public string IpAddress { get; set; } = null!;
    
    /// <summary>
    /// Number of threats from this IP
    /// </summary>
    public int ThreatCount { get; set; }
    
    /// <summary>
    /// Highest severity threat from this IP
    /// </summary>
    public ThreatSeverity HighestSeverity { get; set; }
    
    /// <summary>
    /// Most common threat type from this IP
    /// </summary>
    public ThreatType MostCommonThreatType { get; set; }
    
    /// <summary>
    /// When the first threat from this IP was detected
    /// </summary>
    public DateTime FirstThreatAt { get; set; }
    
    /// <summary>
    /// When the most recent threat from this IP was detected
    /// </summary>
    public DateTime LastThreatAt { get; set; }
}

/// <summary>
/// Summary of threats associated with a specific user
/// </summary>
public class UserThreatSummary
{
    /// <summary>
    /// User ID
    /// </summary>
    public long UserId { get; set; }
    
    /// <summary>
    /// Username
    /// </summary>
    public string? Username { get; set; }
    
    /// <summary>
    /// Number of threats associated with this user
    /// </summary>
    public int ThreatCount { get; set; }
    
    /// <summary>
    /// Highest severity threat for this user
    /// </summary>
    public ThreatSeverity HighestSeverity { get; set; }
    
    /// <summary>
    /// Most common threat type for this user
    /// </summary>
    public ThreatType MostCommonThreatType { get; set; }
    
    /// <summary>
    /// When the first threat for this user was detected
    /// </summary>
    public DateTime FirstThreatAt { get; set; }
    
    /// <summary>
    /// When the most recent threat for this user was detected
    /// </summary>
    public DateTime LastThreatAt { get; set; }
}
