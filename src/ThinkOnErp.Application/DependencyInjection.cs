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

        return services;
    }
}
