using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace ThinkOnErp.Infrastructure.Data;

public class DesignTimeDbContextFactory : IDesignTimeDbContextFactory<OracleDbContext>
{
    public OracleDbContext CreateDbContext(string[] args)
    {
        var connectionString = Environment.GetEnvironmentVariable("ConnectionStrings__OracleDb")
            ?? "User Id=THINKON_ERP;Password=thinkon_erp;Data Source=178.104.126.99:1539/free;";

        var optionsBuilder = new DbContextOptionsBuilder<OracleDbContext>();
        optionsBuilder.UseOracle(connectionString, b =>
        {
            b.MigrationsAssembly(typeof(OracleDbContext).Assembly.FullName);
            b.UseOracleSQLCompatibility(OracleSQLCompatibility.DatabaseVersion19);
        });

        return new OracleDbContext(optionsBuilder.Options);
    }
}
