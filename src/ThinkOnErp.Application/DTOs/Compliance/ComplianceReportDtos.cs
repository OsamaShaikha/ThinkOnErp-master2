namespace ThinkOnErp.Application.DTOs.Compliance;

/// <summary>
/// Base DTO for all compliance reports
/// </summary>
public class ReportDto
{
    /// <summary>
    /// Unique identifier for the report
    /// </summary>
    public string ReportId { get; set; } = null!;
    
    /// <summary>
    /// Type of compliance report (GDPR, SOX, ISO27001, UserActivity, DataModification)
    /// </summary>
    public string ReportType { get; set; } = null!;
    
    /// <summary>
    /// Title of the report
    /// </summary>
    public string Title { get; set; } = null!;
    
    /// <summary>
    /// When the report was generated
    /// </summary>
    public DateTime GeneratedAt { get; set; }
    
    /// <summary>
    /// User ID who generated the report
    /// </summary>
    public long? GeneratedByUserId { get; set; }
    
    /// <summary>
    /// Start date of the report period (if applicable)
    /// </summary>
    public DateTime? PeriodStartDate { get; set; }
    
    /// <summary>
    /// End date of the report period (if applicable)
    /// </summary>
    public DateTime? PeriodEndDate { get; set; }
}

/// <summary>
/// DTO for GDPR data access report
/// </summary>
public class GdprAccessReportDto : ReportDto
{
    /// <summary>
    /// The data subject (user) whose data access is being reported
    /// </summary>
    public long DataSubjectId { get; set; }
    
    /// <summary>
    /// Name of the data subject
    /// </summary>
    public string DataSubjectName { get; set; } = null!;
    
    /// <summary>
    /// Email of the data subject
    /// </summary>
    public string? DataSubjectEmail { get; set; }
    
    /// <summary>
    /// Total number of access events
    /// </summary>
    public int TotalAccessEvents { get; set; }
    
    /// <summary>
    /// List of all access events to the data subject's personal data
    /// </summary>
    public List<DataAccessEventDto> AccessEvents { get; set; } = new();
    
    /// <summary>
    /// Summary of access by entity type
    /// </summary>
    public Dictionary<string, int> AccessByEntityType { get; set; } = new();
    
    /// <summary>
    /// Summary of access by actor
    /// </summary>
    public Dictionary<string, int> AccessByActor { get; set; } = new();
}

/// <summary>
/// DTO for a single data access event
/// </summary>
public class DataAccessEventDto
{
    /// <summary>
    /// When the access occurred
    /// </summary>
    public DateTime AccessedAt { get; set; }
    
    /// <summary>
    /// User ID who accessed the data
    /// </summary>
    public long ActorId { get; set; }
    
    /// <summary>
    /// Name of the user who accessed the data
    /// </summary>
    public string ActorName { get; set; } = null!;
    
    /// <summary>
    /// Type of entity accessed
    /// </summary>
    public string EntityType { get; set; } = null!;
    
    /// <summary>
    /// ID of the entity accessed
    /// </summary>
    public long? EntityId { get; set; }
    
    /// <summary>
    /// Action performed (READ, UPDATE, DELETE)
    /// </summary>
    public string Action { get; set; } = null!;
    
    /// <summary>
    /// Purpose of the access (if recorded)
    /// </summary>
    public string? Purpose { get; set; }
    
    /// <summary>
    /// Legal basis for the access (if recorded)
    /// </summary>
    public string? LegalBasis { get; set; }
    
    /// <summary>
    /// IP address from which the access occurred
    /// </summary>
    public string? IpAddress { get; set; }
    
    /// <summary>
    /// Correlation ID for request tracing
    /// </summary>
    public string? CorrelationId { get; set; }
}

/// <summary>
/// DTO for GDPR data export report
/// </summary>
public class GdprDataExportReportDto : ReportDto
{
    /// <summary>
    /// The data subject (user) whose data is being exported
    /// </summary>
    public long DataSubjectId { get; set; }
    
