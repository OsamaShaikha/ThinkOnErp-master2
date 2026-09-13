using System;
using System.Diagnostics;
using System.Linq;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Oracle.ManagedDataAccess.Client;
using ThinkOnErp.Infrastructure.Data;
using ThinkOnErp.Infrastructure.Services;
using ThinkOnErp.Infrastructure.Repositories;

var loggerFactory = LoggerFactory.Create(builder => builder.AddConsole().SetMinimumLevel(LogLevel.Debug));
var logger = loggerFactory.CreateLogger<ComplianceReporter>();

var optionsBuilder = new DbContextOptionsBuilder<OracleDbContext>();
var connStr = "Data Source=(DESCRIPTION=(ADDRESS=(PROTOCOL=TCP)(HOST=178.104.126.99)(PORT=1539))(CONNECT_DATA=(SERVICE_NAME=free)));User Id=THINKON_ERP;Password=thinkon_erp;Pooling=false;";
using var conn = new OracleConnection(connStr);
conn.Open();
using (var cmd = conn.CreateCommand())
{
    cmd.CommandText = "ALTER SESSION SET CURRENT_SCHEMA = THINKONERP_1122";
    cmd.ExecuteNonQuery();
}

optionsBuilder.UseOracle(conn);
using var dbContext = new OracleDbContext(optionsBuilder.Options);

Console.WriteLine("\n=== Testing QueryDataSubjectAccessEvents with THINKONERP_1122 ===");
var sw = Stopwatch.StartNew();
var startDate = DateTime.UtcNow.AddDays(-30);
var endDate = DateTime.UtcNow;
long dataSubjectId = 1;
try
{
    var countWithMetadata = await dbContext.SysAuditLogs.AsNoTracking().Where(a => a.Metadata != null).CountAsync();
    Console.WriteLine($"Total rows with Metadata not null: {countWithMetadata}");
}
catch (Exception ex)
{
    sw.Stop();
    Console.WriteLine($"Failed after {sw.ElapsedMilliseconds}ms: {ex.Message}");
    if (ex.InnerException != null) Console.WriteLine($"Inner: {ex.InnerException.Message}");
}

Console.WriteLine("\n=== Testing QuerySecurityEventsAsync query ===");
sw.Restart();
try
{
    var query = from al in dbContext.SysAuditLogs
                join u in dbContext.SysUsers on al.ActorId equals u.Id into userJoin
                from u in userJoin.DefaultIfEmpty()
                where al.CreationDate >= startDate
                   && al.CreationDate <= endDate
                   && (al.EventCategory == "Authentication"
                       || al.EventCategory == "Exception"
                       || al.EventCategory == "Security"
                       || al.Severity == "Critical"
                       || al.Severity == "Error"
                       || al.Severity == "Warning")
                orderby al.CreationDate descending
                select new { al.Id, al.CreationDate, al.EventCategory, al.Severity, ActorName = u != null ? u.UserName : null };

    var results = await query.ToListAsync();
    sw.Stop();
    Console.WriteLine($"Security query executed in {sw.ElapsedMilliseconds}ms, count: {results.Count}");
}
catch (Exception ex)
{
    sw.Stop();
    Console.WriteLine($"Security query failed after {sw.ElapsedMilliseconds}ms: {ex.Message}");
}

Console.WriteLine("\n=== Testing QueryFinancialDataAccessEventsAsync query ===");
sw.Restart();
try
{
    var query = from al in dbContext.SysAuditLogs
                join u in dbContext.SysUsers on al.ActorId equals u.Id into userJoin
                from u in userJoin.DefaultIfEmpty()
                join r in dbContext.SysRoles on u.RoleId equals r.Id into roleJoin
                from r in roleJoin.DefaultIfEmpty()
                where al.CreationDate >= startDate
                   && al.CreationDate <= endDate
                   && (al.EntityType.ToUpper().Contains("INVOICE")
                       || al.EntityType.ToUpper().Contains("PAYMENT")
                       || al.EntityType.ToUpper().Contains("TRANSACTION")
                       || al.EntityType.ToUpper().Contains("ACCOUNT")
                       || al.EntityType.ToUpper().Contains("BUDGET")
                       || al.EntityType.ToUpper().Contains("JOURNAL")
                       || al.EntityType.ToUpper().Contains("LEDGER")
                       || al.EntityType.ToUpper().Contains("FINANCIAL")
                       || al.EntityType.ToUpper().Contains("REVENUE")
                       || al.EntityType.ToUpper().Contains("EXPENSE")
                       || al.EntityType.ToUpper().Contains("ASSET")
                       || al.EntityType.ToUpper().Contains("LIABILITY")
                       || dbContext.SysSystems.Any(s => s.Id == al.SystemId && (s.SystemCode == "accounting" || s.SystemCode == "finance")))
                orderby al.CreationDate ascending
                select new { al.Id, al.CreationDate, al.EntityType, ActorName = u != null ? u.UserName : null, RoleName = r != null ? r.RoleNameEn : null };

    var results = await query.ToListAsync();
    sw.Stop();
    Console.WriteLine($"Financial query executed in {sw.ElapsedMilliseconds}ms, count: {results.Count}");
}
catch (Exception ex)
{
    sw.Stop();
    Console.WriteLine($"Financial query failed after {sw.ElapsedMilliseconds}ms: {ex.Message}");
}

