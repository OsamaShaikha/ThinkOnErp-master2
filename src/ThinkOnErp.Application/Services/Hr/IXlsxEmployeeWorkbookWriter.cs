using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using ThinkOnErp.Application.DTOs.Hr;

namespace ThinkOnErp.Application.Services.Hr;

public interface IXlsxEmployeeWorkbookWriter
{
    byte[] WriteExportWorkbook(IReadOnlyList<EmployeeExportRowDto> employees);
    byte[] WriteTemplateWorkbook();
}