    /// <summary>
    /// Name of the data subject
    /// </summary>
    public string DataSubjectName { get; set; } = null!;
    
    /// <summary>
    /// Email of the data subject
    /// </summary>
    public string? DataSubjectEmail { get; set; }
    
    /// <summary>
    /// All personal data organized by entity type
    /// </summary>
    public Dictionary<string, List<string>> PersonalDataByEntityType { get; set; } = new();
    
    /// <summary>
    /// Total number of data records exported
    /// </summary>
    public int TotalRecords { get; set; }
    
    /// <summary>
    /// Data categories included in the export
    /// </summary>
    public List<string> DataCategories { get; set; } = new();
}

/// <summary>
/// DTO for SOX financial access report
/// </summary>
public class SoxFinancialAccessReportDto : ReportDto
{
    /// <summary>
    /// Total number of financial data access events
    /// </summary>
    public int TotalAccessEvents { get; set; }
    
    /// <summary>
    /// Number of access events outside normal business hours
    /// </summary>
    public int OutOfHoursAccessEvents { get; set; }
    
    /// <summary>
    /// List of all financial data access events
    /// </summary>
    public List<FinancialAccessEventDto> AccessEvents { get; set; } = new();
    
    /// <summary>
    /// Summary of access by user
    /// </summary>
    public Dictionary<string, int> AccessByUser { get; set; } = new();
    
    /// <summary>
    /// Summary of access by financial entity type
    /// </summary>
    public Dictionary<string, int> AccessByEntityType { get; set; } = new();
    
    /// <summary>
    /// List of suspicious access patterns detected
    /// </summary>
    public List<string> SuspiciousPatterns { get; set; } = new();
}

/// <summary>
/// DTO for a single financial data access event
/// </summary>
public class FinancialAccessEventDto
{
    /// <summary>
    /// When the access occurred
    /// </summary>
    public DateTime AccessedAt { get; set; }
    
    /// <summary>
    /// User ID who accessed the financial data
    /// </summary>
    public long ActorId { get; set; }
    
    /// <summary>
    /// Name of the user who accessed the data
    /// </summary>
    public string ActorName { get; set; } = null!;
    
    /// <summary>
    /// Role of the user at the time of access
    /// </summary>
    public string? ActorRole { get; set; }
    
    /// <summary>
    /// Type of financial entity accessed
    /// </summary>
    public string EntityType { get; set; } = null!;
    
    /// <summary>
    /// ID of the financial entity accessed
    /// </summary>
    public long? EntityId { get; set; }
    
    /// <summary>
    /// Action performed (READ, UPDATE, DELETE, EXPORT)
    /// </summary>
    public string Action { get; set; } = null!;
    
    /// <summary>
    /// Business justification for the access (if recorded)
    /// </summary>
    public string? BusinessJustification { get; set; }
    
    /// <summary>
    /// Whether the access occurred outside normal business hours
    /// </summary>
    public bool OutOfHours { get; set; }
    
    /// <summary>
    /// IP address from which the access occurred
    /// </summary>
    public string? IpAddress { get; set; }
    
    /// <summary>
    /// Correlation ID for request tracing
    /// </summary>
    public string? CorrelationId { get; set; }
}

/// <summary>
/// DTO for SOX segregation of duties report
/// </summary>
public class SoxSegregationReportDto : ReportDto
{
    /// <summary>
    /// Total number of users analyzed
    /// </summary>
    public int TotalUsersAnalyzed { get; set; }
    
    /// <summary>
    /// Number of potential segregation of duties violations detected
    /// </summary>
    public int ViolationsDetected { get; set; }
    
    /// <summary>
    /// List of detected segregation of duties violations
    /// </summary>
    public List<SegregationViolationDto> Violations { get; set; } = new();
    
