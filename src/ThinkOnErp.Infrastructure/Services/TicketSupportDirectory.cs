using System.Text.RegularExpressions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Oracle.ManagedDataAccess.Client;
using ThinkOnErp.Domain.Entities;
using ThinkOnErp.Domain.Interfaces;
using ThinkOnErp.Infrastructure.Data;

namespace ThinkOnErp.Infrastructure.Services;

/// <summary>Resolves tenant support contacts even from a background master-schema scope.</summary>
public sealed class TicketSupportDirectory(OracleDbContext db, IConfiguration configuration) : ITicketSupportDirectory
{
    public async Task<List<SysUser>> GetCompanyAdminsAsync(long companyId)
    {
        if (db.Database.ProviderName != "Oracle.EntityFrameworkCore")
            return await db.SysUsers.AsNoTracking().Where(u => u.CompanyId == companyId && u.IsActive && u.IsAdmin).ToListAsync();
        var company = await db.SysCompanies.AsNoTracking().SingleOrDefaultAsync(c => c.Id == companyId && c.IsActive);
        if (company?.CompanySchema is not { } schema || !Regex.IsMatch(schema, "^[A-Za-z][A-Za-z0-9_$#]{0,127}$"))
            throw new InvalidOperationException("The ticket company has no valid active tenant schema.");
        // Explicit schema qualification avoids changing the active request or a pooled session.
        await using var connection = new OracleConnection(configuration.GetConnectionString("OracleDb"));
        await connection.OpenAsync();
        await using var command = connection.CreateCommand();
        command.BindByName = true;
        command.CommandText = $"SELECT \"Id\", \"USER_NAME\", \"EMAIL\", \"NAME_EN\", \"NAME_LOCAL\" FROM \"{schema}\".\"SYS_USERS\" WHERE \"COMPANY_ID\" = :companyId AND \"IS_ADMIN\" = 1 AND \"IS_ACTIVE\" = 1 AND \"EMAIL\" IS NOT NULL";
        command.Parameters.Add(new OracleParameter("companyId", OracleDbType.Int64) { Value = companyId });
        await using var reader = await command.ExecuteReaderAsync();
        var users = new List<SysUser>();
        while (await reader.ReadAsync())
            users.Add(new SysUser { Id = reader.GetInt64(0), CompanyId = companyId, IsAdmin = true, IsActive = true,
                UserName = reader.IsDBNull(1) ? "" : reader.GetString(1), Email = reader.GetString(2),
                FullNameEn = reader.IsDBNull(3) ? "" : reader.GetString(3), FullNameLocal = reader.IsDBNull(4) ? "" : reader.GetString(4) });
        return users;
    }
}
