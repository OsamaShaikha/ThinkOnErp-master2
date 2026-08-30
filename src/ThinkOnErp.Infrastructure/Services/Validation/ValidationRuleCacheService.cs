using System.Collections.Concurrent;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using ThinkOnErp.Application.Services.Validation;
using ThinkOnErp.Domain.Constants;
using ThinkOnErp.Domain.Entities;
using ThinkOnErp.Infrastructure.Data;

namespace ThinkOnErp.Infrastructure.Services.Validation;

public class ValidationRuleCacheService : IValidationRuleCacheService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<ValidationRuleCacheService> _logger;

    // Cache: EntityName (lowercase) -> List of rules
    private readonly ConcurrentDictionary<string, List<SysFieldValidationRule>> _rulesByEntity = new(StringComparer.OrdinalIgnoreCase);
    private bool _isInitialized;
    private readonly object _initLock = new();

    // Standard baseline default validation rules (Fallback if DB table is unpopulated)
    private static readonly List<SysFieldValidationRule> DefaultBaselineRules = new()
    {
        // GlAccount Validation Rules
        new() { EntityName = "GlAccount", FieldName = "AccountCode", RuleType = "REQUIRED", ErrorCode = ErrorCodes.FieldRequired },
        new() { EntityName = "GlAccount", FieldName = "AccountCode", RuleType = "REGEX", RuleValue = @"^[0-9]+(\.[0-9]+)*$", ErrorCode = ErrorCodes.InvalidAccountCode },
        new() { EntityName = "GlAccount", FieldName = "AccountNameAr", RuleType = "REQUIRED", ErrorCode = ErrorCodes.FieldRequired },
        new() { EntityName = "GlAccount", FieldName = "AccountNameAr", RuleType = "MAX_LENGTH", RuleValue = "200", ErrorCode = ErrorCodes.MaxLength },
        new() { EntityName = "GlAccount", FieldName = "NormalBalance", RuleType = "REQUIRED", ErrorCode = ErrorCodes.FieldRequired },
        new() { EntityName = "GlAccount", FieldName = "NormalBalance", RuleType = "REGEX", RuleValue = "^[DCdc]$", ErrorCode = ErrorCodes.AccountBalanceTypeInvalid },
        new() { EntityName = "GlAccount", FieldName = "AccountType", RuleType = "REQUIRED", ErrorCode = ErrorCodes.FieldRequired },
        new() { EntityName = "GlAccount", FieldName = "AccountType", RuleType = "REGEX", RuleValue = "^(HEADER|DETAIL)$", ErrorCode = ErrorCodes.AccountTypeInvalid },

        // Customer Validation Rules
        new() { EntityName = "Customer", FieldName = "CustomerCode", RuleType = "REQUIRED", ErrorCode = ErrorCodes.FieldRequired },
        new() { EntityName = "Customer", FieldName = "CustomerCode", RuleType = "MAX_LENGTH", RuleValue = "50", ErrorCode = ErrorCodes.MaxLength },
        new() { EntityName = "Customer", FieldName = "NameAr", RuleType = "REQUIRED", ErrorCode = ErrorCodes.FieldRequired },
        new() { EntityName = "Customer", FieldName = "CreditLimit", RuleType = "RANGE", RuleValue = "0..999999999", ErrorCode = ErrorCodes.OutOfRange },
        // Saudi Tax Number: 15 digits starting and ending with 3
        new() { EntityName = "Customer", FieldName = "TaxNumber", RuleType = "REGEX", RuleValue = @"^3\d{13}3$", ErrorCode = ErrorCodes.InvalidTaxNumber, CountryCode = "SA" },
        // Jordan Tax Number: 8 digits
        new() { EntityName = "Customer", FieldName = "TaxNumber", RuleType = "REGEX", RuleValue = @"^\d{8}$", ErrorCode = ErrorCodes.InvalidTaxNumber, CountryCode = "JO" },
        // Egypt Tax Number: 9 digits
        new() { EntityName = "Customer", FieldName = "TaxNumber", RuleType = "REGEX", RuleValue = @"^\d{9}$", ErrorCode = ErrorCodes.InvalidTaxNumber, CountryCode = "EG" },

        // Vendor Validation Rules
        new() { EntityName = "Vendor", FieldName = "VendorCode", RuleType = "REQUIRED", ErrorCode = ErrorCodes.FieldRequired },
        new() { EntityName = "Vendor", FieldName = "NameAr", RuleType = "REQUIRED", ErrorCode = ErrorCodes.FieldRequired },
        new() { EntityName = "Vendor", FieldName = "TaxNumber", RuleType = "REGEX", RuleValue = @"^3\d{13}3$", ErrorCode = ErrorCodes.InvalidTaxNumber, CountryCode = "SA" },
        new() { EntityName = "Vendor", FieldName = "TaxNumber", RuleType = "REGEX", RuleValue = @"^\d{8}$", ErrorCode = ErrorCodes.InvalidTaxNumber, CountryCode = "JO" },

        // GlVoucher Detail Validation Rules
        new() { EntityName = "GlVoucherDetail", FieldName = "AccountCode", RuleType = "REQUIRED", ErrorCode = ErrorCodes.FieldRequired },
        new() { EntityName = "GlVoucherDetail", FieldName = "Debit", RuleType = "RANGE", RuleValue = "0..999999999999", ErrorCode = ErrorCodes.OutOfRange },
        new() { EntityName = "GlVoucherDetail", FieldName = "Credit", RuleType = "RANGE", RuleValue = "0..999999999999", ErrorCode = ErrorCodes.OutOfRange },

        // TaxRate Validation Rules
        new() { EntityName = "TaxRate", FieldName = "TaxRateCode", RuleType = "REQUIRED", ErrorCode = ErrorCodes.FieldRequired },
        new() { EntityName = "TaxRate", FieldName = "RatePercent", RuleType = "RANGE", RuleValue = "0..100", ErrorCode = ErrorCodes.InvalidTaxPercent },

        // PDC Cheque Validation Rules
        new() { EntityName = "GlPdcRegister", FieldName = "ChequeNumber", RuleType = "REQUIRED", ErrorCode = ErrorCodes.ChequeNumberRequired },
        new() { EntityName = "GlPdcRegister", FieldName = "Amount", RuleType = "RANGE", RuleValue = "0.001..999999999999", ErrorCode = ErrorCodes.InvalidChequeAmount }
    };

    public ValidationRuleCacheService(
        IServiceScopeFactory scopeFactory,
        ILogger<ValidationRuleCacheService> logger)
    {
        _scopeFactory = scopeFactory;
        _logger = logger;
    }

    public IReadOnlyList<SysFieldValidationRule> GetRulesForEntity(string entityName, string? countryCode = null, long? companyId = null)
    {
        EnsureInitialized();
        var cleanName = NormalizeEntityName(entityName);

        if (!_rulesByEntity.TryGetValue(cleanName, out var allRulesForEntity))
        {
            // Check common ERP aliases
            if (cleanName.Equals("Pdc", StringComparison.OrdinalIgnoreCase) && _rulesByEntity.TryGetValue("GlPdcRegister", out allRulesForEntity)) { }
            else if (cleanName.Equals("GlPdcRegister", StringComparison.OrdinalIgnoreCase) && _rulesByEntity.TryGetValue("Pdc", out allRulesForEntity)) { }
            else if (!cleanName.StartsWith("Gl", StringComparison.OrdinalIgnoreCase) && _rulesByEntity.TryGetValue("Gl" + cleanName, out allRulesForEntity)) { }
            else if (cleanName.StartsWith("Gl", StringComparison.OrdinalIgnoreCase) && _rulesByEntity.TryGetValue(cleanName[2..], out allRulesForEntity)) { }
            else
            {
                return Array.Empty<SysFieldValidationRule>();
            }
        }

        // Apply Country and Company resolution hierarchy
        // 1. Company overrides
        // 2. Country-specific rules
        // 3. Global rules (CountryCode == null && CompanyId == null)
        var result = new List<SysFieldValidationRule>();

        foreach (var rule in allRulesForEntity)
        {
            // If rule is company-specific, check match
            if (rule.CompanyId.HasValue)
            {
                if (companyId.HasValue && rule.CompanyId.Value == companyId.Value)
                    result.Add(rule);
                continue;
            }

            // If rule is country-specific, check match
            if (!string.IsNullOrWhiteSpace(rule.CountryCode))
            {
                if (!string.IsNullOrWhiteSpace(countryCode) &&
                    string.Equals(rule.CountryCode, countryCode, StringComparison.OrdinalIgnoreCase))
                {
                    result.Add(rule);
                }
                continue;
            }

            // Otherwise, it's a global rule
            result.Add(rule);
        }

        return result;
    }

    public async Task<IReadOnlyList<SysFieldValidationRule>> GetRulesForEntityAsync(string entityName, string? countryCode = null, long? companyId = null)
    {
        if (!_isInitialized)
        {
            await RefreshCacheAsync();
        }

        return GetRulesForEntity(entityName, countryCode, companyId);
    }

    public void RefreshCache()
    {
        lock (_initLock)
        {
            _rulesByEntity.Clear();
            _isInitialized = false;
            EnsureInitialized();
        }
    }

    public async Task RefreshCacheAsync()
    {
        try
        {
            using var scope = _scopeFactory.CreateScope();
            var db = scope.ServiceProvider.GetService<OracleDbContext>();
            if (db != null)
            {
                var rules = await db.SysFieldValidationRules
                    .Where(r => r.IsActive == 1)
                    .ToListAsync();

                _rulesByEntity.Clear();

                // Group loaded DB rules
                foreach (var rule in rules)
                {
                    var key = NormalizeEntityName(rule.EntityName);
                    if (!_rulesByEntity.ContainsKey(key))
                        _rulesByEntity[key] = new List<SysFieldValidationRule>();

                    _rulesByEntity[key].Add(rule);
                }

                // If DB had 0 rules, populate with default baselines
                if (rules.Count == 0)
                {
                    LoadFallbackBaselines();
                }

                _logger.LogInformation("Loaded {Count} dynamic validation rules across {Entities} entities into cache",
                    rules.Count > 0 ? rules.Count : DefaultBaselineRules.Count, _rulesByEntity.Count);
            }
            else
            {
                LoadFallbackBaselines();
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to load dynamic validation rules from database. Using fallback baselines.");
            LoadFallbackBaselines();
        }
        finally
        {
            _isInitialized = true;
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
                    var rules = db.SysFieldValidationRules
                        .Where(r => r.IsActive == 1)
                        .ToList();

                    _rulesByEntity.Clear();
                    foreach (var rule in rules)
                    {
                        var key = NormalizeEntityName(rule.EntityName);
                        if (!_rulesByEntity.ContainsKey(key))
                            _rulesByEntity[key] = new List<SysFieldValidationRule>();

                        _rulesByEntity[key].Add(rule);
                    }

                    if (rules.Count == 0)
                    {
                        LoadFallbackBaselines();
                    }

                    _logger.LogInformation("Loaded {Count} dynamic validation rules across {Entities} entities into cache",
                        rules.Count > 0 ? rules.Count : DefaultBaselineRules.Count, _rulesByEntity.Count);
                }
                else
                {
                    LoadFallbackBaselines();
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to load dynamic validation rules from DB. Using fallback baselines.");
                LoadFallbackBaselines();
            }
            finally
            {
                _isInitialized = true;
            }
        }
    }

    private void LoadFallbackBaselines()
    {
        _rulesByEntity.Clear();
        foreach (var rule in DefaultBaselineRules)
        {
            var key = NormalizeEntityName(rule.EntityName);
            if (!_rulesByEntity.ContainsKey(key))
                _rulesByEntity[key] = new List<SysFieldValidationRule>();

            _rulesByEntity[key].Add(rule);
        }
    }

    public static string NormalizeEntityName(string entityName)
    {
        if (string.IsNullOrWhiteSpace(entityName)) return string.Empty;
        var s = entityName.Trim();

        // 1. Remove prefixes: Create, Update, Add, Modify, Get, Delete
        var prefixes = new[] { "Create", "Update", "Add", "Modify", "Get", "Delete" };
        foreach (var prefix in prefixes)
        {
            if (s.StartsWith(prefix, StringComparison.OrdinalIgnoreCase) && s.Length > prefix.Length)
            {
                s = s[prefix.Length..];
                break;
            }
        }

        // 2. Remove suffixes: CreateDto, UpdateDto, Dto, Command, Request, Response
        var suffixes = new[] { "CreateDto", "UpdateDto", "Dto", "Command", "Request", "Response" };
        foreach (var suffix in suffixes)
        {
            if (s.EndsWith(suffix, StringComparison.OrdinalIgnoreCase) && s.Length > suffix.Length)
            {
                s = s[..^suffix.Length];
                break;
            }
        }

        return s;
    }
}
