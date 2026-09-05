using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using ThinkOnErp.Domain.Entities;
using ThinkOnErp.Domain.Entities.Accounting;
using ThinkOnErp.Domain.Entities.Inventory;
using ThinkOnErp.Domain.Entities.Views;
using ThinkOnErp.Infrastructure.Data.Configurations.Accounting;

namespace ThinkOnErp.Infrastructure.Data;

public class OracleDbContext : DbContext
{
    public OracleDbContext(DbContextOptions<OracleDbContext> options) : base(options)
    {
    }

    public Task<IQueryable<SysAuditLog>> GetCombinedAuditLogsAsync()
    {
        // Audit logs are stored in the dedicated central schema "THINKON_AUDIT"."SYS_AUDIT_LOG"
        return Task.FromResult(this.SysAuditLogs.AsQueryable());
    }

    public Task<IQueryable<SysAuditLog>> GetAllAuditLogsAsync()
    {
        // Audit logs are stored in the dedicated central schema "THINKON_AUDIT"."SYS_AUDIT_LOG"
        return Task.FromResult(this.SysAuditLogs.AsQueryable());
    }

    // Core entities
    public DbSet<SysRole> SysRoles => Set<SysRole>();
    public DbSet<SysCurrency> SysCurrencies => Set<SysCurrency>();
    public DbSet<SysCompany> SysCompanies => Set<SysCompany>();
    public DbSet<SysBranch> SysBranches => Set<SysBranch>();
    public DbSet<SysUser> SysUsers => Set<SysUser>();
    public DbSet<SysFiscalYear> SysFiscalYears => Set<SysFiscalYear>();
    public DbSet<SysApiCategory> SysApiCategories => Set<SysApiCategory>();
    public DbSet<SysApiEndpoint> SysApiEndpoints => Set<SysApiEndpoint>();

    // Permission system entities
    public DbSet<SysSuperAdmin> SysSuperAdmins => Set<SysSuperAdmin>();
    public DbSet<SysSystem> SysSystems => Set<SysSystem>();
    public DbSet<SysScreen> SysScreens => Set<SysScreen>();
    public DbSet<SysFeature> SysFeatures => Set<SysFeature>();
    public DbSet<SysUserRole> SysUserRoles => Set<SysUserRole>();
    public DbSet<SysUserBranch> SysUserBranches => Set<SysUserBranch>();

    // Branch-system provisioning
    public DbSet<SysBranchSystem> SysBranchSystems => Set<SysBranchSystem>();
    public DbSet<SysBranchScreen> SysBranchScreens => Set<SysBranchScreen>();
    public DbSet<SysBranchFeature> SysBranchFeatures => Set<SysBranchFeature>();

    // Company-level permissions (tenant schemas)
    public DbSet<SysRoleScreenPermission> SysRoleScreenPermissions => Set<SysRoleScreenPermission>();
    public DbSet<SysUserScreenPermission> SysUserScreenPermissions => Set<SysUserScreenPermission>();

    // Ticket system entities
    public DbSet<SysRequestTicket> SysRequestTickets => Set<SysRequestTicket>();
    public DbSet<SysTicketType> SysTicketTypes => Set<SysTicketType>();
    public DbSet<SysTicketPriority> SysTicketPriorities => Set<SysTicketPriority>();
    public DbSet<SysTicketStatus> SysTicketStatuses => Set<SysTicketStatus>();
    public DbSet<SysTicketCategory> SysTicketCategories => Set<SysTicketCategory>();
    public DbSet<SysTicketComment> SysTicketComments => Set<SysTicketComment>();
    public DbSet<SysTicketAttachment> SysTicketAttachments => Set<SysTicketAttachment>();
    public DbSet<SysTicketConfig> SysTicketConfigs => Set<SysTicketConfig>();

    // Saved search / search analytics
    public DbSet<SysSavedSearch> SysSavedSearches => Set<SysSavedSearch>();
    public DbSet<SysSearchAnalytics> SysSearchAnalytics => Set<SysSearchAnalytics>();

    // Document management
    public DbSet<SysDocument> SysDocuments => Set<SysDocument>();

    // Audit log (mapped to THINKON_AUDIT.SYS_AUDIT_LOG)
    public DbSet<SysAuditLog> SysAuditLogs => Set<SysAuditLog>();

    // Security monitoring
    public DbSet<SysSecurityThreat> SysSecurityThreats => Set<SysSecurityThreat>();
    public DbSet<SysFailedLogin> SysFailedLogins => Set<SysFailedLogin>();

    // Performance monitoring
    public DbSet<SysPerformanceMetric> SysPerformanceMetrics => Set<SysPerformanceMetric>();
    public DbSet<SysSlowQuery> SysSlowQueries => Set<SysSlowQuery>();

    // Scheduled reporting
    public DbSet<SysReportSchedule> SysReportSchedules => Set<SysReportSchedule>();

    // System Settings & Validation
    public DbSet<SysCode> SysCodes => Set<SysCode>();
    public DbSet<SysSetting> SysSettings => Set<SysSetting>();
    public DbSet<SysFieldValidationRule> SysFieldValidationRules => Set<SysFieldValidationRule>();

