namespace ThinkOnErp.Domain.Entities;

/// <summary>
/// Represents a ticket priority entity for managing urgency levels and SLA targets.
/// Includes multilingual support, priority levels, and escalation thresholds.
/// Maps to the SYS_TICKET_PRIORITY table in Oracle database.
/// </summary>
public class SysTicketPriority
{
    /// <summary>
    /// Primary key - generated from SEQ_SYS_TICKET_PRIORITY sequence
    /// </summary>
    public Int64 RowId { get; set; }

    /// <summary>
    /// Priority name in Arabic
    /// </summary>
    public string PriorityNameAr { get; set; } = string.Empty;

    /// <summary>
    /// Priority name in English
    /// </summary>
    public string PriorityNameEn { get; set; } = string.Empty;

    /// <summary>
    /// Numeric priority level (1=Critical, 2=High, 3=Medium, 4=Low)
    /// Lower numbers indicate higher priority
    /// </summary>
    public int PriorityLevel { get; set; }

    /// <summary>
    /// SLA target hours for resolution of tickets with this priority
    /// </summary>
    public decimal SlaTargetHours { get; set; }

    /// <summary>
    /// Escalation threshold hours - when to send escalation alerts before SLA breach
    /// </summary>
    public decimal EscalationThresholdHours { get; set; }

    /// <summary>
    /// Soft delete flag - true for active, false for deleted
    /// </summary>
    public bool IsActive { get; set; }

    /// <summary>
    /// Username of the user who created this record
    /// </summary>
    public string CreationUser { get; set; } = string.Empty;

    /// <summary>
    /// Timestamp when the record was created
    /// </summary>
    public DateTime? CreationDate { get; set; }

    // Navigation properties
    /// <summary>
    /// Navigation property to all tickets with this priority
    /// </summary>
    public List<SysRequestTicket> Tickets { get; set; } = new();

    /// <summary>
    /// Navigation property to all ticket types that use this as default priority
    /// </summary>
    public List<SysTicketType> TicketTypesWithDefaultPriority { get; set; } = new();

    // Business logic properties
    /// <summary>
    /// Predefined priority levels for common use cases
    /// </summary>
    public static class PriorityLevels
    {
        public const int Critical = 1;
        public const int High = 2;
        public const int Medium = 3;
        public const int Low = 4;
    }

    /// <summary>
    /// Indicates if this is a critical priority level
    /// </summary>
    public bool IsCritical => PriorityLevel == PriorityLevels.Critical;

    /// <summary>
    /// Indicates if this is a high priority level
    /// </summary>
    public bool IsHigh => PriorityLevel == PriorityLevels.High;

    /// <summary>
    /// Indicates if this priority requires immediate attention (Critical or High)
    /// </summary>
    public bool RequiresImmediateAttention => PriorityLevel <= PriorityLevels.High;

    /// <summary>
    /// Calculates the escalation alert time based on creation date
    /// </summary>
    /// <param name="creationDate">The ticket creation date</param>
    /// <returns>The date/time when escalation alert should be sent</returns>
    public DateTime CalculateEscalationAlertTime(DateTime creationDate)
    {
        return creationDate.AddHours((double)EscalationThresholdHours);
    }

    /// <summary>
    /// Calculates the SLA deadline based on creation date
    /// </summary>
    /// <param name="creationDate">The ticket creation date</param>
    /// <returns>The date/time when SLA will be breached</returns>
    public DateTime CalculateSlaDeadline(DateTime creationDate)
    {
        return creationDate.AddHours((double)SlaTargetHours);
    }
}