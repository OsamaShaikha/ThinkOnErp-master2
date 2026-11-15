using System.Text.Json.Serialization;
using MediatR;
using ThinkOnErp.Application.Common;
using ThinkOnErp.Application.DTOs.Hr;
using ThinkOnErp.Application.Services.Hr;

namespace ThinkOnErp.Application.Features.Hr.Employees.Queries.ValidateEmployeeImport;

/// <summary>Dry-run of an import: validates every row and writes nothing (hence a query).</summary>
public sealed record ValidateEmployeeImportQuery([property: JsonIgnore] Stream Workbook) : IRequest<ApiResponse<EmployeeImportResultDto>>;

public sealed class ValidateEmployeeImportQueryHandler : IRequestHandler<ValidateEmployeeImportQuery, ApiResponse<EmployeeImportResultDto>>
{
    private readonly IEmployeeExcelService _excel;

    public ValidateEmployeeImportQueryHandler(IEmployeeExcelService excel) => _excel = excel;

    public async Task<ApiResponse<EmployeeImportResultDto>> Handle(ValidateEmployeeImportQuery request, CancellationToken ct)
    {
        var result = await _excel.ValidateWorkbookAsync(request.Workbook, ct);
        var message = result.IsValid
            ? $"File validated successfully. {result.SuccessCount} rows valid."
            : $"Validation found {result.FailureCount} rows with issues.";
        return ApiResponse<EmployeeImportResultDto>.CreateSuccess(result, message);
    }
}
