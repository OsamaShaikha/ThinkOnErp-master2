using MediatR;
using ThinkOnErp.Application.Services.Hr;

namespace ThinkOnErp.Application.Features.Hr.Employees.Queries.ExportEmployees;

/// <summary>Returns the filtered employee list as .xlsx bytes.</summary>
public sealed record ExportEmployeesQuery(string? SearchKeyword, string? DepartmentCode, string? Status, long? BranchId) : IRequest<byte[]>;

public sealed class ExportEmployeesQueryHandler : IRequestHandler<ExportEmployeesQuery, byte[]>
{
    private readonly IEmployeeExcelService _excel;

    public ExportEmployeesQueryHandler(IEmployeeExcelService excel) => _excel = excel;

    public Task<byte[]> Handle(ExportEmployeesQuery request, CancellationToken ct)
        => _excel.ExportEmployeesAsync(request.SearchKeyword, request.DepartmentCode, request.Status, request.BranchId, ct);
}
