using System.Collections;
using System.Collections.Concurrent;
using System.Globalization;
using System.Reflection;
using System.Text.RegularExpressions;
using FluentValidation;
using FluentValidation.Results;
using Microsoft.Extensions.Logging;
using ThinkOnErp.Application.Services.Localization;
using ThinkOnErp.Domain.Constants;
using ThinkOnErp.Domain.Entities;

namespace ThinkOnErp.Application.Services.Validation;

public class DynamicValidationEngine : IDynamicValidationEngine
{
    private readonly IValidationRuleCacheService _ruleCacheService;
    private readonly ILocalizationService _localizationService;
    private readonly ILogger<DynamicValidationEngine> _logger;

    private static readonly ConcurrentDictionary<(Type Type, string PropName), PropertyInfo?> PropertyCache = new();
    private static readonly ConcurrentDictionary<string, Regex> RegexCache = new();

    public DynamicValidationEngine(
        IValidationRuleCacheService ruleCacheService,
        ILocalizationService localizationService,
        ILogger<DynamicValidationEngine> logger)
    {
        _ruleCacheService = ruleCacheService;
        _localizationService = localizationService;
        _logger = logger;
    }

    public ValidationResult Validate<T>(T instance, string? countryCode = null, long? companyId = null)
    {
        if (instance == null)
        {
            var msg = _localizationService.GetMessage(ErrorCodes.FieldRequired);
            return new ValidationResult(new[] { new ValidationFailure("Request", msg) { ErrorCode = ErrorCodes.FieldRequired } });
        }

        var failures = new List<ValidationFailure>();
        ValidateObjectInternal(instance, typeof(T).Name, countryCode, companyId, failures, prefix: "");

        // Perform cross-field domain invariants (Voucher balancing, date ranges, limits)
        ValidateCrossFieldInvariants(instance, failures);

        return new ValidationResult(failures);
    }

    public Task<ValidationResult> ValidateAsync<T>(T instance, string? countryCode = null, long? companyId = null, CancellationToken cancellationToken = default)
    {
        return Task.FromResult(Validate(instance, countryCode, companyId));
    }

    public void ValidateAndThrow<T>(T instance, string? countryCode = null, long? companyId = null)
    {
        var result = Validate(instance, countryCode, companyId);
        if (!result.IsValid)
        {
            throw new ValidationException(result.Errors);
        }
    }

    public Task ValidateAndThrowAsync<T>(T instance, string? countryCode = null, long? companyId = null, CancellationToken cancellationToken = default)
    {
        ValidateAndThrow(instance, countryCode, companyId);
        return Task.CompletedTask;
    }

    private void ValidateObjectInternal(object instance, string entityTypeName, string? countryCode, long? companyId, List<ValidationFailure> failures, string prefix)
    {
        if (instance == null) return;

        var rules = _ruleCacheService.GetRulesForEntity(entityTypeName, countryCode, companyId);
        foreach (var rule in rules)
        {
            var failure = EvaluateRule(instance, rule, prefix);
            if (failure != null)
            {
                failures.Add(failure);
            }
        }

        // Check child collections (e.g. Details, Lines, Allocations)
        var props = instance.GetType().GetProperties(BindingFlags.Public | BindingFlags.Instance);
        foreach (var prop in props)
        {
            if (prop.PropertyType != typeof(string) && typeof(IEnumerable).IsAssignableFrom(prop.PropertyType))
            {
                var collection = prop.GetValue(instance) as IEnumerable;
                if (collection != null)
                {
                    int index = 0;
                    foreach (var item in collection)
                    {
                        if (item != null)
                        {
                            var childEntityName = item.GetType().Name;
                            var childPrefix = string.IsNullOrEmpty(prefix) ? $"{prop.Name}[{index}]." : $"{prefix}{prop.Name}[{index}].";
                            ValidateObjectInternal(item, childEntityName, countryCode, companyId, failures, childPrefix);
                        }
                        index++;
                    }
                }
            }
        }
    }

