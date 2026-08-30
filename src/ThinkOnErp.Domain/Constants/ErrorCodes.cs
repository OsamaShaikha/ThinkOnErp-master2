namespace ThinkOnErp.Domain.Constants;

/// <summary>
/// Machine-readable error and validation codes stored in SYS_CODE (CODE_MGR = 30).
/// These codes are resolved dynamically into localized human-readable messages (Arabic, English, etc.)
/// based on the client's Accept-Language header.
/// </summary>
public static class ErrorCodes
{
    public const int CodeMgr = 30;

    // Generic Field Validation Errors
    public const string FieldRequired = "ERR_FIELD_REQUIRED";
    public const string InvalidFormat = "ERR_INVALID_FORMAT";
    public const string MinLength = "ERR_MIN_LENGTH";
    public const string MaxLength = "ERR_MAX_LENGTH";
    public const string OutOfRange = "ERR_OUT_OF_RANGE";
    public const string PositiveNumberRequired = "ERR_POSITIVE_NUMBER_REQUIRED";
    public const string InvalidDate = "ERR_INVALID_DATE";
    public const string DateRangeInvalid = "ERR_DATE_RANGE_INVALID";
    public const string InvalidDateRange = "ERR_DATE_RANGE_INVALID";
    public const string DuplicateValue = "ERR_DUPLICATE_VALUE";

    // Accounting & GL Specific Errors
    public const string InvalidAccountCode = "ERR_INVALID_ACCOUNT_CODE";
    public const string GlAccountNotFound = "ERR_GL_ACCOUNT_NOT_FOUND";
    public const string ParentAccountNotFound = "ERR_PARENT_ACCOUNT_NOT_FOUND";
    public const string AccountHasChildren = "ERR_ACCOUNT_HAS_CHILDREN";
    public const string AccountHasTransactions = "ERR_ACCOUNT_HAS_TRANSACTIONS";
    public const string AccountLevelMismatch = "ERR_ACCOUNT_LEVEL_MISMATCH";
    public const string AccountBalanceTypeInvalid = "ERR_ACCOUNT_BALANCE_TYPE_INVALID";
    public const string AccountTypeInvalid = "ERR_ACCOUNT_TYPE_INVALID";

    // Voucher & Journal Errors
    public const string UnbalancedVoucher = "ERR_UNBALANCED_VOUCHER";
    public const string EmptyVoucherLines = "ERR_EMPTY_VOUCHER_LINES";
    public const string InvalidExchangeRate = "ERR_INVALID_EXCHANGE_RATE";
    public const string VoucherAlreadyPosted = "ERR_VOUCHER_ALREADY_POSTED";
    public const string VoucherNotFound = "ERR_VOUCHER_NOT_FOUND";
    public const string LineDebitCreditConflict = "ERR_LINE_DEBIT_CREDIT_CONFLICT";

    // Tax & Compliance Errors
    public const string InvalidTaxNumber = "ERR_INVALID_TAX_NUMBER";
    public const string TaxCategoryNotFound = "ERR_TAX_CATEGORY_NOT_FOUND";
    public const string TaxRateNotFound = "ERR_TAX_RATE_NOT_FOUND";
    public const string InvalidTaxPercent = "ERR_INVALID_TAX_PERCENT";
    public const string TaxGroupNotFound = "ERR_TAX_GROUP_NOT_FOUND";

    // Banking & Cash & PDC Errors
    public const string BankAccountNotFound = "ERR_BANK_ACCOUNT_NOT_FOUND";
    public const string DuplicateBankAccount = "ERR_DUPLICATE_BANK_ACCOUNT";
    public const string CashRegisterNotFound = "ERR_CASH_REGISTER_NOT_FOUND";
    public const string DuplicateCashRegister = "ERR_DUPLICATE_CASH_REGISTER";
    public const string ChequeNumberRequired = "ERR_CHEQUE_NUMBER_REQUIRED";
    public const string DuplicateChequeNumber = "ERR_DUPLICATE_CHEQUE_NUMBER";
    public const string InvalidChequeAmount = "ERR_INVALID_CHEQUE_AMOUNT";
    public const string PdcNotFound = "ERR_PDC_NOT_FOUND";
    public const string BankReconciliationNotFound = "ERR_BANK_RECONCILIATION_NOT_FOUND";

    // Party (Customer & Vendor) Errors
    public const string CustomerNotFound = "ERR_CUSTOMER_NOT_FOUND";
    public const string DuplicateCustomerCode = "ERR_DUPLICATE_CUSTOMER_CODE";
    public const string VendorNotFound = "ERR_VENDOR_NOT_FOUND";
    public const string DuplicateVendorCode = "ERR_DUPLICATE_VENDOR_CODE";
    public const string CreditLimitExceeded = "ERR_CREDIT_LIMIT_EXCEEDED";

    // Fiscal Period & Closing Errors
    public const string FiscalYearNotFound = "ERR_FISCAL_YEAR_NOT_FOUND";
    public const string FiscalYearAlreadyClosed = "ERR_FISCAL_YEAR_ALREADY_CLOSED";
    public const string FiscalPeriodNotFound = "ERR_FISCAL_PERIOD_NOT_FOUND";
    public const string FiscalPeriodClosed = "ERR_FISCAL_PERIOD_CLOSED";
    public const string FiscalPeriodAlreadyExists = "ERR_FISCAL_PERIOD_ALREADY_EXISTS";

    // Security & Generic Entity Errors
    public const string EntityNotFound = "ERR_ENTITY_NOT_FOUND";
    public const string UnauthorizedAction = "ERR_UNAUTHORIZED_ACTION";
    public const string OperationFailed = "ERR_OPERATION_FAILED";
    public const string SystemError = "ERR_OPERATION_FAILED";
    public const string InvalidCredentials = "ERR_INVALID_CREDENTIALS";
    public const string ValidationError = "ERR_VALIDATION_ERROR";
    public const string OpeningBalanceNotFound = "ERR_OPENING_BALANCE_NOT_FOUND";
    public const string CostCenterNotFound = "ERR_COST_CENTER_NOT_FOUND";
    public const string CompanyContextRequired = "ERR_COMPANY_CONTEXT_REQUIRED";
    public const string CompanyNotFound = "ERR_COMPANY_NOT_FOUND";
    public const string BranchNotFound = "ERR_BRANCH_NOT_FOUND";
}
