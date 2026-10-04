using System.IO;
using System.Threading;
using System.Threading.Tasks;
using ThinkOnErp.Application.DTOs.Hr;

namespace ThinkOnErp.Application.Services.Hr;

public interface IEmployeeExcelService
{
    Task<byte[]> ExportEmployeesAsync(
        string? searchKeyword = null,
        string? departmentCode = null,
        string? status = null,
        long? branchId = null,
        CancellationToken cancellationToken = default);

    Task<byte[]> GenerateTemplateAsync(CancellationToken cancellationToken = default);

    Task<EmployeeImportResultDto> ValidateWorkbookAsync(
        Stream workbook,
        CancellationToken cancellationToken = default);

    Task<EmployeeImportResultDto> ImportEmployeesAsync(
        Stream workbook,
        bool updateExisting = false,
        string user = "SYSTEM",
        CancellationToken cancellationToken = default);
}