    /// <summary>
    /// Summary of role conflicts by severity
    /// </summary>
    public Dictionary<string, int> ViolationsBySeverity { get; set; } = new();
}

/// <summary>
/// DTO for a segregation of duties violation
/// </summary>
public class SegregationViolationDto
{
    /// <summary>
    /// User ID with the conflicting roles/permissions
    /// </summary>
    public long UserId { get; set; }
    
    /// <summary>
    /// Name of the user
    /// </summary>
    public string UserName { get; set; } = null!;
    
    /// <summary>
    /// Conflicting role 1
    /// </summary>
    public string Role1 { get; set; } = null!;
    
    /// <summary>
    /// Conflicting role 2
    /// </summary>
    public string Role2 { get; set; } = null!;
    
    /// <summary>
    /// Description of the conflict
    /// </summary>
    public string ConflictDescription { get; set; } = null!;
    
    /// <summary>
    /// Severity of the violation (High, Medium, Low)
    /// </summary>
    public string Severity { get; set; } = null!;
    
    /// <summary>
    /// Recommended remediation action
    /// </summary>
    public string? Recommendation { get; set; }
}

/// <summary>
/// DTO for ISO 27001 security report
/// </summary>
public class Iso27001SecurityReportDto : ReportDto
{
    /// <summary>
    /// Total number of security events
    /// </summary>
    public int TotalSecurityEvents { get; set; }
    
    /// <summary>
    /// Number of critical security events
    /// </summary>
    public int CriticalEvents { get; set; }
    
    /// <summary>
    /// Number of failed login attempts
    /// </summary>
    public int FailedLoginAttempts { get; set; }
    
    /// <summary>
    /// Number of unauthorized access attempts
    /// </summary>
    public int UnauthorizedAccessAttempts { get; set; }
    
    /// <summary>
    /// List of all security events
    /// </summary>
    public List<SecurityEventDto> SecurityEvents { get; set; } = new();
    
    /// <summary>
    /// Summary of events by severity
    /// </summary>
    public Dictionary<string, int> EventsBySeverity { get; set; } = new();
    
    /// <summary>
    /// Summary of events by type
    /// </summary>
    public Dictionary<string, int> EventsByType { get; set; } = new();
    
    /// <summary>
    /// List of security incidents requiring attention
    /// </summary>
    public List<string> IncidentsRequiringAttention { get; set; } = new();
}

/// <summary>
/// DTO for a single security event
/// </summary>
public class SecurityEventDto
{
    /// <summary>
    /// When the security event occurred
    /// </summary>
    public DateTime OccurredAt { get; set; }
    
    /// <summary>
    /// Type of security event
    /// </summary>
    public string EventType { get; set; } = null!;
    
    /// <summary>
    /// Severity of the event (Critical, High, Medium, Low)
    /// </summary>
    public string Severity { get; set; } = null!;
    
    /// <summary>
    /// Description of the security event
    /// </summary>
    public string Description { get; set; } = null!;
    
    /// <summary>
    /// User ID associated with the event (if applicable)
    /// </summary>
    public long? UserId { get; set; }
    
    /// <summary>
    /// User name associated with the event (if applicable)
    /// </summary>
    public string? UserName { get; set; }
    
    /// <summary>
    /// IP address associated with the event
    /// </summary>
    public string? IpAddress { get; set; }
    
    /// <summary>
    /// Action taken in response to the event
    /// </summary>
    public string? ActionTaken { get; set; }
    
    /// <summary>
    /// Correlation ID for request tracing
    /// </summary>
    public string? CorrelationId { get; set; }
}

/// <summary>
/// DTO for user activity report
/// </summary>
public class UserActivityReportDto : ReportDto
{
    /// <summary>
    /// The user whose activity is being reported
    /// </summary>
    public long UserId { get; set; }
    
    /// <summary>
    /// Name of the user
    /// </summary>
    public string UserName { get; set; } = null!;
    
