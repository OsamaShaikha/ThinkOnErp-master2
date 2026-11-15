using Microsoft.EntityFrameworkCore;
using ThinkOnErp.Domain.Entities.Hr;
using ThinkOnErp.Infrastructure.Data;
using ThinkOnErp.Infrastructure.Repositories.Hr;
using Xunit;

namespace ThinkOnErp.HR.Tests;

public sealed class PayrollCurrencyPrecisionRepositoryTests
{
    [Fact]
    public async Task PayrollPrecisionIsCompanyScopedAndNormalizedByCurrencyCode()
    {
        await using var db = new OracleDbContext(new DbContextOptionsBuilder<OracleDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString("N")).Options);
        db.PayrollCurrencyPrecisions.AddRange(
            new PayrollCurrencyPrecision { CompanyId = 1, CurrencyCode = "USD", DecimalPlaces = 0 },
            new PayrollCurrencyPrecision { CompanyId = 1, CurrencyCode = "JOD", DecimalPlaces = 4 },
            new PayrollCurrencyPrecision { CompanyId = 2, CurrencyCode = "USD", DecimalPlaces = 3 });
        await db.SaveChangesAsync();

        var repository = new PolicyRepository(db);
        var firstCompany = await repository.GetConfiguredPayrollCurrencyDecimalsAsync(1);
        var secondCompany = await repository.GetConfiguredPayrollCurrencyDecimalsAsync(2);

        Assert.Equal(0, firstCompany["USD"]);
        Assert.Equal(4, firstCompany["JOD"]);
        Assert.Single(secondCompany);
        Assert.Equal(3, secondCompany["USD"]);
    }

    [Fact]
    public async Task SaveUpdatesExistingPrecisionWithoutChangingCreationAudit()
    {
        await using var db = new OracleDbContext(new DbContextOptionsBuilder<OracleDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString("N")).Options);
        db.PayrollCurrencyPrecisions.Add(new PayrollCurrencyPrecision
        {
            CompanyId = 1, CurrencyCode = "USD", DecimalPlaces = 2,
            CreationUser = "first", CreationDate = new DateTime(2026, 1, 1)
        });
        await db.SaveChangesAsync();
        var repository = new PolicyRepository(db);

        await repository.SavePayrollCurrencyPrecisionAsync(new PayrollCurrencyPrecision
        {
            CompanyId = 1, CurrencyCode = "USD", DecimalPlaces = 4, CreationUser = "ignored",
            UpdateUser = "editor", UpdateDate = new DateTime(2026, 2, 1)
        });
        await repository.SaveChangesAsync();

        var saved = await db.PayrollCurrencyPrecisions.SingleAsync();
        Assert.Equal(4, saved.DecimalPlaces);
        Assert.Equal("first", saved.CreationUser);
        Assert.Equal(new DateTime(2026, 1, 1), saved.CreationDate);
        Assert.Equal("editor", saved.UpdateUser);
    }

    [Fact]
    public async Task DeleteRemovesOnlyRequestedCompanyOverride()
    {
        await using var db = new OracleDbContext(new DbContextOptionsBuilder<OracleDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString("N")).Options);
        db.PayrollCurrencyPrecisions.AddRange(
            new PayrollCurrencyPrecision { CompanyId = 1, CurrencyCode = "USD", DecimalPlaces = 0 },
            new PayrollCurrencyPrecision { CompanyId = 2, CurrencyCode = "USD", DecimalPlaces = 3 });
        await db.SaveChangesAsync();
        var repository = new PolicyRepository(db);

        Assert.True(await repository.DeletePayrollCurrencyPrecisionAsync(1, "USD"));
        await repository.SaveChangesAsync();

        var remaining = await db.PayrollCurrencyPrecisions.SingleAsync();
        Assert.Equal(2, remaining.CompanyId);
        Assert.Equal(3, remaining.DecimalPlaces);
        Assert.False(await repository.DeletePayrollCurrencyPrecisionAsync(1, "USD"));
    }
}
