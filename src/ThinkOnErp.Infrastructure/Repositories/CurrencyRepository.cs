using Microsoft.EntityFrameworkCore;
using ThinkOnErp.Domain.Entities;
using ThinkOnErp.Domain.Interfaces;
using ThinkOnErp.Infrastructure.Data;

namespace ThinkOnErp.Infrastructure.Repositories;

public class CurrencyRepository : ICurrencyRepository
{
    private readonly ThinkOnErpDbContext _context;
    public CurrencyRepository(ThinkOnErpDbContext context) => _context = context;

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
        currency.UpdateDate = DateTime.Now;
        _context.SysCurrencies.Update(currency);
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