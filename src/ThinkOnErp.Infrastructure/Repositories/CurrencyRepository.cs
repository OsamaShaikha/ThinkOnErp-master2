using Microsoft.EntityFrameworkCore;
using ThinkOnErp.Domain.Entities;
using ThinkOnErp.Domain.Interfaces;
using ThinkOnErp.Infrastructure.Data;

namespace ThinkOnErp.Infrastructure.Repositories;

public class CurrencyRepository : ICurrencyRepository
{
    private readonly OracleDbContext _context;
    public CurrencyRepository(OracleDbContext context) => _context = context;

    public async Task<List<SysCurrency>> GetAllAsync() =>
        await _context.SysCurrencies.ToListAsync();

    public async Task<SysCurrency?> GetByIdAsync(long rowId) =>
        await _context.SysCurrencies.FindAsync(rowId);

    public async Task<long> CreateAsync(SysCurrency currency)
    {
        _context.SysCurrencies.Add(currency);
        await _context.SaveChangesAsync();
        return currency.Id;
    }

    public async Task<long> UpdateAsync(SysCurrency currency)
    {
        var existing = await _context.SysCurrencies.FindAsync(currency.Id);
        if (existing == null) return 0;

        existing.CurrencyNameAr = currency.CurrencyNameAr;
        existing.CurrencyNameEn = currency.CurrencyNameEn;
        existing.ShortNameAr = currency.ShortNameAr;
        existing.ShortNameEn = currency.ShortNameEn;
        existing.SingularNameAr = currency.SingularNameAr;
        existing.SingularNameEn = currency.SingularNameEn;
        existing.DualNameAr = currency.DualNameAr;
        existing.DualNameEn = currency.DualNameEn;
        existing.CollectiveNameAr = currency.CollectiveNameAr;
        existing.CollectiveNameEn = currency.CollectiveNameEn;
        existing.FractionNameAr = currency.FractionNameAr;
        existing.FractionNameEn = currency.FractionNameEn;
        existing.CurrRate = currency.CurrRate;
        existing.CurrRateDate = currency.CurrRateDate;
        existing.UpdateUser = currency.UpdateUser;
        existing.UpdateDate = DateTime.Now;

        return await _context.SaveChangesAsync();
    }

    public async Task<long> DeleteAsync(long rowId)
    {
        var currency = await _context.SysCurrencies.FindAsync(rowId);
        if (currency == null) return 0;
        _context.SysCurrencies.Remove(currency);
        return await _context.SaveChangesAsync();
    }
}