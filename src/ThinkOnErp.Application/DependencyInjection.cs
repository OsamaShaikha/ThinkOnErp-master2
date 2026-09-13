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
        services.AddScoped<ThinkOnErp.Application.Services.Inventory.IInvItemGroupService, ThinkOnErp.Application.Services.Inventory.InvItemGroupService>();
        services.AddScoped<ThinkOnErp.Application.Services.Inventory.IInvBomService, ThinkOnErp.Application.Services.Inventory.InvBomService>();
        services.AddScoped<ThinkOnErp.Application.Services.Inventory.IInvOpeningBalanceService, ThinkOnErp.Application.Services.Inventory.InvOpeningBalanceService>();
        services.AddScoped<ThinkOnErp.Application.Services.Inventory.ITrxTypeService, ThinkOnErp.Application.Services.Inventory.TrxTypeService>();
        services.AddScoped<ITranslationService, TranslationService>();

        // POS Services
        services.AddScoped<ThinkOnErp.Application.Services.Pos.IPosTillService, ThinkOnErp.Application.Services.Pos.PosTillService>();
        services.AddScoped<ThinkOnErp.Application.Services.Pos.IPosShiftService, ThinkOnErp.Application.Services.Pos.PosShiftService>();
        services.AddScoped<ThinkOnErp.Application.Services.Pos.IPosScaleBarcodeParser, ThinkOnErp.Application.Services.Pos.PosScaleBarcodeParser>();
        services.AddScoped<ThinkOnErp.Application.Services.Pos.IPosPromotionEngine, ThinkOnErp.Application.Services.Pos.PosPromotionEngine>();
        services.AddScoped<ThinkOnErp.Application.Services.Pos.IPosPromotionService, ThinkOnErp.Application.Services.Pos.PosPromotionService>();
        services.AddScoped<ThinkOnErp.Application.Services.Pos.IPosPriceListService, ThinkOnErp.Application.Services.Pos.PosPriceListService>();
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

        return services;
    }
}
