using Microsoft.EntityFrameworkCore;
using ThinkOnErp.Domain.Entities.Accounting;
using ThinkOnErp.Domain.Interfaces.Accounting;
using ThinkOnErp.Infrastructure.Data;

namespace ThinkOnErp.Infrastructure.Repositories.Accounting;

public sealed class GlVoucherRepository : IGlVoucherRepository
{
    private readonly OracleDbContext _context;

    public GlVoucherRepository(OracleDbContext context)
    {
        _context = context;
    }

    public async Task<IReadOnlyList<GlVoucherType>> GetVoucherTypesAsync(CancellationToken cancellationToken = default)
    {
        return await _context.GlVoucherTypes
            .AsNoTracking()
            .OrderBy(t => t.DisplayOrder)
            .ThenBy(t => t.TypeCode)
            .ToListAsync(cancellationToken);
    }

    public async Task<GlVoucherType?> GetVoucherTypeByIdAsync(long id, CancellationToken cancellationToken = default)
    {
        return await _context.GlVoucherTypes
            .AsNoTracking()
            .FirstOrDefaultAsync(t => t.Id == id, cancellationToken);
    }

    public async Task<GlVoucherType?> GetVoucherTypeByCodeAsync(int typeCode, CancellationToken cancellationToken = default)
    {
        return await _context.GlVoucherTypes
            .AsNoTracking()
            .FirstOrDefaultAsync(t => t.TypeCode == typeCode, cancellationToken);
    }

    public async Task<GlVoucherType?> GetVoucherTypeByKeyAsync(string typeKey, CancellationToken cancellationToken = default)
    {
        return await _context.GlVoucherTypes
            .AsNoTracking()
            .FirstOrDefaultAsync(t => t.TypeKey == typeKey.ToUpper(), cancellationToken);
    }

    public async Task<GlVoucherType> CreateVoucherTypeAsync(GlVoucherType voucherType, CancellationToken cancellationToken = default)
    {
        await _context.GlVoucherTypes.AddAsync(voucherType, cancellationToken);
        await _context.SaveChangesAsync(cancellationToken);
        return voucherType;
    }

    public async Task<GlVoucherType> UpdateVoucherTypeAsync(GlVoucherType voucherType, CancellationToken cancellationToken = default)
    {
        _context.GlVoucherTypes.Update(voucherType);
        await _context.SaveChangesAsync(cancellationToken);
        return voucherType;
    }

    public async Task DeleteVoucherTypeAsync(long id, CancellationToken cancellationToken = default)
    {
        var entity = await _context.GlVoucherTypes.FirstOrDefaultAsync(t => t.Id == id, cancellationToken);
        if (entity != null)
        {
            _context.GlVoucherTypes.Remove(entity);
            await _context.SaveChangesAsync(cancellationToken);
        }
    }

    public async Task<bool> VoucherTypeExistsAsync(int typeCode, string typeKey, long? excludeId = null, CancellationToken cancellationToken = default)
    {
        var upperKey = typeKey.Trim().ToUpper();
        return await _context.GlVoucherTypes
            .AnyAsync(t => (excludeId == null || t.Id != excludeId.Value) &&
                           (t.TypeCode == typeCode || t.TypeKey.ToUpper() == upperKey),
                      cancellationToken);
    }

    public async Task<bool> HasAssociatedVouchersAsync(int typeCode, CancellationToken cancellationToken = default)
    {
        return await _context.GlVoucherHeaders
            .AnyAsync(h => h.VoucherType == typeCode, cancellationToken);
    }

    public async Task<GlVoucherHeader?> GetByIdAsync(long id, CancellationToken cancellationToken = default)
    {
        return await _context.GlVoucherHeaders
            .Include(h => h.Details.OrderBy(d => d.LineSer))
                .ThenInclude(d => d.Account)
            .FirstOrDefaultAsync(h => h.Id == id, cancellationToken);
    }

