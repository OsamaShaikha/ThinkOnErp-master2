using FluentValidation;
using MediatR;
using Microsoft.Extensions.DependencyInjection;
using System.Reflection;
using ThinkOnErp.Application.Behaviors;
using ThinkOnErp.Application.Services;
using ThinkOnErp.Application.Services.Accounting;
using ThinkOnErp.Application.Services.Accounting.Tax;

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
        services.AddScoped<IGlFiscalPeriodService, GlFiscalPeriodService>();
        services.AddScoped<IGlAccountBalanceService, GlAccountBalanceService>();
        services.AddScoped<ICustomerService, CustomerService>();
        services.AddScoped<IVendorService, VendorService>();
        services.AddScoped<ISubledgerService, SubledgerService>();
        services.AddScoped<IReceiptPaymentVoucherService, ReceiptPaymentVoucherService>();
        services.AddScoped<IPdcService, PdcService>();
        services.AddScoped<IFinancialReportsService, FinancialReportsService>();
        services.AddScoped<IPostingRuleService, PostingRuleService>();
        services.AddScoped<IBankingService, BankingService>();
        services.AddScoped<IPaymentMethodService, PaymentMethodService>();
        services.AddScoped<IFiscalClosingService, FiscalClosingService>(); 
        services.AddScoped<IOpeningBalanceService, OpeningBalanceService>();
        services.AddScoped<IAccountStatementService, AccountStatementService>();
        services.AddScoped<ITaxEngineService, TaxEngineService>();
        services.AddScoped<ThinkOnErp.Application.Services.Validation.IDynamicValidationEngine, ThinkOnErp.Application.Services.Validation.DynamicValidationEngine>();

        // Inventory & Unified Trade Documents Engine
        services.AddScoped<ThinkOnErp.Application.Services.Inventory.IInvItemService, ThinkOnErp.Application.Services.Inventory.InvItemService>();
        services.AddScoped<ThinkOnErp.Application.Services.Inventory.IInvWarehouseService, ThinkOnErp.Application.Services.Inventory.InvWarehouseService>();
        services.AddScoped<ThinkOnErp.Application.Services.Inventory.IInvStockLedgerService, ThinkOnErp.Application.Services.Inventory.InvStockLedgerService>();
        services.AddScoped<ThinkOnErp.Application.Services.Inventory.IInvCostingEngine, ThinkOnErp.Application.Services.Inventory.InvCostingEngine>();
        services.AddScoped<ThinkOnErp.Application.Services.Inventory.IInvAtpCalculator, ThinkOnErp.Application.Services.Inventory.InvAtpCalculator>();
        services.AddScoped<ThinkOnErp.Application.Services.Inventory.IInvReconciliationService, ThinkOnErp.Application.Services.Inventory.InvReconciliationService>();
        services.AddScoped<ThinkOnErp.Application.Services.Inventory.ITrxDocumentService, ThinkOnErp.Application.Services.Inventory.TrxDocumentService>();
        services.AddScoped<ThinkOnErp.Application.Services.Inventory.IInvItemCategoryService, ThinkOnErp.Application.Services.Inventory.InvItemCategoryService>();
        services.AddScoped<ThinkOnErp.Application.Services.Inventory.IInvBomService, ThinkOnErp.Application.Services.Inventory.InvBomService>();
        services.AddScoped<ThinkOnErp.Application.Services.Inventory.IInvOpeningBalanceService, ThinkOnErp.Application.Services.Inventory.InvOpeningBalanceService>();
        services.AddScoped<ThinkOnErp.Application.Services.Inventory.ITrxTypeService, ThinkOnErp.Application.Services.Inventory.TrxTypeService>();
        services.AddScoped<ThinkOnErp.Application.Services.Inventory.IInvVariantService, ThinkOnErp.Application.Services.Inventory.InvVariantService>();
        services.AddScoped<ThinkOnErp.Application.Services.Inventory.IInvModifierService, ThinkOnErp.Application.Services.Inventory.InvModifierService>();
        services.AddScoped<ThinkOnErp.Application.Services.Inventory.IInvPriceListService, ThinkOnErp.Application.Services.Inventory.InvPriceListService>();
        services.AddScoped<ITranslationService, TranslationService>();

        // POS Services
        services.AddScoped<ThinkOnErp.Application.Services.Pos.IPosTillService, ThinkOnErp.Application.Services.Pos.PosTillService>();
        services.AddScoped<ThinkOnErp.Application.Services.Pos.IPosShiftService, ThinkOnErp.Application.Services.Pos.PosShiftService>();
        services.AddScoped<ThinkOnErp.Application.Services.Pos.IPosScaleBarcodeParser, ThinkOnErp.Application.Services.Pos.PosScaleBarcodeParser>();
        services.AddScoped<ThinkOnErp.Application.Services.Pos.IPosPromotionEngine, ThinkOnErp.Application.Services.Pos.PosPromotionEngine>();
        services.AddScoped<ThinkOnErp.Application.Services.Pos.IPosPromotionService, ThinkOnErp.Application.Services.Pos.PosPromotionService>();
        services.AddScoped<ThinkOnErp.Application.Services.Pos.IPosPrintTemplateService, ThinkOnErp.Application.Services.Pos.PosPrintTemplateService>();
        services.AddScoped<ThinkOnErp.Application.Services.Pos.IPosCalculationEngine, ThinkOnErp.Application.Services.Pos.PosCalculationEngine>();
        services.AddScoped<ThinkOnErp.Application.Services.Pos.IPosOrderService, ThinkOnErp.Application.Services.Pos.PosOrderService>();
        services.AddScoped<ThinkOnErp.Application.Services.Pos.IPosSyncService, ThinkOnErp.Application.Services.Pos.PosSyncService>();
        services.AddScoped<ThinkOnErp.Application.Services.Pos.IPosTableService, ThinkOnErp.Application.Services.Pos.PosTableService>();
        services.AddScoped<ThinkOnErp.Application.Services.Pos.IPosReservationService, ThinkOnErp.Application.Services.Pos.PosReservationService>();
        services.AddScoped<ThinkOnErp.Application.Services.Pos.IPosBatchPrepService, ThinkOnErp.Application.Services.Pos.PosBatchPrepService>();
        services.AddScoped<ThinkOnErp.Application.Services.Pos.IPosZReportService, ThinkOnErp.Application.Services.Pos.PosZReportService>();
        services.AddScoped<ThinkOnErp.Application.Services.Pos.IPosKdsService, ThinkOnErp.Application.Services.Pos.PosKdsService>();
        services.AddScoped<ThinkOnErp.Application.Services.Pos.IPosAnalyticsService, ThinkOnErp.Application.Services.Pos.PosAnalyticsService>();
        services.AddScoped<ThinkOnErp.Application.Services.Pos.IPosEInvoicingService, ThinkOnErp.Application.Services.Pos.PosEInvoicingService>();
        services.AddScoped<ThinkOnErp.Application.Services.Pos.IPosGiftCardService, ThinkOnErp.Application.Services.Pos.PosGiftCardService>();
        services.AddScoped<ThinkOnErp.Application.Services.Pos.IPosStaffAttendanceService, ThinkOnErp.Application.Services.Pos.PosStaffAttendanceService>();
        services.AddScoped<ThinkOnErp.Application.Services.Pos.IPosAuditService, ThinkOnErp.Application.Services.Pos.PosAuditService>();
        services.AddScoped<ThinkOnErp.Application.Services.Pos.IPosModifierService, ThinkOnErp.Application.Services.Pos.PosModifierService>();

        // HR & Dynamic Payroll Engine Services
        services.AddScoped<ThinkOnErp.Application.Services.Hr.IWorkCalendarService, ThinkOnErp.Application.Services.Hr.WorkCalendarService>();
        services.AddScoped<ThinkOnErp.Application.Services.Hr.IAttendanceCalculationEngine, ThinkOnErp.Application.Services.Hr.AttendanceCalculationEngine>();
        services.AddScoped<ThinkOnErp.Application.Services.Hr.IAttendanceCorrectionService, ThinkOnErp.Application.Services.Hr.AttendanceCorrectionService>();
        services.AddScoped<ThinkOnErp.Application.Services.Hr.IOvertimeCalculationService, ThinkOnErp.Application.Services.Hr.OvertimeCalculationService>();
        services.AddScoped<ThinkOnErp.Application.Services.Hr.IPayrollProrationService, ThinkOnErp.Application.Services.Hr.PayrollProrationService>();
        services.AddScoped<ThinkOnErp.Application.Services.Hr.ISSCCalculationService, ThinkOnErp.Application.Services.Hr.SSCCalculationService>();
        services.AddScoped<ThinkOnErp.Application.Services.Hr.ITaxCalculationEngine, ThinkOnErp.Application.Services.Hr.TaxCalculationEngine>();
        services.AddScoped<ThinkOnErp.Application.Services.Hr.ILoanDeductionService, ThinkOnErp.Application.Services.Hr.LoanDeductionService>();
        services.AddScoped<ThinkOnErp.Application.Services.Hr.IPayrollAdjustmentService, ThinkOnErp.Application.Services.Hr.PayrollAdjustmentService>();
        services.AddScoped<ThinkOnErp.Application.Services.Hr.IPayrollCalculationEngine, ThinkOnErp.Application.Services.Hr.PayrollCalculationEngine>();
        services.AddScoped<ThinkOnErp.Application.Services.Hr.IPayrollValidationService, ThinkOnErp.Application.Services.Hr.PayrollValidationService>();
        services.AddScoped<ThinkOnErp.Application.Services.Hr.IPayrollExplanationService, ThinkOnErp.Application.Services.Hr.PayrollExplanationService>();
        services.AddScoped<ThinkOnErp.Application.Services.Hr.IPayrollService, ThinkOnErp.Application.Services.Hr.PayrollService>();
        services.AddScoped<ThinkOnErp.Application.Services.Hr.IEmployeeService, ThinkOnErp.Application.Services.Hr.EmployeeService>();
        services.AddScoped<ThinkOnErp.Application.Services.Hr.ILeaveService, ThinkOnErp.Application.Services.Hr.LeaveService>();
        services.AddScoped<ThinkOnErp.Application.Services.Hr.IBankPayrollExportService, ThinkOnErp.Application.Services.Hr.BankPayrollExportService>();

        return services;
    }
}
