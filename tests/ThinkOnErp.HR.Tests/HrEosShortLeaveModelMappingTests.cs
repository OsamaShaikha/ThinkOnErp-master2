using Microsoft.EntityFrameworkCore;
using ThinkOnErp.Domain.Entities.Hr;
using ThinkOnErp.Infrastructure.Data;
using Xunit;

namespace ThinkOnErp.HR.Tests;

public sealed class HrEosShortLeaveModelMappingTests
{
    [Fact]
    public void EndOfServiceAndShortLeave_UseEmployeeCodeAsRestrictiveForeignKey()
    {
        var options = new DbContextOptionsBuilder<OracleDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;

        using var db = new OracleDbContext(options);
        AssertEmployeeLink<EndOfServiceSettlement>(db);
        AssertEmployeeLink<ShortLeaveRequest>(db);
    }

    private static void AssertEmployeeLink<TEntity>(OracleDbContext db) where TEntity : class
    {
        var entity = db.Model.FindEntityType(typeof(TEntity));
        Assert.NotNull(entity);
        Assert.DoesNotContain(entity!.GetProperties(), property => property.IsShadowProperty());

        var employeeLink = Assert.Single(entity.GetForeignKeys(), foreignKey =>
            foreignKey.PrincipalEntityType.ClrType == typeof(Employee));
        Assert.Equal(new[] { "EmployeeCode" }, employeeLink.Properties.Select(property => property.Name));
        Assert.Equal(DeleteBehavior.Restrict, employeeLink.DeleteBehavior);
    }
}