    public async Task<GlVoucherHeader?> GetByNumberAsync(
        long branchId,
        int year,
        int month,
        int typeCode,
        long voucherNo,
        CancellationToken cancellationToken = default)
    {
        return await _context.GlVoucherHeaders
            .Include(h => h.Details.OrderBy(d => d.LineSer))
                .ThenInclude(d => d.Account)
            .FirstOrDefaultAsync(
                h => h.BranchId == branchId &&
                     h.VoucherYear == year &&
                     h.VoucherMonth == month &&
                     h.VoucherType == typeCode &&
                     h.VoucherNo == voucherNo,
                cancellationToken);
    }

    public async Task<(IReadOnlyList<GlVoucherHeader> Items, long TotalCount)> GetPagedVouchersAsync(
        long? branchId,
        int? year,
        int? month,
        int? typeCode,
        int? status,
        DateTime? fromDate,
        DateTime? toDate,
        string? searchKeyword,
        int pageIndex,
        int pageSize,
        CancellationToken cancellationToken = default)
    {
        var query = _context.GlVoucherHeaders
            .AsNoTracking()
            .Include(h => h.Details)
            .AsQueryable();

        if (branchId.HasValue && branchId.Value > 0)
            query = query.Where(h => h.BranchId == branchId.Value);

        if (year.HasValue && year.Value > 0)
            query = query.Where(h => h.VoucherYear == year.Value);

        if (month.HasValue && month.Value > 0)
            query = query.Where(h => h.VoucherMonth == month.Value);

        if (typeCode.HasValue && typeCode.Value > 0)
            query = query.Where(h => h.VoucherType == typeCode.Value);

        if (status.HasValue && status.Value > 0)
            query = query.Where(h => h.Status == status.Value);

        if (fromDate.HasValue)
            query = query.Where(h => h.VoucherDate >= fromDate.Value.Date);

        if (toDate.HasValue)
            query = query.Where(h => h.VoucherDate <= toDate.Value.Date.AddDays(1).AddTicks(-1));

        if (!string.IsNullOrWhiteSpace(searchKeyword))
        {
            var keyword = searchKeyword.Trim().ToLower();
            query = query.Where(h =>
                (h.Description != null && h.Description.ToLower().Contains(keyword)) ||
                h.VoucherNo.ToString().Contains(keyword));
        }

        var totalCount = await query.LongCountAsync(cancellationToken);

        var items = await query
            .OrderByDescending(h => h.VoucherDate)
            .ThenByDescending(h => h.VoucherNo)
            .Skip((pageIndex - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(cancellationToken);

        return (items, totalCount);
    }

    public async Task<long> GenerateNextSerialNoAsync(
        long branchId,
        int year,
        int month,
        int typeCode,
        string resetPolicy,
        CancellationToken cancellationToken = default)
    {
        var targetMonth = resetPolicy.Equals("YEARLY", StringComparison.OrdinalIgnoreCase) ? 0 : month;

        var serialRecord = await _context.GlVoucherSerials
            .FirstOrDefaultAsync(
                s => s.BranchId == branchId &&
                     s.SerialYear == year &&
                     s.SerialMonth == targetMonth &&
                     s.VoucherType == typeCode,
                cancellationToken);

        if (serialRecord == null)
        {
            serialRecord = new GlVoucherSerial
            {
                BranchId = branchId,
                SerialYear = year,
                SerialMonth = targetMonth,
                VoucherType = typeCode,
                LastSerialNo = 1
            };
            _context.GlVoucherSerials.Add(serialRecord);
        }
        else
        {
            serialRecord.LastSerialNo += 1;
            _context.GlVoucherSerials.Update(serialRecord);
        }

        return serialRecord.LastSerialNo;
    }

    public Task AddVoucherAsync(GlVoucherHeader voucher, CancellationToken cancellationToken = default)
    {
        _context.GlVoucherHeaders.Add(voucher);
        return Task.CompletedTask;
    }

    public async Task SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        await _context.SaveChangesAsync(cancellationToken);
    }
}
