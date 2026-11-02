using System;
using System.Collections.Generic;

namespace ThinkOnErp.Domain.Constants;

/// <summary>
/// Constants and protection rules for default/system payment methods defined in DEV_TEMPLATE and initial seed.
/// </summary>
public static class PaymentMethodConstants
{
    public const long CustomerAccountId = 99;
    public const long HospitalityId = 98;

    public const string Cash = "CASH";
    public const string Mada = "MADA";
    public const string Visa = "VISA";
    public const string BankTransfer = "BANK_TRANSFER";
    public const string Cheque = "CHEQUE";
    public const string Hospitality = "HOSPITALITY";
    public const string CustomerAccount = "CUSTOMER_ACCOUNT";

    /// <summary>
    /// Default payment method codes seeded from DEV_TEMPLATE that can never be deleted permanently.
    /// </summary>
    public static readonly HashSet<string> ProtectedCodes = new(StringComparer.OrdinalIgnoreCase)
    {
        Cash,
        Mada,
        Visa,
        BankTransfer,
        Cheque,
        Hospitality,
        CustomerAccount
    };

    /// <summary>
    /// Fixed system IDs for special POS payment channels.
    /// </summary>
    public static readonly HashSet<long> ProtectedIds = new()
    {
        HospitalityId,
        CustomerAccountId
    };

    /// <summary>
    /// Checks whether a payment method is a template/system method protected from permanent deletion.
    /// </summary>
    public static bool IsProtected(long id, string? code)
    {
        if (ProtectedIds.Contains(id))
            return true;

        if (!string.IsNullOrWhiteSpace(code) && ProtectedCodes.Contains(code.Trim()))
            return true;

        return false;
    }
}
