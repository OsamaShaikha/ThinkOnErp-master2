namespace ThinkOnErp.Domain.Constants;

/// <summary>
/// Machine-readable response and success codes stored in SYS_CODE (CODE_MGR = 31).
/// These codes are resolved dynamically into localized human-readable messages (Arabic, English, etc.)
/// based on the client's Accept-Language header.
/// </summary>
public static class ResponseCodes
{
    public const int CodeMgr = 31;

    // Generic CRUD Operations
    public const string OperationSuccessful = "RES_OPERATION_SUCCESSFUL";
    public const string RecordCreated = "RES_RECORD_CREATED";
    public const string RecordUpdated = "RES_RECORD_UPDATED";
    public const string RecordDeleted = "RES_RECORD_DELETED";
    public const string DataRetrieved = "RES_DATA_RETRIEVED";
    public const string StatusUpdated = "RES_STATUS_UPDATED";
    public const string FileUploaded = "RES_FILE_UPLOADED";
    public const string ExportCompleted = "RES_EXPORT_COMPLETED";
    public const string CacheCleared = "RES_CACHE_CLEARED";

    // Module 1: GL & Chart of Accounts
    public const string AccountCreated = "RES_ACCOUNT_CREATED";
    public const string AccountUpdated = "RES_ACCOUNT_UPDATED";
    public const string AccountDeleted = "RES_ACCOUNT_DELETED";
    public const string CoaTreeRetrieved = "RES_COA_TREE_RETRIEVED";
    public const string CategoriesRetrieved = "RES_CATEGORIES_RETRIEVED";
    public const string AccountBalancesRetrieved = "RES_ACCOUNT_BALANCES_RETRIEVED";
    public const string TrialBalanceGenerated = "RES_TRIAL_BALANCE_GENERATED";
    public const string AccountStatementGenerated = "RES_ACCOUNT_STATEMENT_GENERATED";
    public const string IncomeStatementGenerated = "RES_INCOME_STATEMENT_GENERATED";
    public const string BalanceSheetGenerated = "RES_BALANCE_SHEET_GENERATED";
    public const string AgingReportGenerated = "RES_AGING_REPORT_GENERATED";
    public const string CoaImported = "RES_COA_IMPORTED";
    public const string CoaImportedSuccessfully = "RES_COA_IMPORTED_SUCCESSFULLY";
    public const string CoaImportValidated = "RES_COA_IMPORT_VALIDATED";
    public const string CostCentersRetrieved = "RES_COST_CENTERS_RETRIEVED";
    public const string CostCenterTreeRetrieved = "RES_COST_CENTER_TREE_RETRIEVED";
    public const string CostCenterCreated = "RES_COST_CENTER_CREATED";
    public const string CostCenterUpdated = "RES_COST_CENTER_UPDATED";
    public const string CostCenterDeleted = "RES_COST_CENTER_DELETED";
    public const string OpeningBalanceRetrieved = "RES_OPENING_BALANCE_RETRIEVED";
    public const string OpeningBalanceCreated = "RES_OPENING_BALANCE_CREATED";
    public const string OpeningBalanceConfirmed = "RES_OPENING_BALANCE_CONFIRMED";
    public const string OpeningBalancesSaved = "RES_OPENING_BALANCES_SAVED";
    public const string OpeningBalancesPosted = "RES_OPENING_BALANCES_POSTED";

    // Module 2: Journal Entries & Vouchers
    public const string VoucherCreated = "RES_VOUCHER_CREATED";
    public const string VoucherUpdated = "RES_VOUCHER_UPDATED";
    public const string VoucherPosted = "RES_VOUCHER_POSTED";
    public const string VoucherCancelled = "RES_VOUCHER_CANCELLED";
    public const string VoucherReversed = "RES_VOUCHER_REVERSED";
    public const string VouchersRetrieved = "RES_VOUCHERS_RETRIEVED";
    public const string VoucherDetailsRetrieved = "RES_VOUCHER_DETAILS_RETRIEVED";

