using System.Threading;
using System.Threading.Tasks;
using FluentValidation;
using MediatR;
using ThinkOnErp.Application.Common;
using ThinkOnErp.Application.Services.Hr;
using ThinkOnErp.Domain.Entities.Hr;

namespace ThinkOnErp.Application.Features.Hr.AccountingHandoffs.Commands.SavePayrollPostingConfig;

public sealed record SavePayrollPostingConfigCommand(
    PayrollPostingConfiguration Configuration
) : IRequest<ApiResponse<PayrollPostingConfiguration>>;

public sealed class SavePayrollPostingConfigCommandValidator : AbstractValidator<SavePayrollPostingConfigCommand>
{
    public SavePayrollPostingConfigCommandValidator()
    {
        RuleFor(x => x.Configuration).NotNull();
        When(x => x.Configuration != null, () =>
        {
            RuleFor(x => x.Configuration.BranchId).GreaterThan(0);
            RuleFor(x => x.Configuration.SalaryExpense).NotEmpty();
            RuleFor(x => x.Configuration.EmployerSscExpense).NotEmpty();
            RuleFor(x => x.Configuration.SalaryPayable).NotEmpty();
            RuleFor(x => x.Configuration.SscPayable).NotEmpty();
            RuleFor(x => x.Configuration.TaxPayable).NotEmpty();
            RuleFor(x => x.Configuration.DeductionsPayable).NotEmpty();
            RuleFor(x => x.Configuration.CurrencyId).GreaterThan(0);
        });
    }
}

public sealed class SavePayrollPostingConfigCommandHandler : IRequestHandler<SavePayrollPostingConfigCommand, ApiResponse<PayrollPostingConfiguration>>
{
    private readonly IHrPayrollPostingService _payrollPosting;

    public SavePayrollPostingConfigCommandHandler(IHrPayrollPostingService payrollPosting)
    {
        _payrollPosting = payrollPosting;
    }

    public async Task<ApiResponse<PayrollPostingConfiguration>> Handle(SavePayrollPostingConfigCommand request, CancellationToken cancellationToken)
    {
        await _payrollPosting.SaveAsync(request.Configuration, cancellationToken);
        return ApiResponse<PayrollPostingConfiguration>.CreateSuccess(request.Configuration);
    }
}
