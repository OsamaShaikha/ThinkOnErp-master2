namespace ThinkOnErp.Domain.Entities.Hr;

// One authoritative row for an employee's local work date. Manual rows also
// express exceptions to generated/base patterns, including explicit rest days.
public sealed class EmployeeShiftDay
{
    public long Id { get; set; }
    public string EmployeeCode { get; set; } = string.Empty;
    public DateTime WorkDate { get; set; }
    public string? ShiftCode { get; set; }
    public bool IsRestDay { get; set; }
    public bool IsActive { get; set; } = true;
    public string Source { get; set; } = "MANUAL"; // MANUAL, GENERATED
    public string CreationUser { get; set; } = string.Empty;
    public DateTime CreationDate { get; set; } = DateTime.UtcNow;
    public string? UpdateUser { get; set; }
    public DateTime? UpdateDate { get; set; }
}
