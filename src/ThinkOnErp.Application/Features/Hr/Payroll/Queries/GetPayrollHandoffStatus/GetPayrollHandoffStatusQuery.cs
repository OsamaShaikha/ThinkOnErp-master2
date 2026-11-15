using System.Threading;
using System.Threading.Tasks;
using MediatR;
using ThinkOnErp.Application.Common;
using ThinkOnErp.Application.DTOs.Hr;
using ThinkOnErp.Application.Services.Hr;
using ThinkOnErp.Domain.Interfaces.Hr;

namespace ThinkOnErp.Application.Features.Hr.Payroll.Queries.GetPayrollHandoffStatus;

public record GetPayrollHandoffStatusQuery(long Id) : IRequest<ApiResponse<PayrollHandoffStatusDto>>;

public class GetPayrollHandoffStatusQueryHandler : IRequestHandler<GetPayrollHandoffStatusQuery, ApiResponse<PayrollHandoffStatusDto>>
{
    private readonly IHrAccountingHandoffService? _accountingHandoffs;

    public GetPayrollHandoffStatusQueryHandler(IHrAccountingHandoffService? accountingHandoffs = null)
    {
        _accountingHandoffs = accountingHandoffs;
    }

    public async Task<ApiResponse<PayrollHandoffStatusDto>> Handle(GetPayrollHandoffStatusQuery request, CancellationToken cancellationToken)
    {
        if (_accountingHandoffs == null)
            return ApiResponse<PayrollHandoffStatusDto>.CreateFailure("Payroll handoff not found.", statusCode: 404);

        var status = await _accountingHandoffs.GetPayrollHandoffStatusAsync(request.Id, cancellationToken);
        if (status == null)
            return ApiResponse<PayrollHandoffStatusDto>.CreateFailure("Payroll handoff not found.", statusCode: 404);

        return ApiResponse<PayrollHandoffStatusDto>.CreateSuccess(status, "Payroll handoff status retrieved successfully.");
    }
}
