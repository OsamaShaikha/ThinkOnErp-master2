namespace ThinkOnErp.Domain.Entities.Hr;

public static class SalaryPaymentMetadata
{
    public static string Normalize(string? value)=>value?.Trim().ToUpperInvariant() ?? string.Empty;
    public static void Validate(string currency,string method)
    {
        if(string.IsNullOrWhiteSpace(currency) || currency.Length>20 || currency.Any(char.IsControl))
            throw new ArgumentException("A configured currency code of at most 20 characters is required.");
        if(method is not ("BANK_TRANSFER" or "CASH" or "CHEQUE"))
            throw new ArgumentException("PaymentMethod must be BANK_TRANSFER, CASH or CHEQUE.");
    }
}
