using System;

namespace ThinkOnErp.Application.DTOs.Hr;

public sealed class AssetAssignmentDto
{
    public long Id { get; set; }
    public string EmployeeCode { get; set; } = string.Empty;
    public string EmployeeNameEn { get; set; } = string.Empty;
    public string AssetTag { get; set; } = string.Empty;
    public string AssetDescription { get; set; } = string.Empty;
    public string Category { get; set; } = "LAPTOP";
    public string? SerialNumber { get; set; }
    public DateTime IssuedDate { get; set; }
    public DateTime? ExpectedReturnDate { get; set; }
    public DateTime? ReturnedDate { get; set; }
    public string Status { get; set; } = "ASSIGNED";
    public string? IssuedCondition { get; set; }
    public string? ReturnedCondition { get; set; }
    public string? Notes { get; set; }
}

public sealed class CreateAssetAssignmentDto
{
    public string EmployeeCode { get; set; } = string.Empty;
    public string AssetTag { get; set; } = string.Empty;
    public string AssetDescription { get; set; } = string.Empty;
    public string Category { get; set; } = "LAPTOP";
    public string? SerialNumber { get; set; }
    public DateTime IssuedDate { get; set; }
    public DateTime? ExpectedReturnDate { get; set; }
    public string? IssuedCondition { get; set; } = "NEW";
    public string? Notes { get; set; }
}

public sealed class ReturnAssetDto
{
    public DateTime ReturnedDate { get; set; }
    public string? ReturnedCondition { get; set; } = "GOOD";
    public string Status { get; set; } = "RETURNED"; // RETURNED, DAMAGED, LOST
    public string? Notes { get; set; }
}