    // Module 3: Subledger & Parties (Customers & Vendors)
    public const string CustomerCreated = "RES_CUSTOMER_CREATED";
    public const string CustomerUpdated = "RES_CUSTOMER_UPDATED";
    public const string CustomerDeleted = "RES_CUSTOMER_DELETED";
    public const string CustomersRetrieved = "RES_CUSTOMERS_RETRIEVED";
    public const string VendorCreated = "RES_VENDOR_CREATED";
    public const string VendorUpdated = "RES_VENDOR_UPDATED";
    public const string VendorDeleted = "RES_VENDOR_DELETED";
    public const string VendorsRetrieved = "RES_VENDORS_RETRIEVED";
    public const string SubledgerTransactionsRetrieved = "RES_SUBLEDGER_TRANSACTIONS_RETRIEVED";
    public const string CashApplied = "RES_CASH_APPLIED";
    public const string CashUnapplied = "RES_CASH_UNAPPLIED";
    public const string SubledgerReconciled = "RES_SUBLEDGER_RECONCILED";
    public const string SubledgerStatementGenerated = "RES_SUBLEDGER_STATEMENT_GENERATED";
    public const string SubledgerAgingGenerated = "RES_SUBLEDGER_AGING_GENERATED";
    public const string PostingRuleCreated = "RES_POSTING_RULE_CREATED";
    public const string PostingRuleUpdated = "RES_POSTING_RULE_UPDATED";

    // Module 4: Treasury, Banking & PDC
    public const string BankAccountCreated = "RES_BANK_ACCOUNT_CREATED";
    public const string BankAccountUpdated = "RES_BANK_ACCOUNT_UPDATED";
    public const string BankAccountsRetrieved = "RES_BANK_ACCOUNTS_RETRIEVED";
    public const string BankReconciliationCreated = "RES_BANK_RECONCILIATION_CREATED";
    public const string BankReconciliationAutoMatched = "RES_BANK_RECONCILIATION_AUTO_MATCHED";
    public const string BankReconciliationFinalized = "RES_BANK_RECONCILIATION_FINALIZED";
    public const string CashRegisterCreated = "RES_CASH_REGISTER_CREATED";
    public const string CashRegisterUpdated = "RES_CASH_REGISTER_UPDATED";
    public const string CashRegistersRetrieved = "RES_CASH_REGISTERS_RETRIEVED";
    public const string PdcIssued = "RES_PDC_ISSUED";
    public const string PdcReceived = "RES_PDC_RECEIVED";
    public const string PdcDeposited = "RES_PDC_DEPOSITED";
    public const string PdcCleared = "RES_PDC_CLEARED";
    public const string PdcBounced = "RES_PDC_BOUNCED";
    public const string PaymentCreated = "RES_PAYMENT_CREATED";
    public const string PaymentPosted = "RES_PAYMENT_POSTED";
    public const string PaymentVouchersRetrieved = "RES_PAYMENT_VOUCHERS_RETRIEVED";
    public const string PaymentVoucherCreated = "RES_PAYMENT_VOUCHER_CREATED";
    public const string PaymentVoucherUpdated = "RES_PAYMENT_VOUCHER_UPDATED";
    public const string PaymentVoucherDeleted = "RES_PAYMENT_VOUCHER_DELETED";
    public const string ReceiptCreated = "RES_RECEIPT_CREATED";
    public const string ReceiptPosted = "RES_RECEIPT_POSTED";
    public const string ReceiptVouchersRetrieved = "RES_RECEIPT_VOUCHERS_RETRIEVED";
    public const string ReceiptVoucherCreated = "RES_RECEIPT_VOUCHER_CREATED";
    public const string ReceiptVoucherUpdated = "RES_RECEIPT_VOUCHER_UPDATED";
    public const string ReceiptVoucherDeleted = "RES_RECEIPT_VOUCHER_DELETED";

    // Module 5: Tax Engine & Compliance
    public const string TaxCategoryCreated = "RES_TAX_CATEGORY_CREATED";
    public const string TaxRateCreated = "RES_TAX_RATE_CREATED";
    public const string TaxRateUpdated = "RES_TAX_RATE_UPDATED";
    public const string TaxGroupCreated = "RES_TAX_GROUP_CREATED";
    public const string TaxReturnCalculated = "RES_TAX_RETURN_CALCULATED";
    public const string TaxReturnApproved = "RES_TAX_RETURN_APPROVED";
    public const string ComplianceReportGenerated = "RES_COMPLIANCE_REPORT_GENERATED";

