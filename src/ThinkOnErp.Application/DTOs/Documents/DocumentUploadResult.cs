namespace ThinkOnErp.Application.DTOs.Documents;

public class DocumentUploadResult
{
    public long Id { get; set; }
    public string FileName { get; set; } = string.Empty;
    public long FileSize { get; set; }
    public string Message { get; set; } = string.Empty;
}
