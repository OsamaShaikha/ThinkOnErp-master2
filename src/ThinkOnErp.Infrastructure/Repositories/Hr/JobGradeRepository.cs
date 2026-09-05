using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using ThinkOnErp.Domain.Entities.Hr;
using ThinkOnErp.Domain.Interfaces.Hr;
using ThinkOnErp.Infrastructure.Data;

namespace ThinkOnErp.Infrastructure.Repositories.Hr;

public sealed class JobGradeRepository : IJobGradeRepository
{
    private readonly OracleDbContext _context;

    public JobGradeRepository(OracleDbContext context)
    {
        _context = context ?? throw new ArgumentNullException(nameof(context));
    }

    public async Task<IReadOnlyList<JobGrade>> GetAllAsync(bool activeOnly = true, CancellationToken cancellationToken = default)
    {
        var query = _context.JobGrades
            .Include(g => g.Positions)
            .AsNoTracking();

        if (activeOnly)
        {
            query = query.Where(g => g.IsActive);
        }

        return await query
            .OrderBy(g => g.Level)
            .ThenBy(g => g.GradeCode)
            .ToListAsync(cancellationToken);
    }

    public async Task<JobGrade?> GetByCodeAsync(string code, CancellationToken cancellationToken = default)
    {
        return await _context.JobGrades
            .Include(g => g.Positions)
            .SingleOrDefaultAsync(g => g.GradeCode == code, cancellationToken);
    }

    public async Task<bool> CodeExistsAsync(string code, CancellationToken cancellationToken = default)
    {
        return await _context.JobGrades.AsNoTracking().AnyAsync(g => g.GradeCode == code, cancellationToken);
    }

    public async Task<bool> HasPositionsAsync(string code, CancellationToken cancellationToken = default)
    {
        return await _context.Positions.AsNoTracking().AnyAsync(p => p.JobGradeCode == code, cancellationToken);
    }

    public async Task AddAsync(JobGrade jobGrade, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(jobGrade);
        await _context.JobGrades.AddAsync(jobGrade, cancellationToken);
    }

    public void Update(JobGrade jobGrade)
    {
        ArgumentNullException.ThrowIfNull(jobGrade);
        _context.JobGrades.Update(jobGrade);
    }

    public void Remove(JobGrade jobGrade)
    {
        ArgumentNullException.ThrowIfNull(jobGrade);
        _context.JobGrades.Remove(jobGrade);
    }

    public Task SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        return _context.SaveChangesAsync(cancellationToken);
    }
}
