using MediatR;
using ThinkOnErp.Application.Services.Hr;

namespace ThinkOnErp.Application.Features.Hr.Employees.Queries.GetEmployeeImportTemplate;

/// <summary>Returns the blank employee import workbook as .xlsx bytes.</summary>
public sealed record GetEmployeeImportTemplateQuery : IRequest<byte[]>;

public sealed class GetEmployeeImportTemplateQueryHandler : IRequestHandler<GetEmployeeImportTemplateQuery, byte[]>
{
    private readonly IEmployeeExcelService _excel;

    public GetEmployeeImportTemplateQueryHandler(IEmployeeExcelService excel) => _excel = excel;

    public Task<byte[]> Handle(GetEmployeeImportTemplateQuery request, CancellationToken ct) => _excel.GenerateTemplateAsync(ct);
}
