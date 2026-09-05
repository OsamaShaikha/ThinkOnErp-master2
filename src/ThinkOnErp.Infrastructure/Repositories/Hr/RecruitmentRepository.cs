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

public sealed class RecruitmentRepository : IRecruitmentRepository
{
    private readonly OracleDbContext _context;

    public RecruitmentRepository(OracleDbContext context)
    {
        _context = context ?? throw new ArgumentNullException(nameof(context));
    }

    public async Task<IReadOnlyList<JobRequisition>> GetAllRequisitionsAsync(string? status = null, CancellationToken cancellationToken = default)
    {
        var query = _context.JobRequisitions
            .Include(r => r.Position)
            .Include(r => r.Department)
            .Include(r => r.Applications)
            .AsNoTracking();

        if (!string.IsNullOrWhiteSpace(status))
        {
            query = query.Where(r => r.Status == status);
        }

        return await query.OrderByDescending(r => r.CreationDate).ToListAsync(cancellationToken);
    }

    public async Task<JobRequisition?> GetRequisitionByCodeAsync(string code, bool includeApplications = true, CancellationToken cancellationToken = default)
    {
        var query = _context.JobRequisitions
            .Include(r => r.Position)
            .Include(r => r.Department)
            .AsQueryable();

        if (includeApplications)
        {
            query = query
                .Include(r => r.Applications)
                    .ThenInclude(a => a.Candidate);
        }

        return await query.SingleOrDefaultAsync(r => r.RequisitionCode == code, cancellationToken);
    }

    public async Task<bool> RequisitionCodeExistsAsync(string code, CancellationToken cancellationToken = default)
    {
        return await _context.JobRequisitions.AsNoTracking().AnyAsync(r => r.RequisitionCode == code, cancellationToken);
    }

    public async Task AddRequisitionAsync(JobRequisition requisition, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(requisition);
        await _context.JobRequisitions.AddAsync(requisition, cancellationToken);
    }

    public void UpdateRequisition(JobRequisition requisition)
    {
        ArgumentNullException.ThrowIfNull(requisition);
        _context.JobRequisitions.Update(requisition);
    }

    public async Task<IReadOnlyList<Candidate>> GetAllCandidatesAsync(CancellationToken cancellationToken = default)
    {
        return await _context.Candidates
            .Include(c => c.Applications)
            .AsNoTracking()
            .OrderByDescending(c => c.CreationDate)
            .ToListAsync(cancellationToken);
    }

    public async Task<Candidate?> GetCandidateByCodeAsync(string code, bool includeApplications = true, CancellationToken cancellationToken = default)
    {
        var query = _context.Candidates.AsQueryable();

        if (includeApplications)
        {
            query = query.Include(c => c.Applications)
                .ThenInclude(a => a.Requisition);
        }

        return await query.SingleOrDefaultAsync(c => c.CandidateCode == code, cancellationToken);
    }

    public async Task<bool> CandidateCodeExistsAsync(string code, CancellationToken cancellationToken = default)
    {
        return await _context.Candidates.AsNoTracking().AnyAsync(c => c.CandidateCode == code, cancellationToken);
    }

    public async Task AddCandidateAsync(Candidate candidate, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(candidate);
        await _context.Candidates.AddAsync(candidate, cancellationToken);
    }

    public void UpdateCandidate(Candidate candidate)
    {
        ArgumentNullException.ThrowIfNull(candidate);
        _context.Candidates.Update(candidate);
    }

    public async Task<CandidateApplication?> GetApplicationByIdAsync(long id, CancellationToken cancellationToken = default)
    {
        return await _context.CandidateApplications
            .Include(a => a.Candidate)
            .Include(a => a.Requisition)
            .SingleOrDefaultAsync(a => a.Id == id, cancellationToken);
    }

    public async Task AddApplicationAsync(CandidateApplication application, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(application);
        await _context.CandidateApplications.AddAsync(application, cancellationToken);
    }

    public void UpdateApplication(CandidateApplication application)
    {
        ArgumentNullException.ThrowIfNull(application);
        _context.CandidateApplications.Update(application);
    }

    public async Task<IReadOnlyList<OnboardingTask>> GetOnboardingTasksByEmployeeAsync(string employeeCode, CancellationToken cancellationToken = default)
    {
        return await _context.OnboardingTasks
            .AsNoTracking()
            .Where(t => t.EmployeeCode == employeeCode)
            .OrderBy(t => t.DueDate)
            .ToListAsync(cancellationToken);
    }

    public async Task<OnboardingTask?> GetOnboardingTaskByIdAsync(long id, CancellationToken cancellationToken = default)
    {
        return await _context.OnboardingTasks.FindAsync(new object[] { id }, cancellationToken);
    }

    public async Task AddOnboardingTaskAsync(OnboardingTask task, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(task);
        await _context.OnboardingTasks.AddAsync(task, cancellationToken);
    }

    public void UpdateOnboardingTask(OnboardingTask task)
    {
        ArgumentNullException.ThrowIfNull(task);
        _context.OnboardingTasks.Update(task);
    }

    public Task SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        return _context.SaveChangesAsync(cancellationToken);
    }
}
