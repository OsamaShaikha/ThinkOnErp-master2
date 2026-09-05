using System;

namespace ThinkOnErp.Domain.Entities.Hr;

public sealed class AssetAssignment
{
    public long Id { get; set; }
    public string EmployeeCode { get; set; } = string.Empty;
    public string AssetTag { get; set; } = string.Empty;
    public string AssetDescription { get; set; } = string.Empty;
    public string Category { get; set; } = "LAPTOP"; // LAPTOP, MOBILE_PHONE, ACCESS_CARD, VEHICLE, TOOLS, OTHER
    public string? SerialNumber { get; set; }
    public DateTime IssuedDate { get; set; }
    public DateTime? ExpectedReturnDate { get; set; }
    public DateTime? ReturnedDate { get; set; }
    public string Status { get; set; } = "ASSIGNED"; // ASSIGNED, RETURNED, DAMAGED, LOST
    public string? IssuedCondition { get; set; } = "NEW";
    public string? ReturnedCondition { get; set; }
    public string? Notes { get; set; }
    public string CreationUser { get; set; } = string.Empty;
    public DateTime CreationDate { get; set; } = DateTime.UtcNow;
    public string? UpdateUser { get; set; }
    public DateTime? UpdateDate { get; set; }

    public Employee? Employee { get; set; }
}
