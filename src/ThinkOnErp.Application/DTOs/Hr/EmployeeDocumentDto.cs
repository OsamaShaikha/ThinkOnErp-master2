using System;

namespace ThinkOnErp.Application.DTOs.Hr;

public sealed class EmployeeDocumentDto
{
    public long Id { get; set; }
    public string EmployeeCode { get; set; } = string.Empty;
    public string DocumentType { get; set; } = "NATIONAL_ID";
    public string? DocumentNumber { get; set; }
    public string FileReference { get; set; } = string.Empty;
    public string? FileName { get; set; }
    public DateTime? IssuedDate { get; set; }
    public DateTime? ExpiryDate { get; set; }
    public string? Notes { get; set; }
    public bool IsActive { get; set; }
}

public sealed class CreateEmployeeDocumentDto
{
    public string DocumentType { get; set; } = "NATIONAL_ID";
    public string? DocumentNumber { get; set; }
    public string FileReference { get; set; } = string.Empty;
    public string? FileName { get; set; }
    public DateTime? IssuedDate { get; set; }
    public DateTime? ExpiryDate { get; set; }
    public string? Notes { get; set; }
}

public sealed class DocumentExpiryReportDto
{
    public long DocumentId { get; set; }
    public string EmployeeCode { get; set; } = string.Empty;
    public string EmployeeNameAr { get; set; } = string.Empty;
    public string EmployeeNameEn { get; set; } = string.Empty;
    public string? DepartmentName { get; set; }
    public string DocumentType { get; set; } = string.Empty;
    public string? DocumentNumber { get; set; }
    public DateTime? ExpiryDate { get; set; }
    public int DaysUntilExpiry { get; set; }
    public bool IsExpired { get; set; }
}
