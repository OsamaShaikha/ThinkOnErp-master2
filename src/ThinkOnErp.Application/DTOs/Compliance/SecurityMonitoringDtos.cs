namespace ThinkOnErp.Application.DTOs.Compliance;

/// <summary>
/// DTO for security threat information
/// </summary>
public class SecurityThreatDto
{
    /// <summary>
    /// Unique identifier for this security threat
    /// </summary>
    public long Id { get; set; }
    
    /// <summary>
    /// Type of security threat detected
    /// </summary>
    public string ThreatType { get; set; } = null!;
    
    /// <summary>
    /// Severity level of the threat
    /// </summary>
    public string Severity { get; set; } = null!;
    
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
    /// Username associated with the threat (if applicable)
    /// </summary>
    public string? Username { get; set; }
    
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
    /// Username who resolved the threat (if applicable)
    /// </summary>
    public string? ResolvedByUsername { get; set; }
    
    /// <summary>
    /// Notes about the threat resolution
    /// </summary>
    public string? ResolutionNotes { get; set; }
}

/// <summary>
/// DTO for daily security summary report
/// </summary>
public class SecuritySummaryDto
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
    public Dictionary<string, int> ThreatsByType { get; set; } = new();
    
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
    public List<IpThreatSummaryDto> TopThreatIpAddresses { get; set; } = new();
    
    /// <summary>
    /// Top 10 users by threat count
    /// </summary>
    public List<UserThreatSummaryDto> TopThreatUsers { get; set; } = new();
    
    /// <summary>
    /// When this report was generated
    /// </summary>
    public DateTime GeneratedAt { get; set; }
}

/// <summary>
/// DTO for IP threat summary
/// </summary>
public class IpThreatSummaryDto
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
    public string HighestSeverity { get; set; } = null!;
    
    /// <summary>
    /// Most common threat type from this IP
    /// </summary>
    public string MostCommonThreatType { get; set; } = null!;
    
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
/// DTO for user threat summary
/// </summary>
public class UserThreatSummaryDto
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
    public string HighestSeverity { get; set; } = null!;
    
    /// <summary>
    /// Most common threat type for this user
    /// </summary>
    public string MostCommonThreatType { get; set; } = null!;
    
    /// <summary>
    /// When the first threat for this user was detected
    /// </summary>
    public DateTime FirstThreatAt { get; set; }
    
    /// <summary>
    /// When the most recent threat for this user was detected
    /// </summary>
    public DateTime LastThreatAt { get; set; }
}

/// <summary>
/// Request DTO for resolving a security threat
/// </summary>
public class ResolveSecurityThreatDto
{
    /// <summary>
    /// Resolution notes explaining how the threat was resolved
    /// </summary>
    public string ResolutionNotes { get; set; } = null!;
}
