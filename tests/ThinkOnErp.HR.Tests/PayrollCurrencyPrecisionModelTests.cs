using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Metadata;
using ThinkOnErp.Domain.Entities.Hr;
using ThinkOnErp.Infrastructure.Data;
using Xunit;

namespace ThinkOnErp.HR.Tests;

public sealed class PayrollCurrencyPrecisionModelTests
{
    [Fact]
    public void PrecisionOverrideMapsToCompanyScopedHrTableWithRangeConstraint()
    {
        using var db = new OracleDbContext(new DbContextOptionsBuilder<OracleDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString("N")).Options);
        var model = db.GetService<IDesignTimeModel>().Model;
        var entity = model.FindEntityType(typeof(PayrollCurrencyPrecision));

        Assert.NotNull(entity);
        Assert.Equal("HR_PAYROLL_CURRENCY_PRECISION", entity!.GetTableName());
        Assert.Equal(new[] { nameof(PayrollCurrencyPrecision.CompanyId), nameof(PayrollCurrencyPrecision.CurrencyCode) },
            entity.FindPrimaryKey()!.Properties.Select(p => p.Name).ToArray());
        Assert.Equal("NUMBER(2)", entity.FindProperty(nameof(PayrollCurrencyPrecision.DecimalPlaces))!
            .FindAnnotation("Relational:ColumnType")?.Value);
        Assert.Contains(entity.GetCheckConstraints(), check =>
            check.Name == "CK_HR_PAY_CURRENCY_DECIMALS" && check.Sql.Contains("BETWEEN 0 AND 4", StringComparison.Ordinal));

        Assert.Null(model.FindEntityType(typeof(ThinkOnErp.Domain.Entities.SysCurrency))!
            .FindProperty("DecimalPlaces"));
    }
}
