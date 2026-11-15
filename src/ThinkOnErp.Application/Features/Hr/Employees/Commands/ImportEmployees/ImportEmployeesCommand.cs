using System.Text.Json.Serialization;
using MediatR;
using ThinkOnErp.Application.Common;
using ThinkOnErp.Application.DTOs.Hr;
using ThinkOnErp.Application.Services.Hr;

namespace ThinkOnErp.Application.Features.Hr.Employees.Commands.ImportEmployees;

/// <param name="Workbook">The uploaded .xlsx stream. Excluded from audit serialization.</param>
public sealed record ImportEmployeesCommand(
    [property: JsonIgnore] Stream Workbook,
    bool UpdateExisting,
    string Username) : IRequest<ApiResponse<EmployeeImportResultDto>>;

public sealed class ImportEmployeesCommandHandler : IRequestHandler<ImportEmployeesCommand, ApiResponse<EmployeeImportResultDto>>
{
    private readonly IEmployeeExcelService _excel;

    public ImportEmployeesCommandHandler(IEmployeeExcelService excel) => _excel = excel;

    public async Task<ApiResponse<EmployeeImportResultDto>> Handle(ImportEmployeesCommand request, CancellationToken ct)
    {
        var result = await _excel.ImportEmployeesAsync(request.Workbook, request.UpdateExisting, request.Username, ct);

        if (!result.IsValid)
        {
            return ApiResponse<EmployeeImportResultDto>.CreateFailure(
                $"Import failed with {result.FailureCount} errors.",
                errors: result.Errors.Select(e => $"Row {e.RowNumber} [{e.Column}]: {e.Message}").ToList(),
                statusCode: 400);
        }

        return ApiResponse<EmployeeImportResultDto>.CreateSuccess(
            result,
            $"Import completed successfully. {result.InsertedCount} created, {result.UpdatedCount} updated.");
    }
}
