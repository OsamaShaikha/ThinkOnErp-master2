using System.Threading;
using System.Threading.Tasks;
using MediatR;
using ThinkOnErp.Application.Common;
using ThinkOnErp.Application.DTOs.Hr;
using ThinkOnErp.Application.Services.Hr;
using ThinkOnErp.Domain.Interfaces.Hr;

namespace ThinkOnErp.Application.Features.Hr.Payroll.Queries.GetPayrollHandoffPaymentStatus;

public record GetPayrollHandoffPaymentStatusQuery(long Id) : IRequest<ApiResponse<PayrollHandoffPaymentStatusDto>>;

public class GetPayrollHandoffPaymentStatusQueryHandler : IRequestHandler<GetPayrollHandoffPaymentStatusQuery, ApiResponse<PayrollHandoffPaymentStatusDto>>
{
    private readonly IHrAccountingHandoffService? _accountingHandoffs;

    public GetPayrollHandoffPaymentStatusQueryHandler(IHrAccountingHandoffService? accountingHandoffs = null)
    {
        _accountingHandoffs = accountingHandoffs;
    }

    public async Task<ApiResponse<PayrollHandoffPaymentStatusDto>> Handle(GetPayrollHandoffPaymentStatusQuery request, CancellationToken cancellationToken)
    {
        if (_accountingHandoffs == null)
            return ApiResponse<PayrollHandoffPaymentStatusDto>.CreateFailure("Payroll handoff payment status not found.", statusCode: 404);

        var status = await _accountingHandoffs.GetPayrollHandoffPaymentStatusAsync(request.Id, cancellationToken);
        if (status == null)
            return ApiResponse<PayrollHandoffPaymentStatusDto>.CreateFailure("Payroll handoff payment status not found.", statusCode: 404);

        return ApiResponse<PayrollHandoffPaymentStatusDto>.CreateSuccess(status, "Payroll payment status retrieved successfully.");
    }
}
