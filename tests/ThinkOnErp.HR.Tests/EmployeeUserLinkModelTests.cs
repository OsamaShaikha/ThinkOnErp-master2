using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata;
using ThinkOnErp.Domain.Entities;
using ThinkOnErp.Domain.Entities.Hr;
using ThinkOnErp.Infrastructure.Data;
using Xunit;

namespace ThinkOnErp.HR.Tests;

public sealed class EmployeeUserLinkModelTests
{
    [Fact]
    public void EmployeeSerialization_NeverExposesLinkedAccountCredentials()
    {
        var employee = new Employee { UserId = 12, User = new SysUser { Id = 12, Password = "sensitive-password" } };
        var json = System.Text.Json.JsonSerializer.Serialize(employee);
        Assert.DoesNotContain("sensitive-password", json);
        Assert.Contains("\"UserId\":12", json);
    }

    [Fact]
    public void EmployeeUserLink_IsOptionalUniqueAndNeverCascadesDeletion()
    {
        var options = new DbContextOptionsBuilder<OracleDbContext>()
            .UseOracle("User Id=unused;Password=unused;Data Source=localhost/unused",
                o => o.UseOracleSQLCompatibility(OracleSQLCompatibility.DatabaseVersion19)).Options;
        using var context = new OracleDbContext(options);
        var entity = context.Model.FindEntityType(typeof(Employee))!;
        var property = entity.FindProperty(nameof(Employee.UserId))!;
        Assert.True(property.IsNullable);
        Assert.Equal("USER_ID", property.GetColumnName(StoreObjectIdentifier.Table("HR_EMPLOYEE", null)));
        Assert.True(entity.GetIndexes().Single(i => i.Properties.Contains(property)).IsUnique);
        var relationship = entity.GetForeignKeys().Single(k => k.Properties.Contains(property));
        Assert.Equal(typeof(SysUser), relationship.PrincipalEntityType.ClrType);
        Assert.Equal(DeleteBehavior.Restrict, relationship.DeleteBehavior);
        Assert.True(relationship.IsUnique);
    }
}
