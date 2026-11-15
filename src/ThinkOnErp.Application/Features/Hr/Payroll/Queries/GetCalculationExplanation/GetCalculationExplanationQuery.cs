using System.Threading;
using System.Threading.Tasks;
using MediatR;
using ThinkOnErp.Application.Common;
using ThinkOnErp.Application.DTOs.Hr;
using ThinkOnErp.Application.Services.Hr;

namespace ThinkOnErp.Application.Features.Hr.Payroll.Queries.GetCalculationExplanation;

public record GetCalculationExplanationQuery(long LineId) : IRequest<ApiResponse<PayrollExplanationDto>>;

public class GetCalculationExplanationQueryHandler : IRequestHandler<GetCalculationExplanationQuery, ApiResponse<PayrollExplanationDto>>
{
    private readonly IPayrollExplanationService _explanationService;

    public GetCalculationExplanationQueryHandler(IPayrollExplanationService explanationService)
    {
        _explanationService = explanationService;
    }

    public async Task<ApiResponse<PayrollExplanationDto>> Handle(GetCalculationExplanationQuery request, CancellationToken cancellationToken)
    {
        var result = await _explanationService.GetExplanationAsync(request.LineId, cancellationToken);
        if (result == null)
            return ApiResponse<PayrollExplanationDto>.CreateFailure("Calculation explanation snapshot not found.", statusCode: 404);

        return ApiResponse<PayrollExplanationDto>.CreateSuccess(result, "Calculation explanation retrieved successfully.");
    }
}