    /// <summary>
    /// Email of the user
    /// </summary>
    public string? UserEmail { get; set; }
    
    /// <summary>
    /// Total number of actions performed
    /// </summary>
    public int TotalActions { get; set; }
    
    /// <summary>
    /// List of all user actions in chronological order
    /// </summary>
    public List<UserActivityActionDto> Actions { get; set; } = new();
    
    /// <summary>
    /// Summary of actions by type
    /// </summary>
    public Dictionary<string, int> ActionsByType { get; set; } = new();
    
    /// <summary>
    /// Summary of actions by entity type
    /// </summary>
    public Dictionary<string, int> ActionsByEntityType { get; set; } = new();
}

/// <summary>
/// DTO for a single user action
/// </summary>
public class UserActivityActionDto
{
    /// <summary>
    /// When the action occurred
    /// </summary>
    public DateTime PerformedAt { get; set; }
    
    /// <summary>
    /// Type of action (INSERT, UPDATE, DELETE, LOGIN, LOGOUT, etc.)
    /// </summary>
    public string Action { get; set; } = null!;
    
    /// <summary>
    /// Type of entity affected
    /// </summary>
    public string EntityType { get; set; } = null!;
    
    /// <summary>
    /// ID of the entity affected
    /// </summary>
    public long? EntityId { get; set; }
    
    /// <summary>
    /// Description of the action
    /// </summary>
    public string? Description { get; set; }
    
    /// <summary>
    /// IP address from which the action was performed
    /// </summary>
    public string? IpAddress { get; set; }
    
    /// <summary>
    /// Correlation ID for request tracing
    /// </summary>
    public string? CorrelationId { get; set; }
}

/// <summary>
/// DTO for data modification report
/// </summary>
public class DataModificationReportDto : ReportDto
{
    /// <summary>
    /// Type of entity being reported
    /// </summary>
    public string EntityType { get; set; } = null!;
    
    /// <summary>
    /// ID of the entity being reported
    /// </summary>
    public long EntityId { get; set; }
    
    /// <summary>
    /// Total number of modifications
    /// </summary>
    public int TotalModifications { get; set; }
    
    /// <summary>
    /// List of all modifications in chronological order
    /// </summary>
    public List<DataModificationDto> Modifications { get; set; } = new();
    
    /// <summary>
    /// Summary of modifications by action type
    /// </summary>
    public Dictionary<string, int> ModificationsByAction { get; set; } = new();
    
    /// <summary>
    /// Summary of modifications by user
    /// </summary>
    public Dictionary<string, int> ModificationsByUser { get; set; } = new();
}

/// <summary>
/// DTO for a single data modification
/// </summary>
public class DataModificationDto
{
    /// <summary>
    /// When the modification occurred
    /// </summary>
    public DateTime ModifiedAt { get; set; }
    
    /// <summary>
    /// Type of modification (INSERT, UPDATE, DELETE)
    /// </summary>
    public string Action { get; set; } = null!;
    
    /// <summary>
    /// User ID who performed the modification
    /// </summary>
    public long ActorId { get; set; }
    
    /// <summary>
    /// Name of the user who performed the modification
    /// </summary>
    public string ActorName { get; set; } = null!;
    
    /// <summary>
    /// Old value before modification (JSON format)
    /// </summary>
    public string? OldValue { get; set; }
    
    /// <summary>
    /// New value after modification (JSON format)
    /// </summary>
    public string? NewValue { get; set; }
    
    /// <summary>
    /// Fields that were changed (for UPDATE operations)
    /// </summary>
    public List<string>? ChangedFields { get; set; }
    
    /// <summary>
    /// IP address from which the modification was performed
    /// </summary>
    public string? IpAddress { get; set; }
    
    /// <summary>
    /// Correlation ID for request tracing
    /// </summary>
    public string? CorrelationId { get; set; }
}
