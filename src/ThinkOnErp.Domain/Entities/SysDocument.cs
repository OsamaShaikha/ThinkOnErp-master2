namespace ThinkOnErp.Domain.Entities;

public class SysDocument
{
    public Int64 Id { get; set; }
    public string FileName { get; set; } = string.Empty;
    public Int64 FileSize { get; set; }
    public string MimeType { get; set; } = string.Empty;
    public string FileExtension { get; set; } = string.Empty;
    public string FilePath { get; set; } = string.Empty;
    public string? Description { get; set; }
    public string? Category { get; set; }
    public string? Tags { get; set; }
    public int OwnerType { get; set; }
    public Int64 OwnerId { get; set; }
    public string IsActive { get; set; } = "Y";
    public string CreationUser { get; set; } = string.Empty;
    public DateTime? CreationDate { get; set; }
    public string? UpdateUser { get; set; }
    public DateTime? UpdateDate { get; set; }

    public const long MaxFileSizeBytes = 52_428_800;

    public static readonly string[] AllowedFileExtensions =
    {
        ".pdf", ".doc", ".docx", ".xls", ".xlsx",
        ".ppt", ".pptx", ".txt", ".csv",
        ".jpg", ".jpeg", ".png", ".gif",
        ".zip", ".rar", ".7z"
    };

    public static readonly string[] AllowedDocumentCategories =
    {
        "Contracts", "Reports", "Invoices", "Receipts", "Identification",
        "Certificates", "Financial", "HR", "Legal", "Technical", "Marketing", "Other"
    };

    public string GetFormattedFileSize()
    {
        if (FileSize < 1024)
            return $"{FileSize} B";
        else if (FileSize < 1024 * 1024)
            return $"{FileSize / 1024.0:F1} KB";
        else
            return $"{FileSize / (1024.0 * 1024.0):F1} MB";
    }
}
