using System.Collections.Concurrent;
using System.Globalization;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using ThinkOnErp.Application.Services.Localization;
using ThinkOnErp.Domain.Constants;
using ThinkOnErp.Infrastructure.Data;

namespace ThinkOnErp.Infrastructure.Services.Localization;

public class DbLocalizationService : ILocalizationService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly IHttpContextAccessor _httpContextAccessor;
    private readonly ILogger<DbLocalizationService> _logger;

    // Cache: (CodeValue, LangId) -> Localized Message
    private readonly ConcurrentDictionary<(string CodeValue, int LangId), string> _messageCache = new();
    private bool _isInitialized;
    private readonly object _initLock = new();

    // Default built-in fallback translations if DB row is not yet seeded
    private static readonly Dictionary<(string CodeValue, int LangId), string> Fallbacks = new()
    {
        { (ErrorCodes.FieldRequired, 1), "حقل {0} مطلوب ولا يمكن تركه فارغاً." },
        { (ErrorCodes.FieldRequired, 2), "The field {0} is required." },
        { (ErrorCodes.InvalidFormat, 1), "صيغة حقل {0} غير صالحة." },
        { (ErrorCodes.InvalidFormat, 2), "The format of field {0} is invalid." },
        { (ErrorCodes.MinLength, 1), "الحد الأدنى لطول {0} هو {1} خانات." },
        { (ErrorCodes.MinLength, 2), "The minimum length for {0} is {1} characters." },
        { (ErrorCodes.MaxLength, 1), "الحد الأقصى لطول {0} هو {1} خانات." },
        { (ErrorCodes.MaxLength, 2), "The maximum length for {0} is {1} characters." },
        { (ErrorCodes.OutOfRange, 1), "قيمة {0} يجب أن تكون بين {1} و {2}." },
        { (ErrorCodes.OutOfRange, 2), "The value of {0} must be between {1} and {2}." },
        { (ErrorCodes.PositiveNumberRequired, 1), "قيمة {0} يجب أن تكون رقماً موجباً أكبر من الصفر." },
        { (ErrorCodes.PositiveNumberRequired, 2), "The value of {0} must be a positive number greater than zero." },

        { (ErrorCodes.InvalidTaxNumber, 1), "الرقم الضريبي غير مطابق للمعيار الضريبي المعتمد." },
        { (ErrorCodes.InvalidTaxNumber, 2), "Tax registration number does not match the mandatory standard." },
        { (ErrorCodes.InvalidAccountCode, 1), "رمز الحساب المالي غير صالح." },
        { (ErrorCodes.InvalidAccountCode, 2), "Account code format is invalid." },
        { (ErrorCodes.GlAccountNotFound, 1), "الحساب المالي المحدد غير موجود في دليل الحسابات." },
        { (ErrorCodes.GlAccountNotFound, 2), "The specified GL account was not found in the chart of accounts." },
        { (ErrorCodes.UnbalancedVoucher, 1), "القيد المحاسبي غير متوازن (مجموع المدين لا يساوي مجموع الدائن)." },
        { (ErrorCodes.UnbalancedVoucher, 2), "Journal voucher is unbalanced (Total Debit != Total Credit)." },
        { (ErrorCodes.EmptyVoucherLines, 1), "لا يمكن حفظ قيد محاسبي بدون أسطر تفصيلية." },
        { (ErrorCodes.EmptyVoucherLines, 2), "Cannot save a voucher without detail lines." },
        { (ErrorCodes.InvalidExchangeRate, 1), "سعر صرف العملة يجب أن يكون أكبر من الصفر." },
        { (ErrorCodes.InvalidExchangeRate, 2), "Currency exchange rate must be greater than zero." },
        { (ErrorCodes.DuplicateBankAccount, 1), "رقم الحساب البنكي مسجل مسبقاً." },
        { (ErrorCodes.DuplicateBankAccount, 2), "Bank account number already exists." },
        { (ErrorCodes.CustomerNotFound, 1), "العميل المحدد غير موجود." },
        { (ErrorCodes.CustomerNotFound, 2), "The specified customer was not found." },
        { (ErrorCodes.VendorNotFound, 1), "المورد المحدد غير موجود." },
        { (ErrorCodes.VendorNotFound, 2), "The specified vendor was not found." },
        { (ErrorCodes.DuplicateChequeNumber, 1), "رقم الشيك مسجل مسبقاً في هذا الحساب." },
        { (ErrorCodes.InvalidTaxPercent, 1), "نسبة الضريبة يجب أن تكون بين 0 و 100%." },
        { (ErrorCodes.InvalidTaxPercent, 2), "Tax rate percentage must be between 0 and 100%." },
        { (ErrorCodes.AccountBalanceTypeInvalid, 1), "طبيعة رصيد الحساب غير صحيحة (يجب أن تكون مدين 'D' أو دائن 'C')." },
        { (ErrorCodes.AccountBalanceTypeInvalid, 2), "Normal balance type is invalid (must be Debit 'D' or Credit 'C')." },
        { (ErrorCodes.AccountTypeInvalid, 1), "نوع الحساب المالي غير صحيح (يجب أن يكون رئيسي 'HEADER' أو تحليلي 'DETAIL')." },
        { (ErrorCodes.AccountTypeInvalid, 2), "Account type is invalid (must be 'HEADER' or 'DETAIL')." },
        { (ErrorCodes.ChequeNumberRequired, 1), "رقم الشيك إجباري." },
        { (ErrorCodes.ChequeNumberRequired, 2), "Cheque number is mandatory." },
        { (ErrorCodes.InvalidChequeAmount, 1), "مبلغ الشيك يجب أن يكون أكبر من الصفر." },
        { (ErrorCodes.InvalidChequeAmount, 2), "Cheque amount must be greater than zero." },
        { (ErrorCodes.FiscalYearAlreadyClosed, 1), "لا يمكن تنفيذ عمليات على سنة مالية مغلقة." },
        { (ErrorCodes.FiscalYearAlreadyClosed, 2), "Cannot perform transactions on a closed fiscal year." },
        { (ErrorCodes.CompanyContextRequired, 1), "يجب تحديد سياق الشركة عبر الترويسة X-Company-Id أو X-Company-Code." },
        { (ErrorCodes.CompanyContextRequired, 2), "Select a company using X-Company-Id or X-Company-Code header." },
        { (ErrorCodes.CompanyNotFound, 1), "الشركة المحددة غير موجودة." },
        { (ErrorCodes.CompanyNotFound, 2), "The specified company was not found." },
        { (ErrorCodes.BranchNotFound, 1), "الفرع المحدد غير موجود." },
        { (ErrorCodes.BranchNotFound, 2), "The specified branch was not found." }
    };

    public DbLocalizationService(
        IServiceScopeFactory scopeFactory,
        IHttpContextAccessor httpContextAccessor,
        ILogger<DbLocalizationService> logger)
    {
        _scopeFactory = scopeFactory;
        _httpContextAccessor = httpContextAccessor;
        _logger = logger;
    }

    public string GetMessage(string errorCode, string? languageCode = null)
    {
        EnsureInitialized();
        var key = NormalizeKey(errorCode);
        var langId = ResolveLanguageId(languageCode);

        if (_messageCache.TryGetValue((key, langId), out var msg))
            return msg;

        // Fallback to English if Arabic is missing or vice versa
        if (langId != 2 && _messageCache.TryGetValue((key, 2), out var enMsg))
            return enMsg;
        if (langId != 1 && _messageCache.TryGetValue((key, 1), out var arMsg))
            return arMsg;

        // Fallback to built-in constants
        if (Fallbacks.TryGetValue((key, langId), out var fallback))
            return fallback;
        if (Fallbacks.TryGetValue((key, 2), out var fallbackEn))
            return fallbackEn;

        return errorCode; // Return machine key or original message as last resort
    }

    public string GetMessage(string errorCode, int languageId)
    {
        EnsureInitialized();
        var key = NormalizeKey(errorCode);
        if (_messageCache.TryGetValue((key, languageId), out var msg))
            return msg;

        if (Fallbacks.TryGetValue((key, languageId), out var fallback))
            return fallback;

        return errorCode;
    }

    private static string NormalizeKey(string rawCode)
    {
        if (string.IsNullOrWhiteSpace(rawCode)) return rawCode;
        var trimmed = rawCode.Trim();

        // If it's already a standard machine key (starts with RES_ or ERR_) return as is
        if (trimmed.StartsWith("RES_", StringComparison.OrdinalIgnoreCase) || 
            trimmed.StartsWith("ERR_", StringComparison.OrdinalIgnoreCase))
        {
            return trimmed.ToUpperInvariant();
        }

        var lower = trimmed.ToLowerInvariant();

        // Common English Success Phrases
        if (lower.Contains("retrieved successfully") || lower.Contains("fetched successfully") || lower.Contains("loaded successfully"))
            return ResponseCodes.DataRetrieved;
        if (lower.Contains("created successfully") || lower.Contains("added successfully"))
            return ResponseCodes.RecordCreated;
        if (lower.Contains("updated successfully") || lower.Contains("modified successfully"))
            return ResponseCodes.RecordUpdated;
        if (lower.Contains("deleted successfully") || lower.Contains("removed successfully"))
            return ResponseCodes.RecordDeleted;
        if (lower.Contains("saved successfully"))
            return ResponseCodes.RecordCreated;
        if (lower.Contains("password changed successfully") || lower.Contains("password reset successfully"))
            return ResponseCodes.RecordUpdated;
        if (lower.Contains("status updated") || lower.Contains("status changed") || lower.Contains("activated successfully") || lower.Contains("deactivated successfully"))
            return ResponseCodes.StatusUpdated;
        if (lower.Contains("assigned successfully") || lower.Contains("re-allowed successfully") || lower.Contains("revoked successfully") || lower.Contains("provisioning state retrieved"))
            return ResponseCodes.DataRetrieved;
        if (lower.Contains("operation completed successfully") || lower.Contains("completed successfully") || lower.Contains("successful"))
            return ResponseCodes.OperationSuccessful;

        // Common English Error Phrases
        if (lower.Contains("not found"))
            return ErrorCodes.EntityNotFound;
        if (lower.Contains("unauthorized") || lower.Contains("access denied") || lower.Contains("forbidden"))
            return ErrorCodes.UnauthorizedAction;
        if (lower.Contains("invalid credentials") || lower.Contains("password is incorrect") || lower.Contains("incorrect password"))
            return ErrorCodes.InvalidCredentials;
        if (lower.Contains("validation error") || lower.Contains("do not match") || lower.Contains("must be") || lower.Contains("is required"))
            return ErrorCodes.ValidationError;
        if (lower.Contains("failed") || lower.Contains("error occurred"))
            return ErrorCodes.OperationFailed;

        return trimmed;
    }

    public string GetMessageWithFormat(string errorCode, string? languageCode = null, params object[] args)
    {
        var raw = GetMessage(errorCode, languageCode);
        if (args.Length == 0) return raw;

        try
        {
            return string.Format(CultureInfo.InvariantCulture, raw, args);
        }
        catch
        {
            return raw;
        }
    }

    public int ResolveLanguageId(string? languageCode)
    {
        if (!string.IsNullOrWhiteSpace(languageCode))
        {
            var code = languageCode.Trim().ToLowerInvariant();
            if (code == "1" || code.StartsWith("ar")) return 1;
            if (code == "2" || code.StartsWith("en")) return 2;
            if (code == "3" || code.StartsWith("fr")) return 3;
            if (code == "4" || code.StartsWith("tr")) return 4;
        }

        var httpContext = _httpContextAccessor.HttpContext;
        if (httpContext != null)
        {
            // 1. Explicit Custom Header (e.g. X-Language: en or X-Language: 2)
            if (httpContext.Request.Headers.TryGetValue("X-Language", out var xLangValues))
            {
                string xLang = xLangValues.ToString().Trim().ToLowerInvariant();
                if (xLang == "1" || xLang.StartsWith("ar")) return 1;
                if (xLang == "2" || xLang.StartsWith("en")) return 2;
                if (xLang == "3" || xLang.StartsWith("fr")) return 3;
                if (xLang == "4" || xLang.StartsWith("tr")) return 4;
            }

            // 2. Check authenticated User Claims (User-Level Language Preference)
            var userLangClaim = httpContext.User.FindFirst("lang")?.Value 
                ?? httpContext.User.FindFirst("defaultLang")?.Value;
            if (!string.IsNullOrWhiteSpace(userLangClaim))
            {
                var lower = userLangClaim.Trim().ToLowerInvariant();
                if (lower == "1" || lower.StartsWith("ar")) return 1;
                if (lower == "2" || lower.StartsWith("en")) return 2;
                if (lower == "3" || lower.StartsWith("fr")) return 3;
                if (lower == "4" || lower.StartsWith("tr")) return 4;
            }

            // 3. Check HTTP Request Accept-Language header (Browser/Client default)
            if (httpContext.Request.Headers.TryGetValue("Accept-Language", out var headerValues))
            {
                string header = headerValues.ToString();
                if (!string.IsNullOrWhiteSpace(header))
                {
                    var lower = header.ToLowerInvariant();
                    if (lower.StartsWith("ar", StringComparison.Ordinal) || lower.Contains("ar-", StringComparison.Ordinal)) return 1;
                    if (lower.StartsWith("en", StringComparison.Ordinal) || lower.Contains("en-", StringComparison.Ordinal)) return 2;
                    if (lower.StartsWith("fr", StringComparison.Ordinal) || lower.Contains("fr-", StringComparison.Ordinal)) return 3;
                    if (lower.StartsWith("tr", StringComparison.Ordinal) || lower.Contains("tr-", StringComparison.Ordinal)) return 4;
                }
            }
        }

        return 1; // Default to Arabic (Lang 1)
    }

    public void RefreshCache()
    {
        lock (_initLock)
        {
            _messageCache.Clear();
            _isInitialized = false;
            EnsureInitialized();
        }
    }

    private void EnsureInitialized()
    {
        if (_isInitialized) return;

        lock (_initLock)
        {
            if (_isInitialized) return;

            try
            {
                using var scope = _scopeFactory.CreateScope();
                var db = scope.ServiceProvider.GetService<OracleDbContext>();
                if (db != null)
                {
                    var codes = db.SysCodes
                        .Where(c => (c.CodeMgr == ErrorCodes.CodeMgr || c.CodeMgr == ResponseCodes.CodeMgr) && c.IsActive == 1)
                        .Select(c => new { c.CodeValue, c.CodeLang, c.CodeDesc })
                        .ToList();

                    foreach (var c in codes)
                    {
                        if (!string.IsNullOrEmpty(c.CodeValue) && !string.IsNullOrEmpty(c.CodeDesc))
                        {
                            _messageCache[(c.CodeValue, c.CodeLang)] = c.CodeDesc;
                        }
                    }

                    _logger.LogInformation("Loaded {Count} localized message keys from SYS_CODE (Errors & Responses)", _messageCache.Count);
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to load translations from SYS_CODE. Fallback strings will be used.");
            }
            finally
            {
                _isInitialized = true;
            }
        }
    }
}