    // Module 6: Fiscal Years, Periods & Closing
    public const string FiscalYearsRetrieved = "RES_FISCAL_YEARS_RETRIEVED";
    public const string FiscalYearCreated = "RES_FISCAL_YEAR_CREATED";
    public const string FiscalYearUpdated = "RES_FISCAL_YEAR_UPDATED";
    public const string FiscalYearDeleted = "RES_FISCAL_YEAR_DELETED";
    public const string FiscalYearClosed = "RES_FISCAL_YEAR_CLOSED";
    public const string FiscalPeriodsRetrieved = "RES_FISCAL_PERIODS_RETRIEVED";
    public const string FiscalPeriodsGenerated = "RES_FISCAL_PERIODS_GENERATED";
    public const string FiscalPeriodOpened = "RES_FISCAL_PERIOD_OPENED";
    public const string FiscalPeriodSoftClosed = "RES_FISCAL_PERIOD_SOFT_CLOSED";
    public const string FiscalPeriodHardClosed = "RES_FISCAL_PERIOD_HARD_CLOSED";
    public const string FiscalPeriodReopened = "RES_FISCAL_PERIOD_REOPENED";
    public const string FiscalPeriodClosed = "RES_FISCAL_PERIOD_CLOSED";
    public const string MonthEndClosingCompleted = "RES_MONTH_END_CLOSING_COMPLETED";
    public const string YearEndClosingCompleted = "RES_YEAR_END_CLOSING_COMPLETED";

    // Module 7: Auth, SuperAdmin & Multi-Tenancy
    public const string LoginSuccessful = "RES_LOGIN_SUCCESSFUL";
    public const string LogoutSuccessful = "RES_LOGOUT_SUCCESSFUL";
    public const string TokenRefreshed = "RES_TOKEN_REFRESHED";
    public const string PasswordChanged = "RES_PASSWORD_CHANGED";
    public const string CompanyCreated = "RES_COMPANY_CREATED";
    public const string CompanyUpdated = "RES_COMPANY_UPDATED";
    public const string CompanyProvisioned = "RES_COMPANY_PROVISIONED";
    public const string BranchCreated = "RES_BRANCH_CREATED";
    public const string BranchUpdated = "RES_BRANCH_UPDATED";
    public const string BranchAssigned = "RES_BRANCH_ASSIGNED";
    public const string RoleCreated = "RES_ROLE_CREATED";
    public const string RoleUpdated = "RES_ROLE_UPDATED";
    public const string PermissionsAssigned = "RES_PERMISSIONS_ASSIGNED";
    public const string ScreenRevoked = "RES_SCREEN_REVOKED";
    public const string ScreenReAllowed = "RES_SCREEN_RE_ALLOWED";

    // Module 8: Auditing & Security
    public const string AuditLogsRetrieved = "RES_AUDIT_LOGS_RETRIEVED";
    public const string AuditArchived = "RES_AUDIT_ARCHIVED";
    public const string AuditIntegrityVerified = "RES_AUDIT_INTEGRITY_VERIFIED";
    public const string AlertRuleCreated = "RES_ALERT_RULE_CREATED";
    public const string AlertRuleUpdated = "RES_ALERT_RULE_UPDATED";
    public const string AlertDismissed = "RES_ALERT_DISMISSED";

    // Module 9: System Settings, Documents & Validation Rules
    public const string SysSettingUpdated = "RES_SYS_SETTING_UPDATED";
    public const string DocumentUploaded = "RES_DOCUMENT_UPLOADED";
    public const string DocumentDeleted = "RES_DOCUMENT_DELETED";
    public const string SavedSearchSaved = "RES_SAVED_SEARCH_SAVED";
    public const string ValidationRuleCreated = "RES_VALIDATION_RULE_CREATED";
    public const string ValidationRuleUpdated = "RES_VALIDATION_RULE_UPDATED";
    public const string ValidationRuleDeleted = "RES_VALIDATION_RULE_DELETED";

    // Module 10: Unified Trade Documents, Inventory & BOM
    public const string ItemCreated = "RES_ITEM_CREATED";
    public const string ItemUpdated = "RES_ITEM_UPDATED";
    public const string ItemDeleted = "RES_ITEM_DELETED";
    public const string ItemsRetrieved = "RES_ITEMS_RETRIEVED";
    public const string ItemDetailsRetrieved = "RES_ITEM_DETAILS_RETRIEVED";
    public const string ItemUomAdded = "RES_ITEM_UOM_ADDED";
    public const string ItemUomDeleted = "RES_ITEM_UOM_DELETED";
    public const string ItemBarcodeAdded = "RES_ITEM_BARCODE_ADDED";
    public const string ItemBarcodeDeleted = "RES_ITEM_BARCODE_DELETED";