    // General ledger / chart of accounts & vouchers (tenant schemas)
    public DbSet<GlAccount> GlAccounts => Set<GlAccount>();
    public DbSet<GlAccountBranch> GlAccountBranches => Set<GlAccountBranch>();
    public DbSet<GlAccountStructureConfig> GlAccountStructureConfigs => Set<GlAccountStructureConfig>();
    public DbSet<GlCostCenter> GlCostCenters => Set<GlCostCenter>();
    public DbSet<GlVoucherType> GlVoucherTypes => Set<GlVoucherType>();
    public DbSet<GlVoucherHeader> GlVoucherHeaders => Set<GlVoucherHeader>();
    public DbSet<GlVoucherDetail> GlVoucherDetails => Set<GlVoucherDetail>();
    public DbSet<GlVoucherSerial> GlVoucherSerials => Set<GlVoucherSerial>();
    public DbSet<GlFiscalPeriod> GlFiscalPeriods => Set<GlFiscalPeriod>();
    public DbSet<GlAccountBalance> GlAccountBalances => Set<GlAccountBalance>();
    public DbSet<Customer> Customers => Set<Customer>();
    public DbSet<Vendor> Vendors => Set<Vendor>();
    public DbSet<ArSubledgerTransaction> ArSubledgerTransactions => Set<ArSubledgerTransaction>();
    public DbSet<ArCashApplication> ArCashApplications => Set<ArCashApplication>();
    public DbSet<ApSubledgerTransaction> ApSubledgerTransactions => Set<ApSubledgerTransaction>();
    public DbSet<ApCashApplication> ApCashApplications => Set<ApCashApplication>();
    public DbSet<GlPdcRegister> PdcRegisters => Set<GlPdcRegister>();
    public DbSet<GlPostingRule> PostingRules => Set<GlPostingRule>();
    public DbSet<BankAccount> BankAccounts => Set<BankAccount>();
    public DbSet<CashRegister> CashRegisters => Set<CashRegister>();
    public DbSet<BankReconciliation> BankReconciliations => Set<BankReconciliation>();
    public DbSet<BankStatementLine> BankStatementLines => Set<BankStatementLine>();

    // Opening balance staging (dedicated OB table)
    public DbSet<GlOpeningBalanceHeader> GlOpeningBalanceHeaders => Set<GlOpeningBalanceHeader>();
    public DbSet<GlOpeningBalanceDetail> GlOpeningBalanceDetails => Set<GlOpeningBalanceDetail>();

    // Tax Engine (Tax master data & audit transactions)
    public DbSet<TaxCategory> TaxCategories => Set<TaxCategory>();
    public DbSet<TaxRate> TaxRates => Set<TaxRate>();
    public DbSet<TaxGroup> TaxGroups => Set<TaxGroup>();
    public DbSet<TaxGroupItem> TaxGroupItems => Set<TaxGroupItem>();
    public DbSet<TaxTransaction> TaxTransactions => Set<TaxTransaction>();

    // Inventory & Unified Trade Documents Engine
    public DbSet<TrxDocType> TrxDocTypes => Set<TrxDocType>();
    public DbSet<TrxTransactionType> TrxTransactionTypes => Set<TrxTransactionType>();
    public DbSet<InvItemGroup> InvItemGroups => Set<InvItemGroup>();
    public DbSet<InvItem> InvItems => Set<InvItem>();
    public DbSet<InvItemUomConversion> InvItemUomConversions => Set<InvItemUomConversion>();
    public DbSet<InvItemBarcode> InvItemBarcodes => Set<InvItemBarcode>();
    public DbSet<InvBomHeader> InvBomHeaders => Set<InvBomHeader>();
    public DbSet<InvBomLine> InvBomLines => Set<InvBomLine>();
    public DbSet<InvWarehouse> InvWarehouses => Set<InvWarehouse>();
    public DbSet<InvZone> InvZones => Set<InvZone>();
    public DbSet<InvBin> InvBins => Set<InvBin>();
    public DbSet<InvStockLedgerEntry> InvStockLedgerEntries => Set<InvStockLedgerEntry>();
    public DbSet<InvStockBalance> InvStockBalances => Set<InvStockBalance>();
    public DbSet<InvCostLayer> InvCostLayers => Set<InvCostLayer>();
    public DbSet<InvLotMaster> InvLotMasters => Set<InvLotMaster>();
    public DbSet<InvSerialMaster> InvSerialMasters => Set<InvSerialMaster>();
    public DbSet<InvReservation> InvReservations => Set<InvReservation>();
    public DbSet<TrxDocumentHeader> TrxDocumentHeaders => Set<TrxDocumentHeader>();
    public DbSet<TrxDocumentLine> TrxDocumentLines => Set<TrxDocumentLine>();
    public DbSet<TrxDocumentSerial> TrxDocumentSerials => Set<TrxDocumentSerial>();
    public DbSet<InvCountSession> InvCountSessions => Set<InvCountSession>();
    public DbSet<InvCountLine> InvCountLines => Set<InvCountLine>();
    public DbSet<InvOpeningBatch> InvOpeningBatches => Set<InvOpeningBatch>();
    public DbSet<InvOpeningLine> InvOpeningLines => Set<InvOpeningLine>();

    // Universal Dynamic Translation
    public DbSet<SysEntityTranslation> EntityTranslations => Set<SysEntityTranslation>();

    // High-Performance Database Views
    public DbSet<GlAccountStatementView> GlAccountStatementViews => Set<GlAccountStatementView>();
    public DbSet<InventoryValuationView> InventoryValuationViews => Set<InventoryValuationView>();
    public DbSet<ArAgingAnalysisView> ArAgingAnalysisViews => Set<ArAgingAnalysisView>();
    public DbSet<ApAgingAnalysisView> ApAgingAnalysisViews => Set<ApAgingAnalysisView>();
    public DbSet<SalesInvoiceProfitabilityView> SalesInvoiceProfitabilityViews => Set<SalesInvoiceProfitabilityView>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        // Apply all entity configurations in Infrastructure assembly automatically
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(OracleDbContext).Assembly);
    }

}
