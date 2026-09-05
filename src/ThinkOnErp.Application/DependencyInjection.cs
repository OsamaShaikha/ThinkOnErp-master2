using FluentValidation;
using MediatR;
using Microsoft.Extensions.DependencyInjection;
using System.Reflection;
using ThinkOnErp.Application.Behaviors;
using ThinkOnErp.Application.Services;
using ThinkOnErp.Application.Services.Accounting;
using ThinkOnErp.Application.Services.Hr;

namespace ThinkOnErp.Application;

/// <summary>
/// Extension methods for registering Application layer services.
/// Configures MediatR, FluentValidation, and pipeline behaviors.
/// </summary>
public static class DependencyInjection
{
    /// <summary>
    /// Registers Application layer services including MediatR, FluentValidation, and pipeline behaviors.
    /// </summary>
    /// <param name="services">The service collection</param>
    /// <returns>The service collection for chaining</returns>
    public static IServiceCollection AddApplication(this IServiceCollection services)
    {
        var assembly = Assembly.GetExecutingAssembly();

        // Register MediatR and scan for handlers in this assembly
        services.AddMediatR(cfg =>
        {
            cfg.RegisterServicesFromAssembly(assembly);
            
            // Register pipeline behaviors in order: Logging -> Audit -> Validation -> Handler
            cfg.AddBehavior(typeof(IPipelineBehavior<,>), typeof(LoggingBehavior<,>));
            cfg.AddBehavior(typeof(IPipelineBehavior<,>), typeof(AuditLoggingBehavior<,>));
            cfg.AddBehavior(typeof(IPipelineBehavior<,>), typeof(ValidationBehavior<,>));
        });

        // Register FluentValidation validators from this assembly
        services.AddValidatorsFromAssembly(assembly);

        // Register application services
        services.AddScoped<ITicketConfigurationService, TicketConfigurationService>();
        services.AddScoped<IGlAccountService, GlAccountService>();
        services.AddScoped<ICoaExcelImportService, CoaExcelImportService>();
        services.AddScoped<IGlVoucherService, GlVoucherService>();
        services.AddScoped<IGlCostCenterService, GlCostCenterService>();
        services.AddScoped<IOpeningBalanceService, OpeningBalanceService>();
        services.AddScoped<IAccountStatementService, AccountStatementService>();

        // Register HR & Payroll services
        services.AddScoped<IStatutoryRuleService, StatutoryRuleService>();
        services.AddScoped<IDepartmentService, DepartmentService>();
        services.AddScoped<IJobGradeService, JobGradeService>();
        services.AddScoped<IPositionService, PositionService>();
        services.AddScoped<IEmployeeService, EmployeeService>();
        services.AddScoped<IEmployeeDocumentService, EmployeeDocumentService>();
        services.AddScoped<IAttendanceService, AttendanceService>();
        services.AddScoped<ILeaveService, LeaveService>();
        services.AddScoped<ICompensationService, CompensationService>();
        services.AddScoped<IWorkCalendarService, WorkCalendarService>();
        services.AddScoped<IAttendanceCalculationEngine, AttendanceCalculationEngine>();
        services.AddScoped<IAttendanceCorrectionService, AttendanceCorrectionService>();
        services.AddScoped<IOvertimeCalculationService, OvertimeCalculationService>();
        services.AddScoped<IPayrollProrationService, PayrollProrationService>();
        services.AddScoped<ITaxCalculationEngine, TaxCalculationEngine>();
        services.AddScoped<ISSCCalculationService, SSCCalculationService>();
        services.AddScoped<ILoanDeductionService, LoanDeductionService>();
        services.AddScoped<IPayrollValidationService, PayrollValidationService>();
        services.AddScoped<IPayrollCalculationEngine, PayrollCalculationEngine>();
        services.AddScoped<IPayrollExplanationService, PayrollExplanationService>();
        services.AddScoped<IPayrollService, PayrollService>();
        services.AddScoped<IEndOfServiceService, EndOfServiceService>();
        services.AddScoped<IStatutoryReportingService, StatutoryReportingService>();
        services.AddScoped<IRecruitmentService, RecruitmentService>();
        services.AddScoped<IExpenseClaimService, ExpenseClaimService>();
        services.AddScoped<IAssetAssignmentService, AssetAssignmentService>();
        services.AddScoped<ISelfServiceService, SelfServiceService>();

        return services;
    }
}

