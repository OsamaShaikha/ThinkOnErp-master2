using System.IO;
using System.Threading;
using System.Threading.Tasks;
using ThinkOnErp.Application.DTOs.Hr;

namespace ThinkOnErp.Application.Services.Hr;

public interface IXlsxEmployeeWorkbookReader
{
    Task<(IReadOnlyList<EmployeeImportRowDto> Rows, EmployeeImportResultDto Result)> ReadAsync(
        Stream workbook,
        CancellationToken cancellationToken = default);
}
