namespace ThinkOnErp.Application.DTOs.Accounting;

public sealed class CoaImportResultDto
{
    public int TotalRows { get; set; }
    public int ImportedCount { get; set; }
    public List<CoaImportErrorDto> Errors { get; set; } = new();
    public bool IsValid => Errors.Count == 0;
}