    public const string WarehouseCreated = "RES_WAREHOUSE_CREATED";
    public const string WarehouseUpdated = "RES_WAREHOUSE_UPDATED";
    public const string WarehouseDeleted = "RES_WAREHOUSE_DELETED";
    public const string WarehousesRetrieved = "RES_WAREHOUSES_RETRIEVED";
    public const string WarehouseDetailsRetrieved = "RES_WAREHOUSE_DETAILS_RETRIEVED";
    public const string ZoneCreated = "RES_ZONE_CREATED";
    public const string ZoneUpdated = "RES_ZONE_UPDATED";
    public const string ZoneDeleted = "RES_ZONE_DELETED";
    public const string BinCreated = "RES_BIN_CREATED";
    public const string BinUpdated = "RES_BIN_UPDATED";
    public const string BinDeleted = "RES_BIN_DELETED";
    public const string WarehouseStockRetrieved = "RES_WAREHOUSE_STOCK_RETRIEVED";

    public const string DocumentCreated = "RES_DOCUMENT_CREATED";
    public const string DocumentUpdated = "RES_DOCUMENT_UPDATED";
    public const string TrxDocumentDeleted = "RES_TRX_DOCUMENT_DELETED";
    public const string DocumentsRetrieved = "RES_DOCUMENTS_RETRIEVED";
    public const string DocumentDetailsRetrieved = "RES_DOCUMENT_DETAILS_RETRIEVED";
    public const string DocumentPosted = "RES_DOCUMENT_POSTED";
    public const string DocumentCancelled = "RES_DOCUMENT_CANCELLED";

    public const string ItemGroupCreated = "RES_ITEM_GROUP_CREATED";
    public const string ItemGroupUpdated = "RES_ITEM_GROUP_UPDATED";
    public const string ItemGroupDeleted = "RES_ITEM_GROUP_DELETED";
    public const string ItemGroupsRetrieved = "RES_ITEM_GROUPS_RETRIEVED";
    public const string ItemGroupDetailsRetrieved = "RES_ITEM_GROUP_DETAILS_RETRIEVED";

    public const string BomCreated = "RES_BOM_CREATED";
    public const string BomUpdated = "RES_BOM_UPDATED";
    public const string BomDeleted = "RES_BOM_DELETED";
    public const string BomsRetrieved = "RES_BOMS_RETRIEVED";
    public const string BomDetailsRetrieved = "RES_BOM_DETAILS_RETRIEVED";

    public const string InvOpeningBalanceCreated = "RES_INV_OPENING_BALANCE_CREATED";
    public const string OpeningBalanceUpdated = "RES_OPENING_BALANCE_UPDATED";
    public const string OpeningBalanceDeleted = "RES_OPENING_BALANCE_DELETED";
    public const string OpeningBalancesRetrieved = "RES_OPENING_BALANCES_RETRIEVED";
    public const string OpeningBalanceDetailsRetrieved = "RES_OPENING_BALANCE_DETAILS_RETRIEVED";
    public const string OpeningBalancePosted = "RES_OPENING_BALANCE_POSTED";

    public const string StockMovementPosted = "RES_STOCK_MOVEMENT_POSTED";
    public const string StockMovementsRetrieved = "RES_STOCK_MOVEMENTS_RETRIEVED";
    public const string StockBalancesRetrieved = "RES_STOCK_BALANCES_RETRIEVED";
    public const string StockValuationGenerated = "RES_STOCK_VALUATION_GENERATED";
    public const string AtpCalculated = "RES_ATP_CALCULATED";
    public const string StockReconciliationCompleted = "RES_STOCK_RECONCILIATION_COMPLETED";

    public const string DocTypeCreated = "RES_DOC_TYPE_CREATED";
    public const string DocTypeUpdated = "RES_DOC_TYPE_UPDATED";
    public const string DocTypeDeleted = "RES_DOC_TYPE_DELETED";
    public const string DocTypesRetrieved = "RES_DOC_TYPES_RETRIEVED";
    public const string DocTypeDetailsRetrieved = "RES_DOC_TYPE_DETAILS_RETRIEVED";

    public const string TrxTypeCreated = "RES_TRX_TYPE_CREATED";
    public const string TrxTypeUpdated = "RES_TRX_TYPE_UPDATED";
    public const string TrxTypeDeleted = "RES_TRX_TYPE_DELETED";
    public const string TrxTypesRetrieved = "RES_TRX_TYPES_RETRIEVED";
    public const string TrxTypeDetailsRetrieved = "RES_TRX_TYPE_DETAILS_RETRIEVED";
}
