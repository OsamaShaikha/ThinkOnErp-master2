namespace ThinkOnErp.Application.DTOs.Accounting;

public sealed class CoaWorkbookReadResultDto
{
    public List<CoaImportRowDto> Rows { get; set; } = new();
    public List<CoaImportErrorDto> Errors { get; set; } = new();
    public bool IsValid => Errors.Count == 0;
}
