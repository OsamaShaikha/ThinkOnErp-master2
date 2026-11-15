using Microsoft.EntityFrameworkCore;
using ThinkOnErp.Domain.Entities.Hr;

namespace ThinkOnErp.Infrastructure.Repositories.Hr;

public sealed partial class EmployeeRepository
{
    public async Task ValidateSalaryMetadataAsync(string currencyCode,string paymentMethod,CancellationToken cancellationToken=default)
    {
        SalaryPaymentMetadata.Validate(currencyCode,paymentMethod);
        // Accounting already uses SYS_CURRENCY.ShortNameEn as its currency code.
        // Do not guess a country, default currency or choose among ambiguous codes.
        var count=await _context.SysCurrencies.CountAsync(c=>c.ShortNameEn.Trim().ToUpper()==currencyCode,cancellationToken);
        if(count!=1) throw new ArgumentException("CurrencyCode must identify exactly one configured ERP currency ShortNameEn.");
        await _textStorage.ValidateAsync(_context.Entry(new SalaryStructure { CurrencyCode=currencyCode,PaymentMethod=paymentMethod }),cancellationToken);
    }
}