    private ValidationFailure? EvaluateRule(object instance, SysFieldValidationRule rule, string prefix)
    {
        var prop = GetProperty(instance.GetType(), rule.FieldName);
        if (prop == null)
        {
            return null; // Property does not exist on this DTO
        }

        var val = prop.GetValue(instance);
        var ruleType = rule.RuleType.Trim().ToUpperInvariant();
        var fullPropName = $"{prefix}{rule.FieldName}";

        switch (ruleType)
        {
            case "REQUIRED":
                if (val == null || (val is string str && string.IsNullOrWhiteSpace(str)))
                {
                    var msg = _localizationService.GetMessageWithFormat(rule.ErrorCode, null, rule.FieldName);
                    return new ValidationFailure(fullPropName, msg) { ErrorCode = rule.ErrorCode };
                }
                break;

            case "MAX_LENGTH":
                if (val is string sMax && int.TryParse(rule.RuleValue, out var maxLen) && sMax.Length > maxLen)
                {
                    var msg = _localizationService.GetMessageWithFormat(rule.ErrorCode, null, rule.FieldName, maxLen);
                    return new ValidationFailure(fullPropName, msg) { ErrorCode = rule.ErrorCode };
                }
                break;

            case "MIN_LENGTH":
                if (val is string sMin && int.TryParse(rule.RuleValue, out var minLen) && sMin.Length < minLen)
                {
                    var msg = _localizationService.GetMessageWithFormat(rule.ErrorCode, null, rule.FieldName, minLen);
                    return new ValidationFailure(fullPropName, msg) { ErrorCode = rule.ErrorCode };
                }
                break;

            case "REGEX":
                if (val is string sRegex && !string.IsNullOrWhiteSpace(sRegex) && !string.IsNullOrWhiteSpace(rule.RuleValue))
                {
                    var regex = GetRegex(rule.RuleValue);
                    if (!regex.IsMatch(sRegex))
                    {
                        var msg = _localizationService.GetMessageWithFormat(rule.ErrorCode, null, rule.FieldName);
                        return new ValidationFailure(fullPropName, msg) { ErrorCode = rule.ErrorCode };
                    }
                }
                break;

            case "RANGE":
                if (val != null && !string.IsNullOrWhiteSpace(rule.RuleValue))
                {
                    if (TryParseRange(rule.RuleValue, out var minVal, out var maxVal))
                    {
                        var numVal = ConvertToDecimal(val);
                        if (numVal < minVal || numVal > maxVal)
                        {
                            var msg = _localizationService.GetMessageWithFormat(rule.ErrorCode, null, rule.FieldName, minVal, maxVal);
                            return new ValidationFailure(fullPropName, msg) { ErrorCode = rule.ErrorCode };
                        }
                    }
                }
                break;

            case "POSITIVE":
                if (val != null)
                {
                    var numVal = ConvertToDecimal(val);
                    if (numVal <= 0)
                    {
                        var msg = _localizationService.GetMessageWithFormat(rule.ErrorCode, null, rule.FieldName);
                        return new ValidationFailure(fullPropName, msg) { ErrorCode = rule.ErrorCode };
                    }
                }
                break;
        }

        return null;
    }

