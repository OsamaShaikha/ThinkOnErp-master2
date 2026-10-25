namespace ThinkOnErp.Domain.Entities;

/// <summary>Workflow semantics are identified by codes; database IDs are opaque.</summary>
public static class TicketWorkflow
{
    public static string Code(SysTicketStatus? status) => status?.StatusCode?.Trim().ToUpperInvariant() ?? "";
    public static bool IsFinal(SysTicketStatus? status) => status == null || status.IsFinalStatus || Code(status) is "CLOSED" or "CANCELLED";
    public static bool IsResolved(SysTicketStatus? status) => Code(status) is "RESOLVED" or "CLOSED";
    public static bool IsActiveWork(SysTicketStatus? status) => status?.IsActive == true && !IsFinal(status) && !IsResolved(status);

    public static bool CanTransition(SysTicketStatus? from, SysTicketStatus? to)
    {
        if (from == null || to == null || !from.IsActive || !to.IsActive) return false;
        if (from.Id == to.Id) return true;
        if (IsFinal(from)) return false;
        return Code(from) switch {
            "OPEN" => Code(to) is "IN_PROGRESS" or "PENDING_CUSTOMER" or "CANCELLED",
            "IN_PROGRESS" => Code(to) is "OPEN" or "PENDING_CUSTOMER" or "RESOLVED" or "CANCELLED",
            "PENDING_CUSTOMER" => Code(to) is "IN_PROGRESS" or "RESOLVED" or "CANCELLED",
            "RESOLVED" => Code(to) is "CLOSED" or "IN_PROGRESS",
            _ => false
        };
    }

    public static string SlaStatus(SysTicketStatus? status, DateTime? expected, DateTime? actual, DateTime now)
    {
        if (Code(status) == "CANCELLED") return "NotApplicable";
        if (!expected.HasValue) return "Unknown";
        if (IsResolved(status)) return actual.HasValue ? actual <= expected ? "OnTime" : "Overdue" : "Unknown";
        if (!IsActiveWork(status)) return "Unknown";
        return expected < now ? "Overdue" : expected <= now.AddHours(2) ? "AtRisk" : "OnTime";
    }
}
