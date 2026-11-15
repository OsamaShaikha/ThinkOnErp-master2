namespace ThinkOnErp.Domain.Entities.Hr;

public static class PayrollCurrencyRules
{
    public static int GetCurrencyDecimals(string? currencyCode)
    {
        return SalaryPaymentMetadata.Normalize(currencyCode) switch
        {
            "JOD" or "KWD" or "BHD" or "OMR" or "TND" or "IQD" => 3,
            "JPY" or "KRW" or "VND" => 0,
            _ => 2 // Standard for SAR, AED, USD, EUR, QAR, EGP, etc.
        };
    }

    public static int GetCurrencyDecimals(string? currencyCode, int? configuredDecimals)
    {
        return !configuredDecimals.HasValue
            ? GetCurrencyDecimals(currencyCode)
            : configuredDecimals is < 0 or > 4
            ? throw new InvalidOperationException("Configured payroll currency decimal places must be between 0 and 4.")
            : configuredDecimals.Value;
    }

    /// <summary>Rounds an amount using the active branch rounding rule (1..6).</summary>
    public static decimal Round(decimal value, int decimals, int? roundingRule = 1)
    {
        if (decimals is < 0 or > 28)
        {
            throw new ArgumentOutOfRangeException(nameof(decimals));
        }

        if (!roundingRule.HasValue)
        {
            return decimal.Round(value, decimals);
        }

        int rule = roundingRule.Value;
        if (rule == 1) // HALF_UP
        {
            return decimal.Round(value, decimals, MidpointRounding.AwayFromZero);
        }

        decimal factor = 1m;
        for (int i = 0; i < decimals; i++)
        {
            factor *= 10m;
        }

        decimal scaled = value * factor;
        decimal truncated = decimal.Truncate(scaled);
        decimal remainder = Math.Abs(scaled - truncated);
        int sign = Math.Sign(scaled);

        return rule switch
        {
            2 => (truncated + (remainder > 0.5m ? sign : 0)) / factor, // HALF_DOWN
            3 => (truncated + (remainder > 0m ? sign : 0)) / factor, // UP, away from zero
            4 => truncated / factor, // DOWN, toward zero
            5 => decimal.Ceiling(scaled) / factor,
            6 => decimal.Floor(scaled) / factor,
            _ => throw new ArgumentOutOfRangeException(nameof(roundingRule), rule, "Rounding rule must be between 1 and 6.")
        };
    }

    // Existing payroll totals have no conversion rate or currency dimension.
    // Compare normalized codes without rewriting historical salary/run data.
    public static string? RequireSingleCurrency(IEnumerable<string?> currencyCodes)
    {
        string? currency = null;
        foreach (string? code in currencyCodes)
        {
            string normalized = SalaryPaymentMetadata.Normalize(code);
            if (normalized.Length == 0)
            {
                throw new InvalidOperationException("Payroll currency is missing. Resolve the salary currency and recalculate before approval.");
            }

            if (currency != null && currency != normalized)
            {
                throw new InvalidOperationException("Payroll cannot combine different currencies without an approved currency conversion workflow. Calculate separate currency batches.");
            }

            currency = normalized;
        }
        return currency;
    }
}