    private void ValidateCrossFieldInvariants(object instance, List<ValidationFailure> failures)
    {
        var type = instance.GetType();

        // 1. Voucher Lines & Balancing Check (For Voucher Headers)
        var detailsProp = GetProperty(type, "Details") ?? GetProperty(type, "Lines");
        if (detailsProp != null && detailsProp.PropertyType != typeof(string) && typeof(IEnumerable).IsAssignableFrom(detailsProp.PropertyType))
        {
            var details = detailsProp.GetValue(instance) as IEnumerable;
            if (details != null)
            {
                var detailList = details.Cast<object>().ToList();
                if (detailList.Count == 0 && type.Name.Contains("Voucher", StringComparison.OrdinalIgnoreCase))
                {
                    var msg = _localizationService.GetMessage(ErrorCodes.EmptyVoucherLines);
                    failures.Add(new ValidationFailure(detailsProp.Name, msg) { ErrorCode = ErrorCodes.EmptyVoucherLines });
                }
                else if (detailList.Count > 0)
                {
                    decimal totalDebit = 0;
                    decimal totalCredit = 0;
                    int lineIndex = 0;

                    foreach (var line in detailList)
                    {
                        var debitProp = GetProperty(line.GetType(), "Debit") ?? GetProperty(line.GetType(), "DebitAmount");
                        var creditProp = GetProperty(line.GetType(), "Credit") ?? GetProperty(line.GetType(), "CreditAmount");

                        var debit = debitProp != null ? ConvertToDecimal(debitProp.GetValue(line)) : 0m;
                        var credit = creditProp != null ? ConvertToDecimal(creditProp.GetValue(line)) : 0m;

                        if (debit > 0 && credit > 0)
                        {
                            var msg = _localizationService.GetMessage(ErrorCodes.LineDebitCreditConflict);
                            failures.Add(new ValidationFailure($"{detailsProp.Name}[{lineIndex}]", msg) { ErrorCode = ErrorCodes.LineDebitCreditConflict });
                        }
                        else if (debit == 0 && credit == 0 && (debitProp != null || creditProp != null))
                        {
                            var msg = _localizationService.GetMessage(ErrorCodes.InvalidChequeAmount);
                            failures.Add(new ValidationFailure($"{detailsProp.Name}[{lineIndex}]", msg) { ErrorCode = ErrorCodes.InvalidChequeAmount });
                        }

                        totalDebit += debit;
                        totalCredit += credit;
                        lineIndex++;
                    }

                    if (type.Name.Contains("Voucher", StringComparison.OrdinalIgnoreCase) && Math.Abs(totalDebit - totalCredit) > 0.0001m)
                    {
                        var msg = _localizationService.GetMessage(ErrorCodes.UnbalancedVoucher);
                        failures.Add(new ValidationFailure(detailsProp.Name, msg) { ErrorCode = ErrorCodes.UnbalancedVoucher });
                    }
                }
            }
        }

        // 2. Date Range Invariants (StartDate < EndDate)
        var startDateProp = GetProperty(type, "StartDate");
        var endDateProp = GetProperty(type, "EndDate");
        if (startDateProp != null && endDateProp != null)
        {
            var startVal = startDateProp.GetValue(instance);
            var endVal = endDateProp.GetValue(instance);

            if (startVal is DateTime start && endVal is DateTime end && start > end)
            {
                var msg = _localizationService.GetMessageWithFormat(ErrorCodes.DateRangeInvalid, null, start.ToString("yyyy-MM-dd"), end.ToString("yyyy-MM-dd"));
                failures.Add(new ValidationFailure("StartDate", msg) { ErrorCode = ErrorCodes.DateRangeInvalid });
            }
        }

        // 3. Limits Range Invariants (MinLimit <= MaxLimit)
        var minLimitProp = GetProperty(type, "MinLimit");
        var maxLimitProp = GetProperty(type, "MaxLimit");
        if (minLimitProp != null && maxLimitProp != null)
        {
            var min = ConvertToDecimal(minLimitProp.GetValue(instance));
            var max = ConvertToDecimal(maxLimitProp.GetValue(instance));

            if (min > max && max > 0)
            {
                var msg = _localizationService.GetMessageWithFormat(ErrorCodes.OutOfRange, null, "MinLimit", 0, max);
                failures.Add(new ValidationFailure("MinLimit", msg) { ErrorCode = ErrorCodes.OutOfRange });
            }
        }
    }

    private static PropertyInfo? GetProperty(Type type, string propName)
    {
        return PropertyCache.GetOrAdd((type, propName), key =>
        {
            return key.Type.GetProperty(key.PropName, BindingFlags.IgnoreCase | BindingFlags.Public | BindingFlags.Instance);
        });
    }

    private static Regex GetRegex(string pattern)
    {
        return RegexCache.GetOrAdd(pattern, p => new Regex(p, RegexOptions.Compiled | RegexOptions.CultureInvariant));
    }

    private static bool TryParseRange(string rangeStr, out decimal min, out decimal max)
    {
        min = 0;
        max = decimal.MaxValue;

        var parts = rangeStr.Split(new[] { "..", "," }, StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length == 2 &&
            decimal.TryParse(parts[0].Trim(), NumberStyles.Any, CultureInfo.InvariantCulture, out min) &&
            decimal.TryParse(parts[1].Trim(), NumberStyles.Any, CultureInfo.InvariantCulture, out max))
        {
            return true;
        }

        return false;
    }

    private static decimal ConvertToDecimal(object? val)
    {
        if (val == null) return 0m;
        if (val is decimal d) return d;
        if (val is double dbl) return (decimal)dbl;
        if (val is float f) return (decimal)f;
        if (val is int i) return i;
        if (val is long l) return l;
        if (val is short sh) return sh;

        if (decimal.TryParse(val.ToString(), NumberStyles.Any, CultureInfo.InvariantCulture, out var parsed))
            return parsed;

        return 0m;
    }
}
