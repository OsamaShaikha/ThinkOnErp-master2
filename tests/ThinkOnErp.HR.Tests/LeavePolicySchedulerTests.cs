using Moq;
using ThinkOnErp.Application.DTOs.Hr;
using ThinkOnErp.Application.Services.Hr;
using ThinkOnErp.Domain.Entities.Hr;
using ThinkOnErp.Domain.Interfaces.Hr;
using Xunit;

namespace ThinkOnErp.HR.Tests;

public sealed class LeavePolicySchedulerTests
{
    [Fact]
    public async Task UpdatingSettings_RequiresTimeZoneWhenEnablingAndPersistsTenantChoice()
    {
        var repository = new Mock<ILeaveRepository>();
        repository.Setup(r => r.GetHrLeaveSettingsAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new HrLeaveSettings { Id = 1, BalanceDisplayFormat = "DECIMAL" });
        repository.Setup(r => r.HasLeaveBalancesAsync(It.IsAny<CancellationToken>())).ReturnsAsync(false);
        HrLeaveSettings? saved = null;
        repository.Setup(r => r.SaveHrLeaveSettingsAsync(It.IsAny<HrLeaveSettings>(), It.IsAny<int>(), It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .Callback<HrLeaveSettings, int, int, CancellationToken>((settings, _, _, _) => saved = settings)
            .Returns(Task.CompletedTask);
        var service = new LeaveService(repository.Object, Mock.Of<IEmployeeRepository>());

        await Assert.ThrowsAsync<ArgumentException>(() => service.UpdateHrLeaveSettingsAsync(
            new UpdateHrLeaveSettingsDto(1, 1, "DECIMAL", null, true, null), "ADMIN"));

        var result = await service.UpdateHrLeaveSettingsAsync(
            new UpdateHrLeaveSettingsDto(1, 1, "DECIMAL", null, true, "UTC", " jo "), "ADMIN");

        Assert.NotNull(saved);
        Assert.True(saved!.AutomaticAccrualEnabled);
        Assert.Equal("UTC", saved.AccrualTimeZoneId);
        Assert.Equal("JO", saved.EligibleCitizenshipCountryCode);
        Assert.True(result.AutomaticAccrualEnabled);
        Assert.Equal("UTC", result.AccrualTimeZoneId);
        Assert.Equal("JO", result.EligibleCitizenshipCountryCode);
    }

    [Fact]
    public async Task UpdatingSettings_PersistsCompanyConfiguredLeaveCashoutBasisAndPayrollComponent()
    {
        var repository = new Mock<ILeaveRepository>();
        repository.Setup(r => r.GetHrLeaveSettingsAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new HrLeaveSettings { Id = 1, BalanceDisplayFormat = "DECIMAL" });
        repository.Setup(r => r.HasLeaveBalancesAsync(It.IsAny<CancellationToken>())).ReturnsAsync(false);
        HrLeaveSettings? saved = null;
        repository.Setup(r => r.SaveHrLeaveSettingsAsync(It.IsAny<HrLeaveSettings>(), It.IsAny<int>(), It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .Callback<HrLeaveSettings, int, int, CancellationToken>((settings, _, _, _) => saved = settings)
            .Returns(Task.CompletedTask);
        var employees = new Mock<IEmployeeRepository>();
        employees.Setup(r => r.GetSalaryComponentByCodeAsync("LEAVE_CASHOUT", It.IsAny<CancellationToken>()))
            .ReturnsAsync(new SalaryComponent { ComponentCode = "LEAVE_CASHOUT", ComponentType = "ALLOWANCE", IsActive = true });

        var service = new LeaveService(repository.Object, employees.Object);
        var result = await service.UpdateHrLeaveSettingsAsync(new UpdateHrLeaveSettingsDto(
            1, 1, "DECIMAL", null,
            LeaveCashoutWageBasisCode: "basic", LeaveCashoutDayDivisor: 26m, LeaveCashoutPayComponentCode: "leave_cashout"), "ADMIN");

        Assert.Equal("BASIC", result.LeaveCashoutWageBasisCode);
        Assert.Null(result.LeaveCashoutBasisComponentCode);
        Assert.Equal(26m, result.LeaveCashoutDayDivisor);
        Assert.Equal("LEAVE_CASHOUT", result.LeaveCashoutPayComponentCode);
        Assert.NotNull(saved);
        Assert.Equal("BASIC", saved!.LeaveCashoutWageBasisCode);
        Assert.Equal(26m, saved.LeaveCashoutDayDivisor);
        Assert.Equal("LEAVE_CASHOUT", saved.LeaveCashoutPayComponentCode);
    }

    [Fact]
    public async Task UpdatingSettings_RejectsCashoutWithoutPositiveCompanyDivisor()
    {
        var repository = new Mock<ILeaveRepository>();
        repository.Setup(r => r.GetHrLeaveSettingsAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new HrLeaveSettings { Id = 1, BalanceDisplayFormat = "DECIMAL" });
        var service = new LeaveService(repository.Object, Mock.Of<IEmployeeRepository>());

        await Assert.ThrowsAsync<ArgumentException>(() => service.UpdateHrLeaveSettingsAsync(new UpdateHrLeaveSettingsDto(
            1, 1, "DECIMAL", null, LeaveCashoutWageBasisCode: "GROSS_EARNINGS", LeaveCashoutPayComponentCode: "LEAVE_CASHOUT"), "ADMIN"));
    }

    [Fact]
    public async Task CitizensPolicyUsesExplicitNormalizedCountryKeysAndCompanySetting()
    {
        var today = DateTime.UtcNow.Date;
        var employees = new[]
        {
            new Employee { EmployeeCode = "KEY-JO", NameLocal = "Key", Nationality = "Free text differs", CitizenshipCountryCode = "jo", HireDate = today.AddYears(-3), EmploymentStatus = "ACTIVE", IsActive = true },
            new Employee { EmployeeCode = "TEXT-ONLY", NameLocal = "Text", Nationality = "Jordanian", HireDate = today.AddYears(-3), EmploymentStatus = "ACTIVE", IsActive = true },
            new Employee { EmployeeCode = "OTHER-KEY", NameLocal = "Other", Nationality = "Jordanian", CitizenshipCountryCode = "US", HireDate = today.AddYears(-3), EmploymentStatus = "ACTIVE", IsActive = true }
        };
        var employeeRepository = new Mock<IEmployeeRepository>();
        employeeRepository.Setup(r => r.GetAllEmployeesForExportAsync(null, null, "ACTIVE", null, It.IsAny<CancellationToken>()))
            .ReturnsAsync(employees);
        var policy = new LeavePolicy { Id = 1, LeaveTypeCode = "ANNUAL", AccrualMethod = "MONTHLY", AnnualEntitlementDays = 12m,
            ApplicableTo = "CITIZENS", CycleBasis = "CALENDAR_YEAR", IsActive = true, EffectiveFrom = today.AddYears(-5) };
        var repository = new Mock<ILeaveRepository>();
        repository.Setup(r => r.GetLeaveTypeByCodeAsync("ANNUAL", It.IsAny<CancellationToken>()))
            .ReturnsAsync(new LeaveType { LeaveTypeCode = "ANNUAL", NameLocal = "Annual", TracksBalance = true, IsActive = true, MaxDaysPerYear = 12m });
        var settings = new HrLeaveSettings { LeaveYearStartMonth = 1, LeaveYearStartDay = 1, EligibleCitizenshipCountryCode = " JO " };
        repository.Setup(r => r.GetHrLeaveSettingsAsync(It.IsAny<CancellationToken>())).ReturnsAsync(settings);
        repository.Setup(r => r.GetLeavePolicyVersionsAsync("ANNUAL", It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<LeavePolicy> { policy });
        repository.Setup(r => r.GetLeavePolicyByTypeCodeAsync("ANNUAL", It.IsAny<CancellationToken>(), It.IsAny<DateTime?>()))
            .ReturnsAsync(policy);
        var updatedEmployees = new List<string>();
        repository.Setup(r => r.ApplyAccrualTargetAsync(It.IsAny<LeaveBalance>(), It.IsAny<int>(), It.IsAny<int>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .Callback<LeaveBalance, int, int, string, CancellationToken>((balance, _, _, _, _) => updatedEmployees.Add(balance.EmployeeCode))
            .ReturnsAsync(1m);
        repository.Setup(r => r.SaveChangesAsync(It.IsAny<CancellationToken>())).Returns(Task.CompletedTask);

        var result = await new LeaveService(repository.Object, employeeRepository.Object)
            .RunMonthlyAccrualAsync(new MonthlyLeaveAccrualDto(today.Year, today.Month, "ANNUAL", null), "SCHEDULER");

        Assert.Equal(3, result.TotalEmployeesProcessed);
        Assert.Equal(1, result.UpdatedBalancesCount);
        Assert.Equal(new[] { "KEY-JO" }, updatedEmployees);

        settings.EligibleCitizenshipCountryCode = null;
        updatedEmployees.Clear();
        var unconfigured = await new LeaveService(repository.Object, employeeRepository.Object)
            .RunMonthlyAccrualAsync(new MonthlyLeaveAccrualDto(today.Year, today.Month, "ANNUAL", null), "SCHEDULER");
        Assert.Equal(0, unconfigured.UpdatedBalancesCount);
        Assert.Empty(updatedEmployees);
    }

    [Fact]
    public void CountryCodeRulesNormalizeAsciiAlpha2AndRejectOtherShapes()
    {
        Assert.Equal("JO", CountryCodeRules.NormalizeOptional(" jo "));
        Assert.Null(CountryCodeRules.NormalizeOptional(null));
        Assert.Equal(string.Empty, CountryCodeRules.NormalizeOptional(" "));
        Assert.Throws<ArgumentException>(() => CountryCodeRules.NormalizeOptional("J"));
        Assert.Throws<ArgumentException>(() => CountryCodeRules.NormalizeOptional("J1"));
        Assert.Throws<ArgumentException>(() => CountryCodeRules.NormalizeOptional("ع ر"));
    }

    [Fact]
    public async Task ReplacingPolicy_ClosesCurrentVersionAndStartsTomorrow()
    {
        var today = DateTime.UtcNow.Date;
        var existing = new LeavePolicy
        {
            Id = 17, LeaveTypeCode = "ANNUAL", PolicyName = "Old", AccrualMethod = "MONTHLY",
            AccrualRate = 1m, ApplicableTo = "ALL", CycleBasis = "COMPANY_LEAVE_YEAR",
            Tier1Days = 14m, Tier2Days = 21m, IsActive = true, EffectiveFrom = today.AddYears(-1)
        };
        var repository = new Mock<ILeaveRepository>();
        repository.Setup(r => r.GetHrLeaveSettingsAsync(It.IsAny<CancellationToken>())).ReturnsAsync(new HrLeaveSettings());
        repository.Setup(r => r.GetLeaveTypeByCodeAsync("ANNUAL", It.IsAny<CancellationToken>()))
            .ReturnsAsync(new LeaveType { LeaveTypeCode = "ANNUAL", IsActive = true });
        repository.Setup(r => r.ExecuteForLeavePolicyAsync("ANNUAL", It.IsAny<Func<Task<LeavePolicyDetailsDto>>>(), It.IsAny<CancellationToken>()))
            .Returns((string _, Func<Task<LeavePolicyDetailsDto>> action, CancellationToken _) => action());
        repository.Setup(r => r.GetLeavePolicyVersionsAsync("ANNUAL", It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<LeavePolicy> { existing });
        LeavePolicy? added = null;
        repository.Setup(r => r.UpdateLeavePolicyAsync(existing, It.IsAny<CancellationToken>())).Returns(Task.CompletedTask);
        repository.Setup(r => r.AddLeavePolicyAsync(It.IsAny<LeavePolicy>(), It.IsAny<CancellationToken>()))
            .Callback<LeavePolicy, CancellationToken>((policy, _) => added = policy)
            .Returns(Task.CompletedTask);
        repository.Setup(r => r.SaveChangesAsync(It.IsAny<CancellationToken>())).Returns(Task.CompletedTask);
        var service = new LeaveService(repository.Object, Mock.Of<IEmployeeRepository>());
        var dto = new CreateLeavePolicyDto("New", "ANNUAL", "MONTHLY", 1m, "ALL", 0, 5, 14m, 21m, true,
            14m, "COMPANY_LEAVE_YEAR");

        await service.CreateOrUpdateLeavePolicyAsync(dto, "ADMIN");

        Assert.Equal(today, existing.EffectiveTo);
        Assert.Equal(today.AddDays(1), added!.EffectiveFrom);
        Assert.Null(added.EffectiveTo);
    }

    [Fact]
    public async Task NewPolicyVersion_RejectsOverlapWithAnyHistoricalVersion()
    {
        var today = DateTime.UtcNow.Date;
        var versions = new List<LeavePolicy>
        {
            new() { Id = 1, LeaveTypeCode = "ANNUAL", EffectiveFrom = today.AddDays(-10), EffectiveTo = today.AddDays(3) },
            new() { Id = 2, LeaveTypeCode = "ANNUAL", EffectiveFrom = today.AddDays(-1), EffectiveTo = null }
        };
        var repository = new Mock<ILeaveRepository>();
        repository.Setup(r => r.GetHrLeaveSettingsAsync(It.IsAny<CancellationToken>())).ReturnsAsync(new HrLeaveSettings());
        repository.Setup(r => r.GetLeaveTypeByCodeAsync("ANNUAL", It.IsAny<CancellationToken>()))
            .ReturnsAsync(new LeaveType { LeaveTypeCode = "ANNUAL", IsActive = true });
        repository.Setup(r => r.ExecuteForLeavePolicyAsync("ANNUAL", It.IsAny<Func<Task<LeavePolicyDetailsDto>>>(), It.IsAny<CancellationToken>()))
            .Returns((string _, Func<Task<LeavePolicyDetailsDto>> action, CancellationToken _) => action());
        repository.Setup(r => r.GetLeavePolicyVersionsAsync("ANNUAL", It.IsAny<CancellationToken>())).ReturnsAsync(versions);
        var service = new LeaveService(repository.Object, Mock.Of<IEmployeeRepository>());
        var dto = new CreateLeavePolicyDto("New", "ANNUAL", "MONTHLY", 1m, "ALL", 0, 5, 14m, 21m, true);

        await Assert.ThrowsAsync<InvalidOperationException>(() => service.CreateOrUpdateLeavePolicyAsync(dto, "ADMIN"));
        repository.Verify(r => r.AddLeavePolicyAsync(It.IsAny<LeavePolicy>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task YearlySchedulerAllocation_ResolvesEmployeeCycleFromAsOfDate()
    {
        var today = DateTime.UtcNow.Date;
        var employee = new Employee
        {
            EmployeeCode = "EMP-1", NameLocal = "Test", HireDate = today.AddYears(-3),
            EmploymentStatus = "ACTIVE", IsActive = true
        };
        var employeeRepository = new Mock<IEmployeeRepository>();
        employeeRepository.Setup(r => r.GetAllEmployeesForExportAsync(null, null, "ACTIVE", null, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new[] { employee });
        var policy = new LeavePolicy
        {
            Id = 1, LeaveTypeCode = "ANNUAL", PolicyName = "Annual", AccrualMethod = "YEARLY",
            AccrualRate = 1m, AnnualEntitlementDays = 14m, ApplicableTo = "ALL", CycleBasis = "COMPANY_LEAVE_YEAR",
            IsActive = true, EffectiveFrom = today.AddYears(-1)
        };
        var repository = new Mock<ILeaveRepository>();
        repository.Setup(r => r.GetLeaveTypeByCodeAsync("ANNUAL", It.IsAny<CancellationToken>()))
            .ReturnsAsync(new LeaveType { LeaveTypeCode = "ANNUAL", NameLocal = "Annual", IsActive = true, MaxDaysPerYear = 14m });
        repository.Setup(r => r.GetLeavePolicyByTypeCodeAsync("ANNUAL", It.IsAny<CancellationToken>(), It.IsAny<DateTime?>()))
            .ReturnsAsync(policy);
        repository.Setup(r => r.GetHrLeaveSettingsAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new HrLeaveSettings { LeaveYearStartMonth = 1, LeaveYearStartDay = 1 });
        LeaveBalance? target = null;
        repository.Setup(r => r.ApplyAccrualTargetAsync(It.IsAny<LeaveBalance>(), It.IsAny<int>(), It.IsAny<int>(), "SCHEDULER", It.IsAny<CancellationToken>()))
            .Callback<LeaveBalance, int, int, string, CancellationToken>((balance, _, _, _, _) => target = balance)
            .ReturnsAsync(0m);
        repository.Setup(r => r.SaveChangesAsync(It.IsAny<CancellationToken>())).Returns(Task.CompletedTask);
        var service = new LeaveService(repository.Object, employeeRepository.Object);

        await service.RunYearlyAllocationAsync(
            new YearlyLeaveAllocationDto(1900, "ANNUAL", null, null, false, today), "SCHEDULER");

        Assert.NotNull(target);
        Assert.Equal(new DateTime(today.Year, 1, 1), target!.CycleStartDate);
        Assert.Equal(new DateTime(today.Year, 12, 31), target.CycleEndDate);
    }

    [Fact]
    public async Task MonthlyAccrual_DoesNotUseLegacyFallbackWhenPolicyHistoryHasNoEffectiveVersion()
    {
        var today = DateTime.UtcNow.Date;
        var employee = new Employee
        {
            EmployeeCode = "EMP-1", NameLocal = "Test", HireDate = today.AddYears(-3),
            EmploymentStatus = "ACTIVE", IsActive = true
        };
        var employeeRepository = new Mock<IEmployeeRepository>();
        employeeRepository.Setup(r => r.GetAllEmployeesForExportAsync(null, null, "ACTIVE", null, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new[] { employee });
        var repository = new Mock<ILeaveRepository>();
        repository.Setup(r => r.GetLeaveTypeByCodeAsync("ANNUAL", It.IsAny<CancellationToken>()))
            .ReturnsAsync(new LeaveType { LeaveTypeCode = "ANNUAL", IsActive = true, TracksBalance = true, MaxDaysPerYear = 14m });
        repository.Setup(r => r.GetHrLeaveSettingsAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new HrLeaveSettings { LeaveYearStartMonth = 1, LeaveYearStartDay = 1 });
        repository.Setup(r => r.GetLeavePolicyVersionsAsync("ANNUAL", It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<LeavePolicy> { new() { Id = 1, LeaveTypeCode = "ANNUAL", EffectiveFrom = today.AddDays(10) } });
        repository.Setup(r => r.GetLeavePolicyByTypeCodeAsync("ANNUAL", It.IsAny<CancellationToken>(), It.IsAny<DateTime?>()))
            .ReturnsAsync((LeavePolicy?)null);
        var service = new LeaveService(repository.Object, employeeRepository.Object);

        await service.RunMonthlyAccrualAsync(
            new MonthlyLeaveAccrualDto(today.Year, today.Month, "ANNUAL", null), "SCHEDULER");

        repository.Verify(r => r.ApplyAccrualTargetAsync(It.IsAny<LeaveBalance>(), It.IsAny<int>(), It.IsAny<int>(), It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task BalanceAvailability_UsesPendingDaysWithinConfiguredCompanyCycle()
    {
        var today = DateTime.UtcNow.Date;
        var cycleStart = new DateTime(today.Month >= 7 ? today.Year : today.Year - 1, 7, 1);
        var cycleEnd = cycleStart.AddYears(1).AddDays(-1);
        var employeeRepository = new Mock<IEmployeeRepository>();
        employeeRepository.Setup(r => r.GetEmployeeByCodeAsync("EMP-1", It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Employee { EmployeeCode = "EMP-1", NameLocal = "Test", HireDate = today.AddYears(-3), IsActive = true });
        var repository = new Mock<ILeaveRepository>();
        repository.Setup(r => r.GetHrLeaveSettingsAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new HrLeaveSettings { LeaveYearStartMonth = 7, LeaveYearStartDay = 1 });
        repository.Setup(r => r.GetLeaveTypesAsync(true, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<LeaveType> { new() { LeaveTypeCode = "ANNUAL", NameLocal = "Annual", TracksBalance = true } });
        repository.Setup(r => r.GetLeaveBalanceByCycleAsync("EMP-1", "ANNUAL", cycleStart, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new LeaveBalance { EmployeeCode = "EMP-1", LeaveTypeCode = "ANNUAL", YearNo = cycleStart.Year,
                CycleStartDate = cycleStart, CycleEndDate = cycleEnd, AccruedDays = 12m, UsedDays = 3m, CarriedForwardDays = 2m });
        repository.Setup(r => r.IsLeaveCycleClosedAsync("EMP-1", "ANNUAL", cycleStart, cycleEnd, It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);
        repository.Setup(r => r.GetPendingLeaveDaysAsync("EMP-1", "ANNUAL", cycleStart, cycleEnd, null, It.IsAny<CancellationToken>()))
            .ReturnsAsync(1.25m);
        var service = new LeaveService(repository.Object, employeeRepository.Object);

        var result = await service.GetEmployeeBalancesAsync("EMP-1");

        var balance = Assert.Single(result);
        Assert.Equal(1.25m, balance.PendingDays);
        Assert.Equal(9.75m, balance.AvailableDays);
        Assert.Equal(11m, balance.RemainingDays);
        repository.Verify(r => r.GetPendingLeaveDaysAsync("EMP-1", "ANNUAL", cycleStart, cycleEnd, null, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task LeaveRequestCrossingCompanyCycle_IsRejectedBeforeReservation()
    {
        var employeeRepository = new Mock<IEmployeeRepository>();
        employeeRepository.Setup(r => r.GetEmployeeByCodeAsync("EMP-1", It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Employee { EmployeeCode = "EMP-1", NameLocal = "Test", HireDate = new DateTime(2020, 1, 1), IsActive = true });
        var repository = new Mock<ILeaveRepository>();
        repository.Setup(r => r.ExecuteForLeaveEmployeeAsync("EMP-1", It.IsAny<Func<Task<LeaveRequestDto>>>(), It.IsAny<CancellationToken>()))
            .Returns((string _, Func<Task<LeaveRequestDto>> action, CancellationToken _) => action());
        repository.Setup(r => r.GetLeaveTypeByCodeAsync("ANNUAL", It.IsAny<CancellationToken>()))
            .ReturnsAsync(new LeaveType { LeaveTypeCode = "ANNUAL", IsActive = true, TracksBalance = true });
        repository.Setup(r => r.GetHrLeaveSettingsAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new HrLeaveSettings { LeaveYearStartMonth = 7, LeaveYearStartDay = 1 });
        repository.Setup(r => r.GetLeavePolicyByTypeCodeAsync("ANNUAL", It.IsAny<CancellationToken>(), It.IsAny<DateTime?>()))
            .ReturnsAsync((LeavePolicy?)null);
        repository.Setup(r => r.GetLeavePolicyVersionsAsync("ANNUAL", It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<LeavePolicy>());
        var service = new LeaveService(repository.Object, employeeRepository.Object);

        await Assert.ThrowsAsync<InvalidOperationException>(() => service.SubmitLeaveRequestAsync(
            new SubmitLeaveRequestDto("EMP-1", "ANNUAL", new DateTime(2027, 6, 30), new DateTime(2027, 7, 1), 2m, "boundary", null), "EMP-1"));

        repository.Verify(r => r.AddLeaveRequestAsync(It.IsAny<LeaveRequest>(), It.IsAny<CancellationToken>()), Times.Never);
        repository.Verify(r => r.GetPendingLeaveDaysAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<DateTime>(),
            It.IsAny<DateTime>(), It.IsAny<long?>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task BalanceAvailability_UsesPendingDaysWithinEmploymentAnniversaryCycle()
    {
        var today = DateTime.UtcNow.Date;
        var hireDate = new DateTime(2019, 9, 15);
        var cycleStart = new DateTime(today >= new DateTime(today.Year, 9, 15) ? today.Year : today.Year - 1, 9, 15);
        var cycleEnd = cycleStart.AddYears(1).AddDays(-1);
        var employeeRepository = new Mock<IEmployeeRepository>();
        employeeRepository.Setup(r => r.GetEmployeeByCodeAsync("EMP-1", It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Employee { EmployeeCode = "EMP-1", NameLocal = "Test", HireDate = hireDate, IsActive = true });
        var policy = new LeavePolicy { Id = 9, LeaveTypeCode = "ANNUAL", AccrualMethod = "YEARLY", CycleBasis = "EMPLOYMENT_ANNIVERSARY",
            ApplicableTo = "ALL", IsActive = true, EffectiveFrom = new DateTime(2018, 1, 1) };
        var repository = new Mock<ILeaveRepository>();
        repository.Setup(r => r.GetHrLeaveSettingsAsync(It.IsAny<CancellationToken>())).ReturnsAsync(new HrLeaveSettings());
        repository.Setup(r => r.GetLeaveTypesAsync(true, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<LeaveType> { new() { LeaveTypeCode = "ANNUAL", NameLocal = "Annual", TracksBalance = true } });
        repository.Setup(r => r.GetLeavePolicyByTypeCodeAsync("ANNUAL", It.IsAny<CancellationToken>(), It.IsAny<DateTime?>()))
            .ReturnsAsync(policy);
        repository.Setup(r => r.GetLeaveBalanceByCycleAsync("EMP-1", "ANNUAL", cycleStart, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new LeaveBalance { EmployeeCode = "EMP-1", LeaveTypeCode = "ANNUAL", YearNo = cycleStart.Year,
                CycleStartDate = cycleStart, CycleEndDate = cycleEnd, AccruedDays = 10m, UsedDays = 2m, CarriedForwardDays = 1m });
        repository.Setup(r => r.IsLeaveCycleClosedAsync("EMP-1", "ANNUAL", cycleStart, cycleEnd, It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);
        repository.Setup(r => r.GetPendingLeaveDaysAsync("EMP-1", "ANNUAL", cycleStart, cycleEnd, null, It.IsAny<CancellationToken>()))
            .ReturnsAsync(3.25m);
        var service = new LeaveService(repository.Object, employeeRepository.Object);

        var result = await service.GetEmployeeBalancesAsync("EMP-1");

        var balance = Assert.Single(result);
        Assert.Equal(3.25m, balance.PendingDays);
        Assert.Equal(5.75m, balance.AvailableDays);
        Assert.Equal(9m, balance.RemainingDays);
        repository.Verify(r => r.GetPendingLeaveDaysAsync("EMP-1", "ANNUAL", cycleStart, cycleEnd, null, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task SubmitLeaveRequest_RejectsAnOverlappingPendingOrApprovedLeave()
    {
        var employee = new Employee { EmployeeCode = "EMP-1", NameLocal = "Test", HireDate = new DateTime(2020, 1, 1), IsActive = true };
        var employees = new Mock<IEmployeeRepository>();
        employees.Setup(r => r.GetEmployeeByCodeAsync("EMP-1", It.IsAny<CancellationToken>())).ReturnsAsync(employee);
        var repository = new Mock<ILeaveRepository>();
        repository.Setup(r => r.ExecuteForLeaveEmployeeAsync("EMP-1", It.IsAny<Func<Task<LeaveRequestDto>>>(), It.IsAny<CancellationToken>()))
            .Returns((string _, Func<Task<LeaveRequestDto>> action, CancellationToken _) => action());
        repository.Setup(r => r.GetLeaveTypeByCodeAsync("ANNUAL", It.IsAny<CancellationToken>()))
            .ReturnsAsync(new LeaveType { LeaveTypeCode = "ANNUAL", IsActive = true, TracksBalance = false });
        repository.Setup(r => r.GetHrLeaveSettingsAsync(It.IsAny<CancellationToken>())).ReturnsAsync(new HrLeaveSettings());
        repository.Setup(r => r.GetLeavePolicyByTypeCodeAsync("ANNUAL", It.IsAny<CancellationToken>(), It.IsAny<DateTime?>()))
            .ReturnsAsync((LeavePolicy?)null);
        repository.Setup(r => r.GetLeavePolicyVersionsAsync("ANNUAL", It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<LeavePolicy>());
        repository.Setup(r => r.EnsureLeaveCycleOpenAsync("EMP-1", "ANNUAL", It.IsAny<DateTime>(), It.IsAny<DateTime>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);
        repository.Setup(r => r.HasConflictingLeaveAsync("EMP-1", It.IsAny<DateTime>(), It.IsAny<DateTime>(), null, It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);
        var service = new LeaveService(repository.Object, employees.Object);
        var date = DateTime.UtcNow.Date.AddDays(1);
        while (date.DayOfWeek is DayOfWeek.Friday or DayOfWeek.Saturday)
            date = date.AddDays(1);

        var error = await Assert.ThrowsAsync<InvalidOperationException>(() => service.SubmitLeaveRequestAsync(
            new SubmitLeaveRequestDto("EMP-1", "ANNUAL", date, date, 1m, "overlap", null), "EMP-1"));

        Assert.Contains("overlaps these dates", error.Message);
        repository.Verify(r => r.AddLeaveRequestAsync(It.IsAny<LeaveRequest>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Theory]
    [InlineData("employee-login", "other-user", "employee-login")]
    [InlineData("other-user", "employee-login", "employee-login")]
    public async Task ApproveLeaveRequest_RejectsCreatorAndLinkedEmployeeSelfApproval(string creator, string linkedUser, string approver)
    {
        var request = new LeaveRequest
        {
            Id = 901,
            EmployeeCode = "EMP-1",
            LeaveTypeCode = "ANNUAL",
            StartDate = new DateTime(2026, 11, 16),
            EndDate = new DateTime(2026, 11, 16),
            Status = "PENDING",
            CreationUser = creator
        };
        var employees = new Mock<IEmployeeRepository>();
        employees.Setup(r => r.GetLinkedUserNameByEmployeeCodeAsync("EMP-1", It.IsAny<CancellationToken>()))
            .ReturnsAsync(linkedUser);
        var repository = new Mock<ILeaveRepository>();
        repository.Setup(r => r.GetLeaveRequestSnapshotAsync(request.Id, It.IsAny<CancellationToken>())).ReturnsAsync(request);
        repository.Setup(r => r.ExecuteForLeaveEmployeeAsync("EMP-1", It.IsAny<Func<Task<LeaveRequestDto>>>(), It.IsAny<CancellationToken>()))
            .Returns((string _, Func<Task<LeaveRequestDto>> action, CancellationToken _) => action());
        repository.Setup(r => r.GetLeaveRequestByIdAsync(request.Id, It.IsAny<CancellationToken>())).ReturnsAsync(request);
        var service = new LeaveService(repository.Object, employees.Object);

        var error = await Assert.ThrowsAsync<InvalidOperationException>(() => service.ProcessLeaveRequestAsync(
            request.Id, new ProcessLeaveRequestDto(true, null), approver));

        Assert.Contains("Self-approval is not allowed", error.Message);
        Assert.Equal("PENDING", request.Status);
        repository.Verify(r => r.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task SubmittingRequest_ReservesItsDaysAgainstTheNextRequest()
    {
        var today = DateTime.UtcNow.Date;
        var firstStart = today.AddDays(1);
        while (firstStart.DayOfWeek is DayOfWeek.Friday or DayOfWeek.Saturday)
            firstStart = firstStart.AddDays(1);
        var firstEnd = firstStart;
        var firstWorkingDays = 1;
        while (firstWorkingDays < 3)
        {
            firstEnd = firstEnd.AddDays(1);
            if (firstEnd.DayOfWeek is not (DayOfWeek.Friday or DayOfWeek.Saturday))
                firstWorkingDays++;
        }
        var secondDate = firstEnd.AddDays(1);
        while (secondDate.DayOfWeek is DayOfWeek.Friday or DayOfWeek.Saturday)
            secondDate = secondDate.AddDays(1);
        var cycleStart = new DateTime(today.Month >= 7 ? today.Year : today.Year - 1, 7, 1);
        var cycleEnd = cycleStart.AddYears(1).AddDays(-1);
        var employee = new Employee { EmployeeCode = "EMP-1", NameLocal = "Test", HireDate = today.AddYears(-3),
            EmploymentStatus = "ACTIVE", IsActive = true };
        var employees = new Mock<IEmployeeRepository>();
        employees.Setup(r => r.GetEmployeeByCodeAsync("EMP-1", It.IsAny<CancellationToken>())).ReturnsAsync(employee);
        var pending = new List<LeaveRequest>
        {
            new() { Id = 1, EmployeeCode = "EMP-1", LeaveTypeCode = "ANNUAL", StartDate = today.AddDays(-10), EndDate = today.AddDays(-10),
                DaysRequested = 1.25m, Status = "PENDING" },
            new() { Id = 2, EmployeeCode = "EMP-1", LeaveTypeCode = "ANNUAL", StartDate = today.AddDays(-8), EndDate = today.AddDays(-7),
                DaysRequested = 2.5m, Status = "PENDING" }
        };
        var repository = new Mock<ILeaveRepository>();
        repository.Setup(r => r.ExecuteForLeaveEmployeeAsync("EMP-1", It.IsAny<Func<Task<LeaveRequestDto>>>(), It.IsAny<CancellationToken>()))
            .Returns((string _, Func<Task<LeaveRequestDto>> action, CancellationToken _) => action());
        repository.Setup(r => r.GetLeaveTypeByCodeAsync("ANNUAL", It.IsAny<CancellationToken>()))
            .ReturnsAsync(new LeaveType { LeaveTypeCode = "ANNUAL", NameLocal = "Annual", IsActive = true, TracksBalance = true });
        repository.Setup(r => r.GetHrLeaveSettingsAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new HrLeaveSettings { LeaveYearStartMonth = 7, LeaveYearStartDay = 1 });
        repository.Setup(r => r.GetLeavePolicyByTypeCodeAsync("ANNUAL", It.IsAny<CancellationToken>(), It.IsAny<DateTime?>()))
            .ReturnsAsync((LeavePolicy?)null);
        repository.Setup(r => r.GetLeavePolicyVersionsAsync("ANNUAL", It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<LeavePolicy>());
        repository.Setup(r => r.GetLeaveBalanceByCycleAsync("EMP-1", "ANNUAL", cycleStart, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new LeaveBalance { EmployeeCode = "EMP-1", LeaveTypeCode = "ANNUAL", YearNo = cycleStart.Year,
                CycleStartDate = cycleStart, CycleEndDate = cycleEnd, AccruedDays = 9.5m, UsedDays = 2m });
        repository.Setup(r => r.GetPendingLeaveDaysAsync("EMP-1", "ANNUAL", cycleStart, cycleEnd, null, It.IsAny<CancellationToken>()))
            .Returns(() => Task.FromResult(pending.Where(r => r.Status == "PENDING" && r.StartDate <= cycleEnd && r.EndDate >= cycleStart)
                .Sum(r => r.DaysRequested)));
        repository.Setup(r => r.EnsureLeaveCycleOpenAsync("EMP-1", "ANNUAL", cycleStart, cycleEnd, It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);
        repository.Setup(r => r.AddLeaveRequestAsync(It.IsAny<LeaveRequest>(), It.IsAny<CancellationToken>()))
            .Callback<LeaveRequest, CancellationToken>((request, _) => pending.Add(request)).Returns(Task.CompletedTask);
        repository.Setup(r => r.SaveChangesAsync(It.IsAny<CancellationToken>())).Returns(Task.CompletedTask);
        var service = new LeaveService(repository.Object, employees.Object);

        var first = await service.SubmitLeaveRequestAsync(
            new SubmitLeaveRequestDto("EMP-1", "ANNUAL", firstStart, firstEnd, 0.01m, "first", null), "EMP-1");
        Assert.Equal("PENDING", first.Status);
        Assert.Equal(3m, first.DaysRequested);
        await Assert.ThrowsAsync<InvalidOperationException>(() => service.SubmitLeaveRequestAsync(
            new SubmitLeaveRequestDto("EMP-1", "ANNUAL", secondDate, secondDate, 999m, "second", null), "EMP-1"));
        repository.Verify(r => r.AddLeaveRequestAsync(It.IsAny<LeaveRequest>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task CreateOrUpdateLeavePolicy_NullDto_ThrowsArgumentNullException()
    {
        var service = new LeaveService(Mock.Of<ILeaveRepository>(), Mock.Of<IEmployeeRepository>());
        await Assert.ThrowsAsync<ArgumentNullException>(() =>
            service.CreateOrUpdateLeavePolicyAsync(null!, "ADMIN"));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public async Task CreateOrUpdateLeavePolicy_NullOrWhitespaceLeaveTypeCode_ThrowsArgumentException(string? leaveTypeCode)
    {
        var service = new LeaveService(Mock.Of<ILeaveRepository>(), Mock.Of<IEmployeeRepository>());
        var dto = new CreateLeavePolicyDto("Policy", leaveTypeCode!, "MONTHLY", 1m, "ALL", 0, 5, 14m, 21m, true, 14m, "COMPANY_LEAVE_YEAR");
        await Assert.ThrowsAsync<ArgumentException>(() =>
            service.CreateOrUpdateLeavePolicyAsync(dto, "ADMIN"));
    }

    [Fact]
    public async Task CreateOrUpdateLeavePolicy_NegativeMinServiceMonths_ThrowsArgumentOutOfRangeException()
    {
        var service = new LeaveService(Mock.Of<ILeaveRepository>(), Mock.Of<IEmployeeRepository>());
        var dto = new CreateLeavePolicyDto("Policy", "ANNUAL", "MONTHLY", 1m, "ALL", -1, 5, 14m, 21m, true, 14m, "COMPANY_LEAVE_YEAR");
        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() =>
            service.CreateOrUpdateLeavePolicyAsync(dto, "ADMIN"));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-5)]
    public async Task CreateOrUpdateLeavePolicy_ZeroOrNegativeAnnualEntitlement_ThrowsArgumentOutOfRangeException(decimal annualEntitlement)
    {
        var service = new LeaveService(Mock.Of<ILeaveRepository>(), Mock.Of<IEmployeeRepository>());
        var dto = new CreateLeavePolicyDto("Policy", "ANNUAL", "MONTHLY", 1m, "ALL", 0, 5, 14m, 21m, true, annualEntitlement, "COMPANY_LEAVE_YEAR");
        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() =>
            service.CreateOrUpdateLeavePolicyAsync(dto, "ADMIN"));
    }

    [Theory]
    [InlineData(-1, 14, 21, 1)]
    [InlineData(5, -14, 21, 1)]
    [InlineData(5, 14, -21, 1)]
    [InlineData(5, 14, 21, -1)]
    public async Task CreateOrUpdateLeavePolicy_NegativeThresholdsOrRates_ThrowsArgumentOutOfRangeException(
        int tier1Years, decimal tier1Days, decimal tier2Days, decimal accrualRate)
    {
        var service = new LeaveService(Mock.Of<ILeaveRepository>(), Mock.Of<IEmployeeRepository>());
        var dto = new CreateLeavePolicyDto("Policy", "ANNUAL", "MONTHLY", accrualRate, "ALL", 0, tier1Years, tier1Days, tier2Days, true, 14m, "COMPANY_LEAVE_YEAR");
        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() =>
            service.CreateOrUpdateLeavePolicyAsync(dto, "ADMIN"));
    }

    [Theory]
    [InlineData("UNKNOWN")]
    [InlineData("INVALID_GROUP")]
    public async Task CreateOrUpdateLeavePolicy_InvalidApplicability_ThrowsArgumentException(string applicableTo)
    {
        var service = new LeaveService(Mock.Of<ILeaveRepository>(), Mock.Of<IEmployeeRepository>());
        var dto = new CreateLeavePolicyDto("Policy", "ANNUAL", "MONTHLY", 1m, applicableTo, 0, 5, 14m, 21m, true, 14m, "COMPANY_LEAVE_YEAR");
        await Assert.ThrowsAsync<ArgumentException>(() =>
            service.CreateOrUpdateLeavePolicyAsync(dto, "ADMIN"));
    }

    [Theory]
    [InlineData("HOURLY")]
    [InlineData("WEEKLY")]
    public async Task CreateOrUpdateLeavePolicy_InvalidAccrualMethod_ThrowsArgumentException(string accrualMethod)
    {
        var service = new LeaveService(Mock.Of<ILeaveRepository>(), Mock.Of<IEmployeeRepository>());
        var dto = new CreateLeavePolicyDto("Policy", "ANNUAL", accrualMethod, 1m, "ALL", 0, 5, 14m, 21m, true, 14m, "COMPANY_LEAVE_YEAR");
        await Assert.ThrowsAsync<ArgumentException>(() =>
            service.CreateOrUpdateLeavePolicyAsync(dto, "ADMIN"));
    }

    [Theory]
    [InlineData("CUSTOM_CALENDAR")]
    [InlineData("INVALID_BASIS")]
    public async Task CreateOrUpdateLeavePolicy_InvalidCycleBasis_ThrowsArgumentException(string cycleBasis)
    {
        var service = new LeaveService(Mock.Of<ILeaveRepository>(), Mock.Of<IEmployeeRepository>());
        var dto = new CreateLeavePolicyDto("Policy", "ANNUAL", "MONTHLY", 1m, "ALL", 0, 5, 14m, 21m, true, 14m, cycleBasis);
        await Assert.ThrowsAsync<ArgumentException>(() =>
            service.CreateOrUpdateLeavePolicyAsync(dto, "ADMIN"));
    }

    [Fact]
    public async Task CreateOrUpdateLeavePolicy_InactiveLeaveTypeWithActivePolicy_ThrowsInvalidOperationException()
    {
        var repository = new Mock<ILeaveRepository>();
        repository.Setup(r => r.GetLeaveTypeByCodeAsync("INACTIVE_TYPE", It.IsAny<CancellationToken>()))
            .ReturnsAsync(new LeaveType { LeaveTypeCode = "INACTIVE_TYPE", IsActive = false });

        var service = new LeaveService(repository.Object, Mock.Of<IEmployeeRepository>());
        var dto = new CreateLeavePolicyDto("Policy", "INACTIVE_TYPE", "MONTHLY", 1m, "ALL", 0, 5, 14m, 21m, true, 14m, "COMPANY_LEAVE_YEAR");

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            service.CreateOrUpdateLeavePolicyAsync(dto, "ADMIN"));
        Assert.Contains("An active leave policy cannot be assigned to an inactive leave type", ex.Message);
    }

    [Fact]
    public async Task CreateOrUpdateLeavePolicy_BackdatedEffectiveFrom_ThrowsArgumentException()
    {
        var repository = new Mock<ILeaveRepository>();
        repository.Setup(r => r.GetHrLeaveSettingsAsync(It.IsAny<CancellationToken>())).ReturnsAsync(new HrLeaveSettings());
        repository.Setup(r => r.GetLeaveTypeByCodeAsync("ANNUAL", It.IsAny<CancellationToken>()))
            .ReturnsAsync(new LeaveType { LeaveTypeCode = "ANNUAL", IsActive = true });

        var service = new LeaveService(repository.Object, Mock.Of<IEmployeeRepository>());
        var yesterday = DateTime.UtcNow.Date.AddDays(-1);
        var dto = new CreateLeavePolicyDto("Policy", "ANNUAL", "MONTHLY", 1m, "ALL", 0, 5, 14m, 21m, true, 14m, "COMPANY_LEAVE_YEAR",
            EffectiveFrom: yesterday);

        var ex = await Assert.ThrowsAsync<ArgumentException>(() =>
            service.CreateOrUpdateLeavePolicyAsync(dto, "ADMIN"));
        Assert.Contains("A policy version cannot be backdated", ex.Message);
    }

    [Fact]
    public async Task CreateOrUpdateLeavePolicy_StartBeforeOrAtLatestStart_ThrowsInvalidOperationException()
    {
        var today = DateTime.UtcNow.Date;
        var futureDate = today.AddMonths(2);
        var latestPolicy = new LeavePolicy
        {
            Id = 5,
            LeaveTypeCode = "ANNUAL",
            EffectiveFrom = futureDate,
            IsActive = true
        };

        var repository = new Mock<ILeaveRepository>();
        repository.Setup(r => r.GetHrLeaveSettingsAsync(It.IsAny<CancellationToken>())).ReturnsAsync(new HrLeaveSettings());
        repository.Setup(r => r.GetLeaveTypeByCodeAsync("ANNUAL", It.IsAny<CancellationToken>()))
            .ReturnsAsync(new LeaveType { LeaveTypeCode = "ANNUAL", IsActive = true });
        repository.Setup(r => r.ExecuteForLeavePolicyAsync("ANNUAL", It.IsAny<Func<Task<LeavePolicyDetailsDto>>>(), It.IsAny<CancellationToken>()))
            .Returns((string _, Func<Task<LeavePolicyDetailsDto>> action, CancellationToken _) => action());
        repository.Setup(r => r.GetLeavePolicyVersionsAsync("ANNUAL", It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<LeavePolicy> { latestPolicy });

        var service = new LeaveService(repository.Object, Mock.Of<IEmployeeRepository>());
        // Trying to create version starting at or before futureDate (e.g. futureDate or futureDate - 1 day)
        var dto = new CreateLeavePolicyDto("Policy", "ANNUAL", "MONTHLY", 1m, "ALL", 0, 5, 14m, 21m, true, 14m, "COMPANY_LEAVE_YEAR",
            EffectiveFrom: futureDate);

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            service.CreateOrUpdateLeavePolicyAsync(dto, "ADMIN"));
        Assert.Contains("A new policy version must start after the latest existing version", ex.Message);
    }
}
