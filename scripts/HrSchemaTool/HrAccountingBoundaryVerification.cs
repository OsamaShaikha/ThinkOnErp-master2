using Microsoft.EntityFrameworkCore;
using Oracle.ManagedDataAccess.Client;
using ThinkOnErp.Application.DTOs.Hr;
using ThinkOnErp.Application.Services.Hr;
using ThinkOnErp.Infrastructure.Data;
using ThinkOnErp.Infrastructure.Repositories.Hr;

internal static class HrAccountingBoundaryVerification
{
    public static async Task Verify(OracleConnection connection, List<string> report)
    {
        using var db = new OracleDbContext(new DbContextOptionsBuilder<OracleDbContext>().UseOracle(connection,
            o => o.UseOracleSQLCompatibility(OracleSQLCompatibility.DatabaseVersion19)).Options);
        var employee = await db.Employees.AsNoTracking().Where(e => e.IsActive)
            .Select(e => new { e.EmployeeCode, e.BranchId }).FirstAsync();
        if (!employee.BranchId.HasValue)
            throw new InvalidOperationException("Accounting boundary verification requires an employee with a branch.");
        var companyId = await db.SysBranches.AsNoTracking().Where(b => b.Id == employee.BranchId.Value)
            .Select(b => b.CompanyId).SingleOrDefaultAsync();
        if (!companyId.HasValue)
            throw new InvalidOperationException("Accounting boundary verification could not resolve the employee branch company.");
        db.HrScopeCompanyId = companyId.Value;
        var loanCount = await db.EmployeeLoans.CountAsync();
        var advanceCount = await db.EmployeeAdvances.CountAsync();
        var scheduleCount = await db.LoanRepaymentSchedules.CountAsync();
        var journalCount = await db.GlVoucherHeaders.CountAsync();
        long loanId = 0, advanceId = 0;
        await using (var transaction = await db.Database.BeginTransactionAsync(System.Data.IsolationLevel.ReadCommitted))
        {
            try
            {
                var repository = new LoanRepository(db);
                var service = new LoanDeductionService(repository);
                var loan = await service.CreateLoanApplicationAsync(new CreateLoanApplicationDto(employee.EmployeeCode, "PERSONAL", 100m, 2,
                    new DateTime(2026, 11, 1), "INT01_VERIFY", "JOD"), "HR_VERIFY");
                loanId = loan.Id;
                await service.ApproveLoanAsync(loanId, "HR_VERIFY");
                await service.ApproveLoanAsync(loanId, "HR_RETRY");
                var advance = await service.CreateAdvanceRequestAsync(new CreateAdvanceRequestDto(employee.EmployeeCode, 100m, "2026-11", "INT01_VERIFY", "JOD"), "HR_VERIFY");
                advanceId = advance.Id;
                await service.ApproveAdvanceAsync(advanceId, "HR_VERIFY");
                await service.ApproveAdvanceAsync(advanceId, "HR_RETRY");
                var storedLoan = await db.EmployeeLoans.AsNoTracking().SingleAsync(l => l.Id == loanId);
                var storedAdvance = await db.EmployeeAdvances.AsNoTracking().SingleAsync(a => a.Id == advanceId);
                if (storedLoan.Status != "APPROVED" || storedAdvance.Status != "APPROVED" ||
                    storedLoan.ApprovedBy != "HR_VERIFY" || storedAdvance.ApprovedBy != "HR_VERIFY" ||
                    storedLoan.RemainingBalance != 100m || storedLoan.TotalPaidAmount != 0m ||
                    storedAdvance.RemainingBalance != 100m || storedAdvance.DeductedAmount != 0m)
                    throw new InvalidOperationException("HR-only approval activated, settled or rewrote its first approval actor.");
                if ((await repository.GetPendingSchedulesForPeriodAsync("2026-12")).Any(s => s.EmployeeLoanId == loanId))
                    throw new InvalidOperationException("An unfunded approved loan entered payroll deduction eligibility.");
                var preview = await service.CalculateAndApplyDeductionsAsync(null, employee.EmployeeCode, "2026-11", 1000m, null);
                if (preview.UpdatedAdvances.Any(a => a.Id == advanceId))
                    throw new InvalidOperationException("An unfunded approved advance entered deduction preview.");
                var blocked = false;
                try { await service.CalculateAndApplyDeductionsAsync(null, employee.EmployeeCode, "2026-11", 1000m, null, true); }
                catch (InvalidOperationException e) when (e.Message.Contains("Accounting posting receipt")) { blocked = true; }
                if (!blocked || journalCount != await db.GlVoucherHeaders.CountAsync())
                    throw new InvalidOperationException("HR approval attempted a financial settlement or journal.");
                report.Add("Oracle HR/Accounting boundary: loan/advance approvals persisted as APPROVED only; repeated approval preserved the first actor, balances remained unchanged, unfunded requests were excluded from deduction eligibility/preview, and HR repayment persistence was rejected. No accounting journal created.");
            }
            finally { await transaction.RollbackAsync(); db.ChangeTracker.Clear(); }
        }
        if (loanCount != await db.EmployeeLoans.CountAsync() || advanceCount != await db.EmployeeAdvances.CountAsync() ||
            scheduleCount != await db.LoanRepaymentSchedules.CountAsync() || journalCount != await db.GlVoucherHeaders.CountAsync() ||
            await db.EmployeeLoans.AnyAsync(l => l.Id == loanId) || await db.EmployeeAdvances.AnyAsync(a => a.Id == advanceId))
            throw new InvalidOperationException("HR boundary fixture rollback failed.");
        report.Add("HR boundary rollback verified: fixture loans/advances/schedules absent and original counts preserved. No handoff/inbox, funding/payment receipt, financial posting, Legacy migration, DDL or deployment is proved by this fixture.");
    }
}
