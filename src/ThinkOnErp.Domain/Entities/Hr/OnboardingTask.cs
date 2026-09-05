using System;

namespace ThinkOnErp.Domain.Entities.Hr;

public sealed class OnboardingTask
{
    public long Id { get; set; }
    public string EmployeeCode { get; set; } = string.Empty;
    public string TaskName { get; set; } = string.Empty;
    public string? AssignedTo { get; set; }
    public DateTime? DueDate { get; set; }
    public bool IsCompleted { get; set; } = false;
    public DateTime? CompletedDate { get; set; }
    public string? CompletedBy { get; set; }
    public string? Notes { get; set; }
    public string CreationUser { get; set; } = string.Empty;
    public DateTime CreationDate { get; set; } = DateTime.UtcNow;

    public Employee? Employee { get; set; }
}
