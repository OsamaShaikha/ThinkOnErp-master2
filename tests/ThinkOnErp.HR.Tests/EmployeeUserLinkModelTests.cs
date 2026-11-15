using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata;
using ThinkOnErp.Application.DTOs.Hr;
using ThinkOnErp.Domain.Entities;
using ThinkOnErp.Domain.Entities.Hr;
using ThinkOnErp.Infrastructure.Data;
using Xunit;

namespace ThinkOnErp.HR.Tests;

public sealed class EmployeeUserLinkModelTests
{
    private static OracleDbContext CreateModelContext()
    {
        var options = new DbContextOptionsBuilder<OracleDbContext>()
            .UseOracle("User Id=unused;Password=unused;Data Source=localhost/unused",
                o => o.UseOracleSQLCompatibility(OracleSQLCompatibility.DatabaseVersion19)).Options;
        return new OracleDbContext(options);
    }

    [Fact]
    public void EmployeeSerialization_NeverExposesLinkedAccountCredentials()
    {
        var employee = new Employee { UserId = 12, User = new SysUser { Id = 12, Password = "sensitive-password" } };
        var json = System.Text.Json.JsonSerializer.Serialize(employee);
        Assert.DoesNotContain("sensitive-password", json);
        Assert.Contains("\"UserId\":12", json);
    }

    [Fact]
    public void EmployeeSerialization_WhenUserIsNull_SerializesCleanly()
    {
        var employee = new Employee { EmployeeCode = "E100", UserId = null, User = null };
        var json = System.Text.Json.JsonSerializer.Serialize(employee);
        Assert.Contains("\"EmployeeCode\":\"E100\"", json);
        Assert.Contains("\"UserId\":null", json);
    }

    [Fact]
    public void EmployeeUserLink_IsOptionalUniqueAndNeverCascadesDeletion()
    {
        using var context = CreateModelContext();
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

    [Fact]
    public void EmployeeUserLink_NavigationProperty_PointsToSysUser()
    {
        using var context = CreateModelContext();
        var entity = context.Model.FindEntityType(typeof(Employee))!;
        var property = entity.FindProperty(nameof(Employee.UserId))!;
        var foreignKey = entity.GetForeignKeys().Single(k => k.Properties.Contains(property));
        Assert.NotNull(foreignKey.DependentToPrincipal);
        Assert.Equal(nameof(Employee.User), foreignKey.DependentToPrincipal!.Name);
        Assert.Equal(typeof(SysUser), foreignKey.PrincipalEntityType.ClrType);
    }

    [Fact]
    public void EmployeeUserLink_PrincipalKey_IsSysUserId()
    {
        using var context = CreateModelContext();
        var entity = context.Model.FindEntityType(typeof(Employee))!;
        var property = entity.FindProperty(nameof(Employee.UserId))!;
        var foreignKey = entity.GetForeignKeys().Single(k => k.Properties.Contains(property));
        var principalKeyProperty = Assert.Single(foreignKey.PrincipalKey.Properties);
        Assert.Equal(nameof(SysUser.Id), principalKeyProperty.Name);
    }

    [Fact]
    public void EmployeeUserLink_PropertyColumnMapping_MatchesStandardPrecision()
    {
        using var context = CreateModelContext();
        var entity = context.Model.FindEntityType(typeof(Employee))!;
        var property = entity.FindProperty(nameof(Employee.UserId))!;
        Assert.Equal(typeof(long?), property.ClrType);
        Assert.True(property.IsNullable);
    }

    [Fact]
    public void EmployeeUserLinkDto_EqualityAndValueSemantics()
    {
        var dto1 = new EmployeeUserLinkDto("EMP1", 42L);
        var dto2 = new EmployeeUserLinkDto("EMP1", 42L);
        var dto3 = new EmployeeUserLinkDto("EMP1", 99L);
        var dtoNull = new EmployeeUserLinkDto("EMP1", null);

        Assert.Equal(dto1, dto2);
        Assert.NotEqual(dto1, dto3);
        Assert.NotEqual(dto1, dtoNull);
        Assert.Equal(dto1.GetHashCode(), dto2.GetHashCode());
        Assert.Equal("EMP1", dto1.EmployeeCode);
        Assert.Equal(42L, dto1.UserId);
        Assert.Null(dtoNull.UserId);
    }

    [Fact]
    public void LinkEmployeeUserDto_EqualityAndValueSemantics()
    {
        var req1 = new LinkEmployeeUserDto(100L);
        var req2 = new LinkEmployeeUserDto(100L);
        var reqNull = new LinkEmployeeUserDto(null);

        Assert.Equal(req1, req2);
        Assert.NotEqual(req1, reqNull);
        Assert.Equal(req1.GetHashCode(), req2.GetHashCode());
        Assert.Equal(100L, req1.UserId);
        Assert.Null(reqNull.UserId);
    }

    [Fact]
    public void EmployeeUserLinkDto_JsonSerialization_RoundTrip()
    {
        var original = new EmployeeUserLinkDto("EMP_TEST_100", 999L);
        var json = System.Text.Json.JsonSerializer.Serialize(original);
        var deserialized = System.Text.Json.JsonSerializer.Deserialize<EmployeeUserLinkDto>(json);

        Assert.NotNull(deserialized);
        Assert.Equal(original.EmployeeCode, deserialized!.EmployeeCode);
        Assert.Equal(original.UserId, deserialized.UserId);
    }

    [Fact]
    public void LinkEmployeeUserDto_JsonSerialization_RoundTrip()
    {
        var original = new LinkEmployeeUserDto(555L);
        var json = System.Text.Json.JsonSerializer.Serialize(original);
        var deserialized = System.Text.Json.JsonSerializer.Deserialize<LinkEmployeeUserDto>(json);

        Assert.NotNull(deserialized);
        Assert.Equal(original.UserId, deserialized!.UserId);
    }

    [Fact]
    public void LinkEmployeeUserDto_NullUserId_JsonSerialization_RoundTrip()
    {
        var original = new LinkEmployeeUserDto(null);
        var json = System.Text.Json.JsonSerializer.Serialize(original);
        var deserialized = System.Text.Json.JsonSerializer.Deserialize<LinkEmployeeUserDto>(json);

        Assert.NotNull(deserialized);
        Assert.Null(deserialized!.UserId);
    }

    [Fact]
    public void EmployeeModel_TableNameAndSchema_ConfiguredCorrectly()
    {
        using var context = CreateModelContext();
        var entity = context.Model.FindEntityType(typeof(Employee))!;
        Assert.Equal("HR_EMPLOYEE", entity.GetTableName());
    }

    [Fact]
    public void EmployeeModel_UserIdProperty_HasCorrectDefaultValueOrNullability()
    {
        using var context = CreateModelContext();
        var entity = context.Model.FindEntityType(typeof(Employee))!;
        var property = entity.FindProperty(nameof(Employee.UserId))!;
        Assert.True(property.IsNullable);
        Assert.Null(property.GetDefaultValue());
    }

    [Fact]
    public void EmployeeUserLinkDto_ToString_ContainsEmployeeCodeAndUserId()
    {
        var dto = new EmployeeUserLinkDto("EMP_STR", 123L);
        var str = dto.ToString();
        Assert.Contains("EMP_STR", str);
        Assert.Contains("123", str);
    }

    [Fact]
    public void LinkEmployeeUserDto_ToString_ContainsUserId()
    {
        var dto = new LinkEmployeeUserDto(777L);
        var str = dto.ToString();
        Assert.Contains("777", str);
    }
}
