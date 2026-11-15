using System;
using System.Threading;
using System.Threading.Tasks;
using FluentValidation;
using MediatR;
using ThinkOnErp.Application.Common;
using ThinkOnErp.Application.DTOs.Hr;
using ThinkOnErp.Application.Services.Hr;
using ThinkOnErp.Domain.Interfaces.Hr;

namespace ThinkOnErp.Application.Features.Hr.Payroll.Commands.WithdrawPayrollHandoff;

public sealed record WithdrawPayrollHandoffCommand(
    long Id,
    string Reason,
    string User
) : IRequest<ApiResponse<PayrollHandoffStatusDto>>;

public sealed class WithdrawPayrollHandoffCommandValidator : AbstractValidator<WithdrawPayrollHandoffCommand>
{
    public WithdrawPayrollHandoffCommandValidator()
    {
        RuleFor(x => x.Id).GreaterThan(0);
        RuleFor(x => x.Reason).NotEmpty();
        RuleFor(x => x.User).NotEmpty();
    }
}

public sealed class WithdrawPayrollHandoffCommandHandler : IRequestHandler<WithdrawPayrollHandoffCommand, ApiResponse<PayrollHandoffStatusDto>>
{
    private readonly IHrAccountingHandoffService? _accountingHandoffs;

    public WithdrawPayrollHandoffCommandHandler(IHrAccountingHandoffService? accountingHandoffs = null)
    {
        _accountingHandoffs = accountingHandoffs;
    }

    public async Task<ApiResponse<PayrollHandoffStatusDto>> Handle(WithdrawPayrollHandoffCommand request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        if (_accountingHandoffs == null)
        {
            return ApiResponse<PayrollHandoffStatusDto>.CreateFailure("Payroll handoff not found.", statusCode: 404);
        }

        var result = await _accountingHandoffs.WithdrawPayrollHandoffAsync(request.Id, request.Reason, request.User, cancellationToken);
        if (result == null)
        {
            return ApiResponse<PayrollHandoffStatusDto>.CreateFailure("Payroll handoff not found.", statusCode: 404);
        }

        return ApiResponse<PayrollHandoffStatusDto>.CreateSuccess(result, "Payroll handoff withdrawn before Accounting acceptance.");
    }
}
