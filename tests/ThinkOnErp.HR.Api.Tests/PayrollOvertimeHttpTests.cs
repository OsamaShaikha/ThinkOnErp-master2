using System.Net;
using Microsoft.EntityFrameworkCore;
using ThinkOnErp.Application.DTOs.Hr;
using ThinkOnErp.Domain.Entities;
using ThinkOnErp.Domain.Entities.Hr;
using ThinkOnErp.Infrastructure.Data;
using Xunit;

namespace ThinkOnErp.HR.Api.Tests;

public sealed partial class HrAttendanceHttpTests
{
    [Fact]
    public async Task AttendanceOvertimeHttpIsTenantIsolatedIdempotentAndBranchScoped()
    {
        await SeedOvertimeAdjustmentTenant(1, basicSalary: 1200m);
        await SeedOvertimeAdjustmentTenant(2, basicSalary: 2400m);

        var sourceDate = new DateTime(2026, 11, 5);
        var companyOneDto = new CreateOvertimePayrollAdjustmentDto(1, "E-OT", sourceDate, "P26", "OT_ALLOWANCE", 7);
        using var firstOne = await Send("POST", "/api/hr/adjustments/overtime", companyOneDto, company: 1);
        Assert.True(firstOne.StatusCode == HttpStatusCode.OK, await firstOne.Content.ReadAsStringAsync());
        var companyOneResult = await Data(firstOne);
        Assert.Equal("PENDING", companyOneResult.GetProperty("status").GetString());
        Assert.Equal(12.5m, companyOneResult.GetProperty("amount").GetDecimal());
        var companyOneId = companyOneResult.GetProperty("id").GetInt64();

        using var replayOne = await Send("POST", "/api/hr/adjustments/overtime", companyOneDto, company: 1);
        Assert.Equal(HttpStatusCode.OK, replayOne.StatusCode);
        Assert.Equal(companyOneId, (await Data(replayOne)).GetProperty("id").GetInt64());

        using (var db = new OracleDbContext(Options(1)))
        {
            db.Add(new SysScreenFeature { ScreenId = 998, FeatureId = 72 });
            db.Add(new SysUserScreenPermission { Id = 982, UserId = 12, BranchId = 7, ScreenId = 998, FeatureId = 72, IsGranted = true });
            await db.SaveChangesAsync();
        }
        using var approved = await Send("POST", $"/api/hr/adjustments/{companyOneId}/approve",
            new PayrollAdjustmentDecisionDto("Reviewed from HTTP", 7), company: 1);
        Assert.True(approved.StatusCode == HttpStatusCode.OK, await approved.Content.ReadAsStringAsync());
        var approvedResult = await Data(approved);
        Assert.Equal("APPROVED", approvedResult.GetProperty("status").GetString());
        Assert.Equal("Reviewed from HTTP", approvedResult.GetProperty("decisionReason").GetString());
        Assert.Equal("MOBILE_USER", approvedResult.GetProperty("updateUser").GetString());

        var companyTwoDto = companyOneDto with { CompanyId = 2 };
        using var firstTwo = await Send("POST", "/api/hr/adjustments/overtime", companyTwoDto, company: 2);
        Assert.Equal(HttpStatusCode.OK, firstTwo.StatusCode);
        var companyTwoResult = await Data(firstTwo);
        Assert.Equal("PENDING", companyTwoResult.GetProperty("status").GetString());
        Assert.Equal(25m, companyTwoResult.GetProperty("amount").GetDecimal());

        using var crossTenantBody = await Send("POST", "/api/hr/adjustments/overtime", companyTwoDto, company: 1);
        Assert.Equal(HttpStatusCode.Forbidden, crossTenantBody.StatusCode);
        using var wrongBranch = await Send("POST", "/api/hr/adjustments/overtime",
            companyOneDto with { BranchId = 8 }, company: 1);
        Assert.Equal(HttpStatusCode.Forbidden, wrongBranch.StatusCode);

        using var companyOneDb = new OracleDbContext(Options(1));
        using var companyTwoDb = new OracleDbContext(Options(2));
        var one = Assert.Single(await companyOneDb.PayrollAdjustments.AsNoTracking().ToListAsync());
        var two = Assert.Single(await companyTwoDb.PayrollAdjustments.AsNoTracking().ToListAsync());
        Assert.Equal(companyOneId, one.Id);
        Assert.Equal(companyTwoResult.GetProperty("id").GetInt64(), two.Id);
        Assert.Equal("ATTENDANCE_OVERTIME", one.SourceType);
        Assert.Equal("ATTENDANCE_OVERTIME", two.SourceType);
        Assert.Equal(12.5m, one.Amount);
        Assert.Equal(25m, two.Amount);
    }

    private async Task SeedOvertimeAdjustmentTenant(int companyId, decimal basicSalary)
    {
        using var db = new OracleDbContext(Options(companyId));
        db.AddRange(
            new SysScreen { Id = 998, SystemId = 50, ScreenCode = "hr-payroll-adjustments", IsActive = true },
            new SysScreenFeature { ScreenId = 998, FeatureId = 61 },
            new SysScreenFeature { ScreenId = 998, FeatureId = 62 },
            new SysUserScreenPermission { Id = 980, UserId = 12, BranchId = 7, ScreenId = 998, FeatureId = 61, IsGranted = true },
            new SysUserScreenPermission { Id = 981, UserId = 12, BranchId = 7, ScreenId = 998, FeatureId = 62, IsGranted = true },
            new Employee
            {
                EmployeeCode = "E-OT", BranchId = 7, IsActive = true, EmploymentStatus = "ACTIVE",
                HireDate = new DateTime(2020, 1, 1)
            },
            new AttendanceDay
            {
                Id = 55, EmployeeCode = "E-OT", AttendanceDate = new DateTime(2026, 11, 5),
                Status = "PRESENT", OvertimeHours = 2m, CheckInBranchId = 7
            },
            new PayrollPeriod
            {
                Id = 100, CompanyId = companyId, PeriodCode = "P26", Status = "OPEN",
                StartDate = new DateTime(2026, 11, 1), EndDate = new DateTime(2026, 11, 30)
            },
            new SalaryComponent { ComponentCode = "OT_ALLOWANCE", ComponentType = "ALLOWANCE", IsActive = true },
            new SalaryStructure
            {
                Id = 200, EmployeeCode = "E-OT", EffectiveFrom = new DateTime(2020, 1, 1),
                BasicSalary = basicSalary, IsActive = true
            },
            new OvertimeRule
            {
                Id = 300, CompanyId = companyId, Code = "OT_REG", EffectiveFrom = new DateTime(2020, 1, 1),
                DayType = "REGULAR", Multiplier = 1.25m, MinimumMinutes = 30, MaximumMinutes = 240,
                HourlyDivisorFormula = "MONTHLY_SALARY / 240", RequiresApproval = true, IsActive = true
            });
        await db.SaveChangesAsync();
    }
}
