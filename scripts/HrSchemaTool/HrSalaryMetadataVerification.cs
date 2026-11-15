using Microsoft.EntityFrameworkCore;
using Oracle.ManagedDataAccess.Client;
using ThinkOnErp.Domain.Entities.Hr;
using ThinkOnErp.Infrastructure.Data;
using ThinkOnErp.Infrastructure.Repositories.Hr;

internal static class HrSalaryMetadataVerification
{
    public static async Task Verify(OracleConnection connection,List<string> report)
    {
        using var db=new OracleDbContext(new DbContextOptionsBuilder<OracleDbContext>().UseOracle(connection,
            o=>o.UseOracleSQLCompatibility(OracleSQLCompatibility.DatabaseVersion19)).Options);
        var repository=new EmployeeRepository(db);
        var storedCodes=await db.SysCurrencies.AsNoTracking().Select(c=>c.ShortNameEn).ToListAsync();
        var candidates=storedCodes.GroupBy(SalaryPaymentMetadata.Normalize).Where(g=>g.Count()==1 && g.Key.Length>0 && g.Key.Length<=20).Select(g=>g.Key).OrderBy(c=>c=="JOD").ThenBy(c=>c).ToArray();
        string? currency=null;
        foreach(var candidate in candidates)
        {
            try { await repository.ValidateSalaryMetadataAsync(candidate,"CASH"); currency=candidate; break; }
            catch(ArgumentException) { }
        }
        if(currency==null) throw new InvalidOperationException("Salary metadata fixture requires an unambiguous configured currency fitting the actual salary column capacity.");
        async Task Rejected(string code,string method)
        {
            try { await repository.ValidateSalaryMetadataAsync(code,method); }
            catch(ArgumentException) { return; }
            throw new InvalidOperationException("Invalid salary currency/payment metadata was accepted.");
        }
        await Rejected("UNKNOWN_"+Guid.NewGuid().ToString("N")[..8].ToUpperInvariant(),"CASH");
        await Rejected(currency,"CARD");
        var employee=await db.Employees.AsNoTracking().Select(e=>e.EmployeeCode).FirstAsync();
        var original=await db.SalaryStructures.AsNoTracking().Where(s=>s.EmployeeCode==employee).Select(s=>new { s.Id,s.CurrencyCode,s.PaymentMethod,s.EffectiveTo }).ToListAsync();
        long fixtureId=0;
        await using(var tx=await db.Database.BeginTransactionAsync(System.Data.IsolationLevel.ReadCommitted))
        {
            try
            {
                var from=DateTime.UtcNow.Date;
                while(await db.SalaryStructures.AnyAsync(s=>s.EmployeeCode==employee && s.EffectiveFrom==from)) from=from.AddDays(1);
                await repository.ValidateSalaryMetadataAsync(currency,"CASH");
                var row=new SalaryStructure { EmployeeCode=employee,EffectiveFrom=from,BasicSalary=123.456m,CurrencyCode=currency,PaymentMethod="CASH",IsActive=false,CreationUser="HR_VERIFY",CreationDate=DateTime.UtcNow };
                db.Add(row); await db.SaveChangesAsync(); fixtureId=row.Id;
                var stored=await db.SalaryStructures.AsNoTracking().SingleAsync(s=>s.Id==fixtureId);
                if(stored.CurrencyCode!=currency || stored.PaymentMethod!="CASH" || stored.BasicSalary!=123.456m || stored.IsActive)
                    throw new InvalidOperationException("Explicit salary metadata/precision did not round-trip on Oracle.");
                report.Add("Oracle salary metadata: configured ShortNameEn lookup and actual storage caps accepted an explicit currency/CASH pair; unknown currency and unsupported payment rejected. An inactive transaction-local row verified metadata and three-decimal DML round-trip without closing any salary. No company default or exchange rate was selected.");
            }
            finally { await tx.RollbackAsync(); db.ChangeTracker.Clear(); }
        }
        var after=await db.SalaryStructures.AsNoTracking().Where(s=>s.EmployeeCode==employee).Select(s=>new { s.Id,s.CurrencyCode,s.PaymentMethod,s.EffectiveTo }).ToListAsync();
        if(await db.SalaryStructures.AnyAsync(s=>s.Id==fixtureId) || !original.OrderBy(s=>s.Id).SequenceEqual(after.OrderBy(s=>s.Id)))
            throw new InvalidOperationException("Salary metadata fixture rollback failed.");
        report.Add("Salary metadata rollback verified: fixture row absent and original salary currency/payment/end dates unchanged. This is storage validation/DML, not a successful owning Excel import commit, payroll FX calculation or Oracle HTTP test; no DDL or currency catalog writes.");
    }
}
