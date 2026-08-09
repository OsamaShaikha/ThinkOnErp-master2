namespace ThinkOnErp.Application.DTOs.Accounting;

public sealed record CoaImportErrorDto(
    int? RowNumber,
    string? Column,
    string Code,
    string Message);
