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

        existing.CurrencyNameLocal = currency.CurrencyNameLocal;
        existing.CurrencyNameEn = currency.CurrencyNameEn;
        existing.ShortNameLocal = currency.ShortNameLocal;
        existing.ShortNameEn = currency.ShortNameEn;
        existing.SingularNameLocal = currency.SingularNameLocal;
        existing.SingularNameEn = currency.SingularNameEn;
        existing.DualNameLocal = currency.DualNameLocal;
        existing.DualNameEn = currency.DualNameEn;
        existing.CollectiveNameLocal = currency.CollectiveNameLocal;
        existing.CollectiveNameEn = currency.CollectiveNameEn;
        existing.FractionNameLocal = currency.FractionNameLocal;
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