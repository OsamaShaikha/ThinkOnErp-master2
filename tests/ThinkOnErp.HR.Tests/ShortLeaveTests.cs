using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Moq;
using ThinkOnErp.Application.DTOs.Hr;
using ThinkOnErp.Application.Services.Hr;
using ThinkOnErp.Domain.Entities.Hr;
using ThinkOnErp.Domain.Interfaces.Hr;
using Xunit;

namespace ThinkOnErp.HR.Tests;

public class ShortLeaveTests
{
    private readonly Mock<IShortLeaveRepository> _repoMock = new();
    private readonly Mock<IEmployeeRepository> _empRepoMock = new();
    private readonly ShortLeaveService _service;

    public ShortLeaveTests()
    {
        _service = new ShortLeaveService(_repoMock.Object, _empRepoMock.Object);
    }

    private Task<ShortLeaveDto> SubmitAsync(
        CreateShortLeaveDto dto,
        string user,
        CancellationToken ct = default) =>
        _service.SubmitRequestAsync(dto with { CompanyId = dto.CompanyId > 0 ? dto.CompanyId : 1 }, user, ct);

    private (Employee Employee, ShortLeavePolicy Policy) CreateStandardFixture()
    {
        var employee = new Employee
        {
            EmployeeCode = "EMP_PERM",
            NameLocal = "سارة أحمد",
            HireDate = new DateTime(2022, 1, 1),
            IsActive = true,
            EmploymentStatus = "ACTIVE",
            BranchId = 1,
            SalaryStructures = new List<SalaryStructure>
            {
                new()
                {
                    BasicSalary = 900m,
                    CurrencyCode = "JOD",
                    IsActive = true
                }
            }
        };

        var policy = new ShortLeavePolicy
        {
            Id = 1,
            CompanyId = 1,
            MonthlyPaidMinutesQuota = 120, // 2 hours
            MaxMinutesPerRequest = 120,
            AllowMiddayExit = true,
            ExcessAction = ShortLeaveCompensationTypes.DeductSalary,
            StandardWorkdayHours = 8.0m,
            IsActive = true
        };

        return (employee, policy);
    }

    [Fact]
    public async Task SubmitRequest_Success_WithinMonthlyQuota_MarkedPaid()
    {
        var (emp, policy) = CreateStandardFixture();
        _empRepoMock.Setup(r => r.GetEmployeeByCodeAsync(emp.EmployeeCode, It.IsAny<CancellationToken>()))
            .ReturnsAsync(emp);
        _repoMock.Setup(r => r.GetActivePolicyAsync(1, It.IsAny<CancellationToken>()))
            .ReturnsAsync(policy);
        _repoMock.Setup(r => r.GetMonthlyApprovedMinutesAsync(emp.EmployeeCode, 2026, 11, It.IsAny<CancellationToken>()))
            .ReturnsAsync(30); // 30 minutes already used
        _repoMock.Setup(r => r.HasOverlappingRequestAsync(emp.EmployeeCode, It.IsAny<DateTime>(), It.IsAny<TimeSpan>(), It.IsAny<TimeSpan>(), null, It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);

        var dto = new CreateShortLeaveDto(
            emp.EmployeeCode,
            ShortLeaveTypes.LateArrival,
            new DateTime(2026, 11, 10),
            new TimeSpan(8, 0, 0),
            new TimeSpan(9, 0, 0), // 60 minutes
            "Traffic congestion"
        );

        var result = await SubmitAsync(dto, "USER1");

        Assert.Equal(ShortLeaveStatus.PendingApproval, result.Status);
        Assert.Equal(60, result.DurationMinutes);
        Assert.Equal(ShortLeaveCompensationTypes.Paid, result.CompensationType);
        _repoMock.Verify(r => r.AddRequestAsync(It.IsAny<ShortLeaveRequest>(), It.IsAny<CancellationToken>()), Times.Once);
        _repoMock.Verify(r => r.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task SubmitRequest_RequiresResolvedCompanyId()
    {
        var dto = new CreateShortLeaveDto(
            "EMP_PERM", ShortLeaveTypes.LateArrival, new DateTime(2026, 11, 10),
            new TimeSpan(8, 0, 0), new TimeSpan(9, 0, 0), "Traffic");

        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(
            () => _service.SubmitRequestAsync(dto, "USER1"));

        _empRepoMock.Verify(r => r.GetEmployeeByCodeAsync(
            It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
        _repoMock.Verify(r => r.AddRequestAsync(
            It.IsAny<ShortLeaveRequest>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task SubmitRequest_ExcessMonthlyQuota_SwitchesToDeductSalary()
    {
        var (emp, policy) = CreateStandardFixture();
        policy.ExcessAction = ShortLeaveCompensationTypes.DeductSalary;

        _empRepoMock.Setup(r => r.GetEmployeeByCodeAsync(emp.EmployeeCode, It.IsAny<CancellationToken>()))
            .ReturnsAsync(emp);
        _repoMock.Setup(r => r.GetActivePolicyAsync(1, It.IsAny<CancellationToken>()))
            .ReturnsAsync(policy);
        _repoMock.Setup(r => r.GetMonthlyApprovedMinutesAsync(emp.EmployeeCode, 2026, 11, It.IsAny<CancellationToken>()))
            .ReturnsAsync(100); // 100 min used out of 120
        _repoMock.Setup(r => r.HasOverlappingRequestAsync(emp.EmployeeCode, It.IsAny<DateTime>(), It.IsAny<TimeSpan>(), It.IsAny<TimeSpan>(), null, It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);

        var dto = new CreateShortLeaveDto(
            emp.EmployeeCode,
            ShortLeaveTypes.MiddayExit,
            new DateTime(2026, 11, 10),
            new TimeSpan(12, 0, 0),
            new TimeSpan(13, 0, 0), // 60 minutes -> 100 + 60 = 160 > 120
            "Doctor appointment"
        );

        var result = await SubmitAsync(dto, "USER1");

        Assert.Equal(ShortLeaveCompensationTypes.DeductSalary, result.CompensationType);
    }

    [Fact]
    public async Task SubmitRequest_Throws_WhenMonthlyQuotaExceededAndExcessActionIsReject()
    {
        var (emp, policy) = CreateStandardFixture();
        policy.ExcessAction = "REJECT";

        _empRepoMock.Setup(r => r.GetEmployeeByCodeAsync(emp.EmployeeCode, It.IsAny<CancellationToken>()))
            .ReturnsAsync(emp);
        _repoMock.Setup(r => r.GetActivePolicyAsync(1, It.IsAny<CancellationToken>()))
            .ReturnsAsync(policy);
        _repoMock.Setup(r => r.GetMonthlyApprovedMinutesAsync(emp.EmployeeCode, 2026, 11, It.IsAny<CancellationToken>()))
            .ReturnsAsync(100);

        var dto = new CreateShortLeaveDto(
            emp.EmployeeCode,
            ShortLeaveTypes.EarlyDeparture,
            new DateTime(2026, 11, 10),
            new TimeSpan(16, 0, 0),
            new TimeSpan(17, 0, 0),
            "Family errand"
        );

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            SubmitAsync(dto, "USER1"));

        Assert.Contains("Monthly short leave quota", ex.Message);
    }

    [Fact]
    public async Task SubmitRequest_Throws_WhenDurationExceedsMaxPerRequest()
    {
        var (emp, policy) = CreateStandardFixture();
        policy.MaxMinutesPerRequest = 60; // 1 hour max

        _empRepoMock.Setup(r => r.GetEmployeeByCodeAsync(emp.EmployeeCode, It.IsAny<CancellationToken>()))
            .ReturnsAsync(emp);
        _repoMock.Setup(r => r.GetActivePolicyAsync(1, It.IsAny<CancellationToken>()))
            .ReturnsAsync(policy);

        var dto = new CreateShortLeaveDto(
            emp.EmployeeCode,
            ShortLeaveTypes.LateArrival,
            new DateTime(2026, 11, 10),
            new TimeSpan(8, 0, 0),
            new TimeSpan(10, 0, 0), // 120 minutes
            "Car breakdown"
        );

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            SubmitAsync(dto, "USER1"));

        Assert.Contains("exceeds the maximum allowed limit", ex.Message);
    }

    [Fact]
    public async Task SubmitRequest_Throws_WhenMiddayExitNotAllowedByPolicy()
    {
        var (emp, policy) = CreateStandardFixture();
        policy.AllowMiddayExit = false;

        _empRepoMock.Setup(r => r.GetEmployeeByCodeAsync(emp.EmployeeCode, It.IsAny<CancellationToken>()))
            .ReturnsAsync(emp);
        _repoMock.Setup(r => r.GetActivePolicyAsync(1, It.IsAny<CancellationToken>()))
            .ReturnsAsync(policy);

        var dto = new CreateShortLeaveDto(
            emp.EmployeeCode,
            ShortLeaveTypes.MiddayExit,
            new DateTime(2026, 11, 10),
            new TimeSpan(11, 0, 0),
            new TimeSpan(12, 0, 0),
            "Personal business"
        );

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            SubmitAsync(dto, "USER1"));

        Assert.Contains("Midday exit permissions are not allowed", ex.Message);
    }

    [Fact]
    public async Task SubmitRequest_Throws_WhenOverlappingRequestExists()
    {
        var (emp, policy) = CreateStandardFixture();
        _empRepoMock.Setup(r => r.GetEmployeeByCodeAsync(emp.EmployeeCode, It.IsAny<CancellationToken>()))
            .ReturnsAsync(emp);
        _repoMock.Setup(r => r.GetActivePolicyAsync(1, It.IsAny<CancellationToken>()))
            .ReturnsAsync(policy);
        _repoMock.Setup(r => r.HasOverlappingRequestAsync(emp.EmployeeCode, It.IsAny<DateTime>(), It.IsAny<TimeSpan>(), It.IsAny<TimeSpan>(), null, It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        var dto = new CreateShortLeaveDto(
            emp.EmployeeCode,
            ShortLeaveTypes.LateArrival,
            new DateTime(2026, 11, 10),
            new TimeSpan(8, 0, 0),
            new TimeSpan(9, 0, 0),
            "Overlapping"
        );

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            SubmitAsync(dto, "USER1"));

        Assert.Contains("already overlaps", ex.Message);
    }

    [Fact]
    public async Task ApproveRequest_RejectsWhenPendingOrApprovedLeaveCoversPermissionDate()
    {
        var (employee, _) = CreateStandardFixture();
        var request = new ShortLeaveRequest
        {
            Id = 16,
            CompanyId = 1,
            EmployeeCode = employee.EmployeeCode,
            PermissionDate = new DateTime(2026, 11, 12),
            StartTime = new TimeSpan(8, 0, 0),
            EndTime = new TimeSpan(9, 0, 0),
            DurationMinutes = 60,
            Status = ShortLeaveStatus.PendingApproval,
            Employee = employee
        };
        _repoMock.Setup(r => r.GetByIdAsync(request.Id, It.IsAny<CancellationToken>())).ReturnsAsync(request);
        _repoMock.Setup(r => r.HasConflictingLeaveRequestAsync(employee.EmployeeCode, request.PermissionDate, It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        var error = await Assert.ThrowsAsync<InvalidOperationException>(() => _service.ApproveRequestAsync(request.Id, "HR_MANAGER"));

        Assert.Contains("pending or approved leave request", error.Message);
        Assert.Equal(ShortLeaveStatus.PendingApproval, request.Status);
        _repoMock.Verify(r => r.UpdateRequestAsync(It.IsAny<ShortLeaveRequest>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task ApproveRequest_CalculatesSalaryDeductionAccurately()
    {
        var (emp, policy) = CreateStandardFixture();
        // Basic salary = 900 JOD -> daily rate = 30 JOD -> hourly rate (8h) = 3.75 JOD
        // 90 minutes = 1.5 hours -> deduction = 1.5 * 3.75 = 5.625 JOD

        var request = new ShortLeaveRequest
        {
            Id = 15,
            CompanyId = 1,
            EmployeeCode = emp.EmployeeCode,
            PermissionDate = new DateTime(2026, 11, 12),
            StartTime = new TimeSpan(8, 0, 0),
            EndTime = new TimeSpan(9, 30, 0),
            DurationMinutes = 90,
            Reason = "Medical",
            Status = ShortLeaveStatus.PendingApproval,
            Employee = emp
        };

        _repoMock.Setup(r => r.GetByIdAsync(15, It.IsAny<CancellationToken>()))
            .ReturnsAsync(request);
        _repoMock.Setup(r => r.GetActivePolicyAsync(1, It.IsAny<CancellationToken>()))
            .ReturnsAsync(policy);
        _repoMock.Setup(r => r.GetMonthlyApprovedMinutesAsync(emp.EmployeeCode, 2026, 11, It.IsAny<CancellationToken>()))
            .ReturnsAsync(60); // 60 + 90 = 150 > 120 quota
        _empRepoMock.Setup(r => r.GetActiveSalaryStructureAsync(emp.EmployeeCode, request.PermissionDate, It.IsAny<CancellationToken>()))
            .ReturnsAsync(emp.SalaryStructures[0]);

        var result = await _service.ApproveRequestAsync(15, "HR_MANAGER");

        Assert.Equal(ShortLeaveStatus.Approved, result.Status);
        Assert.Equal(ShortLeaveCompensationTypes.DeductSalary, result.CompensationType);
        Assert.Equal(5.625m, result.DeductedSalaryAmount);
        Assert.Equal("HR_MANAGER", request.ApprovedBy);
        Assert.NotNull(request.ApprovedAtUtc);
    }

    [Fact]
    public async Task ApproveRequest_RefusesSalaryDeductionWithoutActiveSalaryStructure()
    {
        var (emp, policy) = CreateStandardFixture();
        emp.SalaryStructures.Clear();
        var request = new ShortLeaveRequest
        {
            Id = 151, CompanyId = 1, EmployeeCode = emp.EmployeeCode, Employee = emp,
            PermissionDate = new DateTime(2026, 11, 12), StartTime = new TimeSpan(15, 0, 0),
            EndTime = new TimeSpan(15, 30, 0), DurationMinutes = 30, Status = ShortLeaveStatus.PendingApproval
        };
        _repoMock.Setup(r => r.GetByIdAsync(request.Id, It.IsAny<CancellationToken>())).ReturnsAsync(request);
        _repoMock.Setup(r => r.GetActivePolicyAsync(1, It.IsAny<CancellationToken>())).ReturnsAsync(policy);
        _repoMock.Setup(r => r.GetMonthlyApprovedMinutesAsync(emp.EmployeeCode, 2026, 11, It.IsAny<CancellationToken>())).ReturnsAsync(120);
        _empRepoMock.Setup(r => r.GetEmployeeByCodeAsync(emp.EmployeeCode, It.IsAny<CancellationToken>())).ReturnsAsync(emp);

        var error = await Assert.ThrowsAsync<InvalidOperationException>(() => _service.ApproveRequestAsync(request.Id, "HR_MANAGER"));

        Assert.Contains("salary structure effective on the short leave date", error.Message);
        Assert.Equal(ShortLeaveStatus.PendingApproval, request.Status);
        _repoMock.Verify(r => r.UpdateRequestAsync(It.IsAny<ShortLeaveRequest>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task ApproveRequest_RefusesWhenPolicyChangedToRejectAfterSubmission()
    {
        var (emp, policy) = CreateStandardFixture();
        policy.ExcessAction = "REJECT";
        var request = new ShortLeaveRequest
        {
            Id = 152, CompanyId = 1, EmployeeCode = emp.EmployeeCode, Employee = emp,
            PermissionDate = new DateTime(2026, 11, 12), StartTime = new TimeSpan(15, 0, 0),
            EndTime = new TimeSpan(15, 30, 0), DurationMinutes = 30, Status = ShortLeaveStatus.PendingApproval
        };
        _repoMock.Setup(r => r.GetByIdAsync(request.Id, It.IsAny<CancellationToken>())).ReturnsAsync(request);
        _repoMock.Setup(r => r.GetActivePolicyAsync(1, It.IsAny<CancellationToken>())).ReturnsAsync(policy);
        _repoMock.Setup(r => r.GetMonthlyApprovedMinutesAsync(emp.EmployeeCode, 2026, 11, It.IsAny<CancellationToken>())).ReturnsAsync(120);

        await Assert.ThrowsAsync<InvalidOperationException>(() => _service.ApproveRequestAsync(request.Id, "HR_MANAGER"));

        Assert.Equal(ShortLeaveStatus.PendingApproval, request.Status);
        _repoMock.Verify(r => r.UpdateRequestAsync(It.IsAny<ShortLeaveRequest>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Theory]
    [InlineData("")]
    [InlineData("UNKNOWN")]
    [InlineData(null)]
    public async Task SavePolicy_RejectsUnsupportedExcessAction(string? excessAction)
    {
        var dto = new ShortLeavePolicyDto(0, 1, 120, 120, true, excessAction!, "ANNUAL", 8m, true);

        await Assert.ThrowsAsync<ArgumentException>(() => _service.SavePolicyAsync(dto, "ADMIN"));

        _repoMock.Verify(r => r.SavePolicyAsync(It.IsAny<ShortLeavePolicy>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task ApproveRequest_CalculatesConvertedLeaveDaysAccurately()
    {
        var (emp, policy) = CreateStandardFixture();
        policy.ExcessAction = ShortLeaveCompensationTypes.DeductLeaveBalance;
        // 120 minutes out of 480 workday minutes (8h * 60) = 0.25 days

        var request = new ShortLeaveRequest
        {
            Id = 22,
            CompanyId = 1,
            EmployeeCode = emp.EmployeeCode,
            PermissionDate = new DateTime(2026, 11, 14),
            StartTime = new TimeSpan(15, 0, 0),
            EndTime = new TimeSpan(17, 0, 0),
            DurationMinutes = 120,
            Reason = "Official errand",
            Status = ShortLeaveStatus.PendingApproval,
            Employee = emp
        };

        _repoMock.Setup(r => r.GetByIdAsync(22, It.IsAny<CancellationToken>()))
            .ReturnsAsync(request);
        _repoMock.Setup(r => r.GetActivePolicyAsync(1, It.IsAny<CancellationToken>()))
            .ReturnsAsync(policy);
        _repoMock.Setup(r => r.GetMonthlyApprovedMinutesAsync(emp.EmployeeCode, 2026, 11, It.IsAny<CancellationToken>()))
            .ReturnsAsync(60); // 60 + 120 = 180 > 120 quota

        var result = await _service.ApproveRequestAsync(22, "HR_MANAGER");

        Assert.Equal(ShortLeaveStatus.Approved, result.Status);
        Assert.Equal(ShortLeaveCompensationTypes.DeductLeaveBalance, result.CompensationType);
        Assert.Equal(0.2500m, result.ConvertedLeaveDays);
    }

    [Fact]
    public async Task RejectRequest_TransitionsStatus_AndSetsReason()
    {
        var (emp, _) = CreateStandardFixture();
        var request = new ShortLeaveRequest
        {
            Id = 33,
            EmployeeCode = emp.EmployeeCode,
            Status = ShortLeaveStatus.PendingApproval,
            Employee = emp
        };

        _repoMock.Setup(r => r.GetByIdAsync(33, It.IsAny<CancellationToken>()))
            .ReturnsAsync(request);

        var result = await _service.RejectRequestAsync(33, "Operationally critical meeting at that time", "DIRECTOR");

        Assert.Equal(ShortLeaveStatus.Rejected, result.Status);
        Assert.Equal("Operationally critical meeting at that time", request.RejectionReason);
        Assert.Equal("DIRECTOR", request.RejectedBy);
    }

    [Fact]
    public async Task CancelRequest_ReversesApprovedRequest_WithDocumentedReason()
    {
        var (emp, _) = CreateStandardFixture();
        var request = new ShortLeaveRequest
        {
            Id = 44,
            EmployeeCode = emp.EmployeeCode,
            Status = ShortLeaveStatus.Approved,
            ApprovedBy = "MGR",
            Employee = emp
        };

        _repoMock.Setup(r => r.GetByIdAsync(44, It.IsAny<CancellationToken>()))
            .ReturnsAsync(request);

        var result = await _service.CancelRequestAsync(44, "Employee cancelled appointment", "MGR");

        Assert.Equal(ShortLeaveStatus.Cancelled, result.Status);
        Assert.Equal("Employee cancelled appointment", request.CancellationReason);
        Assert.Equal("MGR", request.CancelledBy);
    }

    [Fact]
    public void CalculateExcusedMinutes_AggregatesLateEarlyAndMiddayCorrectly()
    {
        var list = new List<ShortLeaveRequest>
        {
            new() { PermissionType = ShortLeaveTypes.LateArrival, DurationMinutes = 45, Status = ShortLeaveStatus.Approved },
            new() { PermissionType = ShortLeaveTypes.LateArrival, DurationMinutes = 15, Status = ShortLeaveStatus.Approved },
            new() { PermissionType = ShortLeaveTypes.EarlyDeparture, DurationMinutes = 30, Status = ShortLeaveStatus.Approved },
            new() { PermissionType = ShortLeaveTypes.MiddayExit, DurationMinutes = 60, Status = ShortLeaveStatus.Approved },
            new() { PermissionType = ShortLeaveTypes.LateArrival, DurationMinutes = 90, Status = ShortLeaveStatus.Rejected } // Should ignore non-approved
        };

        var (late, early, midday) = _service.CalculateExcusedMinutes(list);

        Assert.Equal(60, late);
        Assert.Equal(30, early);
        Assert.Equal(60, midday);
    }

    [Fact]
    public void ScheduledAttendanceCalculator_ExcusesLateArrival_WhenApprovedPermissionExists()
    {
        var date = new DateOnly(2026, 11, 5);
        var schedule = ScheduledAttendanceTests.Schedule(date, 8, 16); // 08:00 to 16:00
        var policy = new AttendancePolicy { GracePeriodMinutes = 10, AutoDeductLateArrival = false };

        // Employee punched in at 08:45 (45 minutes late).
        // With 0 excused minutes: late = 45 minutes (> 10).
        var standardResult = ScheduledAttendanceCalculator.Calculate(
            "E", date,
            schedule.StartUtc!.Value.UtcDateTime.AddMinutes(45),
            schedule.EndUtc!.Value.UtcDateTime,
            schedule, policy,
            excusedLateMinutes: 0
        );
        Assert.Equal(45, standardResult.LateArrivalMinutes);

        // With 45 excused minutes from approved short leave:
        // rawLate = 45 - 45 = 0 -> late arrival minutes = 0!
        var excusedResult = ScheduledAttendanceCalculator.Calculate(
            "E", date,
            schedule.StartUtc!.Value.UtcDateTime.AddMinutes(45),
            schedule.EndUtc!.Value.UtcDateTime,
            schedule, policy,
            excusedLateMinutes: 45
        );
        Assert.Equal(0, excusedResult.LateArrivalMinutes);
    }

    [Fact]
    public void ScheduledAttendanceCalculator_ExcusesEarlyDeparture_WhenApprovedPermissionExists()
    {
        var date = new DateOnly(2026, 11, 5);
        var schedule = ScheduledAttendanceTests.Schedule(date, 8, 16); // 08:00 to 16:00
        var policy = new AttendancePolicy { EarlyLeaveToleranceMinutes = 5 };

        // Employee punched out 30 minutes early (at 15:30).
        // With 0 excused minutes: early = 30 minutes.
        var standardResult = ScheduledAttendanceCalculator.Calculate(
            "E", date,
            schedule.StartUtc!.Value.UtcDateTime,
            schedule.EndUtc!.Value.UtcDateTime.AddMinutes(-30),
            schedule, policy,
            excusedEarlyMinutes: 0
        );
        Assert.Equal(30, standardResult.EarlyLeaveMinutes);

        // With 30 excused minutes from approved short leave:
        // early = 30 - 30 = 0 -> early leave minutes = 0!
        var excusedResult = ScheduledAttendanceCalculator.Calculate(
            "E", date,
            schedule.StartUtc!.Value.UtcDateTime,
            schedule.EndUtc!.Value.UtcDateTime.AddMinutes(-30),
            schedule, policy,
            excusedEarlyMinutes: 30
        );
        Assert.Equal(0, excusedResult.EarlyLeaveMinutes);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public async Task SubmitRequest_Throws_WhenEmployeeCodeNullOrWhitespace(string? employeeCode)
    {
        var dto = new CreateShortLeaveDto(
            employeeCode!,
            ShortLeaveTypes.LateArrival,
            new DateTime(2026, 11, 10),
            new TimeSpan(8, 0, 0),
            new TimeSpan(9, 0, 0),
            "Valid Reason"
        );

        await Assert.ThrowsAsync<ArgumentException>(() => SubmitAsync(dto, "USER"));
    }

    [Fact]
    public async Task SubmitRequest_Throws_WhenInvalidPermissionType()
    {
        var (emp, _) = CreateStandardFixture();
        var dto = new CreateShortLeaveDto(
            emp.EmployeeCode,
            "INVALID_TYPE",
            new DateTime(2026, 11, 10),
            new TimeSpan(8, 0, 0),
            new TimeSpan(9, 0, 0),
            "Valid Reason"
        );

        var ex = await Assert.ThrowsAsync<ArgumentException>(() => SubmitAsync(dto, "USER"));
        Assert.Contains("Invalid permission type", ex.Message);
    }

    [Fact]
    public async Task SubmitRequest_Throws_WhenEndTimePrecedesOrEqualsStartTime()
    {
        var (emp, _) = CreateStandardFixture();
        var dtoInverted = new CreateShortLeaveDto(
            emp.EmployeeCode,
            ShortLeaveTypes.LateArrival,
            new DateTime(2026, 11, 10),
            new TimeSpan(9, 0, 0),
            new TimeSpan(8, 0, 0), // End < Start
            "Valid Reason"
        );

        var ex = await Assert.ThrowsAsync<ArgumentException>(() => SubmitAsync(dtoInverted, "USER"));
        Assert.Contains("End time must be later than start time", ex.Message);

        var dtoEqual = new CreateShortLeaveDto(
            emp.EmployeeCode,
            ShortLeaveTypes.LateArrival,
            new DateTime(2026, 11, 10),
            new TimeSpan(8, 0, 0),
            new TimeSpan(8, 0, 0), // End == Start
            "Valid Reason"
        );

        await Assert.ThrowsAsync<ArgumentException>(() => SubmitAsync(dtoEqual, "USER"));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public async Task SubmitRequest_Throws_WhenReasonNullOrWhitespace(string? reason)
    {
        var (emp, _) = CreateStandardFixture();
        var dto = new CreateShortLeaveDto(
            emp.EmployeeCode,
            ShortLeaveTypes.LateArrival,
            new DateTime(2026, 11, 10),
            new TimeSpan(8, 0, 0),
            new TimeSpan(9, 0, 0),
            reason!
        );

        var ex = await Assert.ThrowsAsync<ArgumentException>(() => SubmitAsync(dto, "USER"));
        Assert.Contains("A reason is required", ex.Message);
    }

    [Fact]
    public async Task SubmitRequest_Throws_WhenEmployeeNotFound()
    {
        _empRepoMock.Setup(r => r.GetEmployeeByCodeAsync("NOT_EXIST", It.IsAny<CancellationToken>()))
            .ReturnsAsync((Employee?)null);

        var dto = new CreateShortLeaveDto(
            "NOT_EXIST",
            ShortLeaveTypes.LateArrival,
            new DateTime(2026, 11, 10),
            new TimeSpan(8, 0, 0),
            new TimeSpan(9, 0, 0),
            "Doctor Appointment"
        );

        await Assert.ThrowsAsync<KeyNotFoundException>(() => SubmitAsync(dto, "USER"));
    }

    [Fact]
    public async Task SubmitRequest_Throws_WhenEmployeeInactive()
    {
        var (emp, _) = CreateStandardFixture();
        emp.IsActive = false;

        _empRepoMock.Setup(r => r.GetEmployeeByCodeAsync(emp.EmployeeCode, It.IsAny<CancellationToken>()))
            .ReturnsAsync(emp);

        var dto = new CreateShortLeaveDto(
            emp.EmployeeCode,
            ShortLeaveTypes.LateArrival,
            new DateTime(2026, 11, 10),
            new TimeSpan(8, 0, 0),
            new TimeSpan(9, 0, 0),
            "Doctor Appointment"
        );

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() => SubmitAsync(dto, "USER"));
        Assert.Contains("Cannot submit short leave request for inactive employee", ex.Message);
    }

    [Fact]
    public async Task ApproveRequest_Throws_WhenRequestNotFound()
    {
        _repoMock.Setup(r => r.GetByIdAsync(999, It.IsAny<CancellationToken>()))
            .ReturnsAsync((ShortLeaveRequest?)null);

        await Assert.ThrowsAsync<KeyNotFoundException>(() => _service.ApproveRequestAsync(999, "HR"));
    }

    [Theory]
    [InlineData(ShortLeaveStatus.Approved)]
    [InlineData(ShortLeaveStatus.Rejected)]
    [InlineData(ShortLeaveStatus.Cancelled)]
    public async Task ApproveRequest_Throws_WhenRequestNotInPendingApprovalStatus(string currentStatus)
    {
        var request = new ShortLeaveRequest
        {
            Id = 50,
            EmployeeCode = "EMP01",
            Status = currentStatus
        };
        _repoMock.Setup(r => r.GetByIdAsync(50, It.IsAny<CancellationToken>())).ReturnsAsync(request);

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() => _service.ApproveRequestAsync(50, "HR"));
        Assert.Contains("Cannot approve request in status", ex.Message);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public async Task RejectRequest_Throws_WhenReasonNullOrWhitespace(string? reason)
    {
        var ex = await Assert.ThrowsAsync<ArgumentException>(() => _service.RejectRequestAsync(10, reason!, "HR"));
        Assert.Contains("A rejection reason is required", ex.Message);
    }

    [Fact]
    public async Task RejectRequest_Throws_WhenRequestNotFound()
    {
        _repoMock.Setup(r => r.GetByIdAsync(999, It.IsAny<CancellationToken>()))
            .ReturnsAsync((ShortLeaveRequest?)null);

        await Assert.ThrowsAsync<KeyNotFoundException>(() => _service.RejectRequestAsync(999, "Rejected reason", "HR"));
    }

    [Fact]
    public async Task RejectRequest_Throws_WhenRequestNotPending()
    {
        var request = new ShortLeaveRequest
        {
            Id = 51,
            EmployeeCode = "EMP01",
            Status = ShortLeaveStatus.Approved // already approved
        };
        _repoMock.Setup(r => r.GetByIdAsync(51, It.IsAny<CancellationToken>())).ReturnsAsync(request);

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() => _service.RejectRequestAsync(51, "Valid Reason", "HR"));
        Assert.Contains("Cannot reject request in status", ex.Message);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public async Task CancelRequest_Throws_WhenReasonNullOrWhitespace(string? reason)
    {
        var ex = await Assert.ThrowsAsync<ArgumentException>(() => _service.CancelRequestAsync(10, reason!, "HR"));
        Assert.Contains("A cancellation reason is required", ex.Message);
    }

    [Fact]
    public async Task CancelRequest_Throws_WhenRequestNotFound()
    {
        _repoMock.Setup(r => r.GetByIdAsync(999, It.IsAny<CancellationToken>()))
            .ReturnsAsync((ShortLeaveRequest?)null);

        await Assert.ThrowsAsync<KeyNotFoundException>(() => _service.CancelRequestAsync(999, "Cancellation reason", "HR"));
    }

    [Fact]
    public async Task CancelRequest_Throws_WhenRequestAlreadyCancelled()
    {
        var request = new ShortLeaveRequest
        {
            Id = 52,
            EmployeeCode = "EMP01",
            Status = ShortLeaveStatus.Cancelled
        };
        _repoMock.Setup(r => r.GetByIdAsync(52, It.IsAny<CancellationToken>())).ReturnsAsync(request);

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() => _service.CancelRequestAsync(52, "Cancel reason", "HR"));
        Assert.Contains("already cancelled", ex.Message);
    }

    [Fact]
    public async Task CancelRequest_Throws_WhenRequestAlreadyRejected()
    {
        var request = new ShortLeaveRequest
        {
            Id = 53,
            EmployeeCode = "EMP01",
            Status = ShortLeaveStatus.Rejected
        };
        _repoMock.Setup(r => r.GetByIdAsync(53, It.IsAny<CancellationToken>())).ReturnsAsync(request);

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() => _service.CancelRequestAsync(53, "Cancel reason", "HR"));
        Assert.Contains("Cannot cancel an already rejected", ex.Message);
    }

    [Fact]
    public async Task GetByIdAsync_ReturnsDtoWhenFound_AndNullWhenNotFound()
    {
        var request = new ShortLeaveRequest
        {
            Id = 60,
            EmployeeCode = "EMP01",
            PermissionType = ShortLeaveTypes.LateArrival,
            PermissionDate = new DateTime(2026, 11, 10),
            StartTime = new TimeSpan(8, 0, 0),
            EndTime = new TimeSpan(9, 0, 0),
            DurationMinutes = 60,
            Status = ShortLeaveStatus.PendingApproval,
            CompensationType = ShortLeaveCompensationTypes.Paid
        };
        _repoMock.Setup(r => r.GetByIdAsync(60, It.IsAny<CancellationToken>())).ReturnsAsync(request);
        _repoMock.Setup(r => r.GetByIdAsync(999, It.IsAny<CancellationToken>())).ReturnsAsync((ShortLeaveRequest?)null);

        var found = await _service.GetByIdAsync(60);
        Assert.NotNull(found);
        Assert.Equal(60, found!.Id);

        var notFound = await _service.GetByIdAsync(999);
        Assert.Null(notFound);
    }

    [Fact]
    public async Task GetPolicyAsync_And_SavePolicyAsync_DelegatesToRepository()
    {
        var policy = new ShortLeavePolicy
        {
            Id = 1,
            CompanyId = 1,
            MonthlyPaidMinutesQuota = 120,
            MaxMinutesPerRequest = 60,
            AllowMiddayExit = true,
            ExcessAction = ShortLeaveCompensationTypes.DeductSalary,
            StandardWorkdayHours = 8.0m,
            IsActive = true
        };

        _repoMock.Setup(r => r.GetActivePolicyAsync(1, It.IsAny<CancellationToken>())).ReturnsAsync(policy);

        var policyDto = await _service.GetPolicyAsync(1);
        Assert.Equal(120, policyDto.MonthlyPaidMinutesQuota);
        Assert.Equal(60, policyDto.MaxMinutesPerRequest);
        Assert.True(policyDto.AllowMiddayExit);

        var saveDto = new ShortLeavePolicyDto(1, 1, 180, 90, false, ShortLeaveCompensationTypes.DeductLeaveBalance, "ANNUAL", 8.0m, true);
        ShortLeavePolicy? savedEntity = null;
        _repoMock.Setup(r => r.SavePolicyAsync(It.IsAny<ShortLeavePolicy>(), It.IsAny<CancellationToken>()))
            .Callback<ShortLeavePolicy, CancellationToken>((p, _) => savedEntity = p)
            .Returns(Task.CompletedTask);
        _repoMock.Setup(r => r.SaveChangesAsync(It.IsAny<CancellationToken>())).Returns(Task.CompletedTask);

        var savedResult = await _service.SavePolicyAsync(saveDto, "ADMIN");
        Assert.NotNull(savedEntity);
        Assert.Equal(180, savedResult.MonthlyPaidMinutesQuota);
        Assert.Equal(90, savedResult.MaxMinutesPerRequest);
        Assert.False(savedResult.AllowMiddayExit);
        _repoMock.Verify(r => r.SavePolicyAsync(It.IsAny<ShortLeavePolicy>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Theory]
    [InlineData(-1, 60, 8.0)]
    [InlineData(120, 0, 8.0)]
    [InlineData(120, -10, 8.0)]
    [InlineData(120, 60, 0.0)]
    [InlineData(120, 60, -8.0)]
    public async Task SavePolicy_Validation_ThrowsArgumentException_WhenInvalidArgs(int quota, int maxPerReq, decimal workdayHours)
    {
        var saveDto = new ShortLeavePolicyDto(1, 1, quota, maxPerReq, true, ShortLeaveCompensationTypes.DeductSalary, string.Empty, workdayHours, true);
        await Assert.ThrowsAsync<ArgumentException>(() => _service.SavePolicyAsync(saveDto, "ADMIN"));
    }

    [Fact]
    public async Task SavePolicy_WhenPolicyDoesNotExist_CreatesNewPolicyWithCreationUser()
    {
        _repoMock.Setup(r => r.GetActivePolicyAsync(2, It.IsAny<CancellationToken>()))
            .ReturnsAsync((ShortLeavePolicy?)null);

        ShortLeavePolicy? captured = null;
        _repoMock.Setup(r => r.SavePolicyAsync(It.IsAny<ShortLeavePolicy>(), It.IsAny<CancellationToken>()))
            .Callback<ShortLeavePolicy, CancellationToken>((p, _) => captured = p)
            .Returns(Task.CompletedTask);

        var saveDto = new ShortLeavePolicyDto(0, 2, 100, 50, true, ShortLeaveCompensationTypes.DeductSalary, string.Empty, 8.0m, true);
        var result = await _service.SavePolicyAsync(saveDto, "NEW_CREATOR");

        Assert.NotNull(captured);
        Assert.Equal("NEW_CREATOR", captured!.CreationUser);
        Assert.Equal(2, captured.CompanyId);
        Assert.Equal(100, result.MonthlyPaidMinutesQuota);
        Assert.Equal(50, result.MaxMinutesPerRequest);
    }

    [Fact]
    public async Task GetByEmployeeAsync_DelegatesToRepositoryAndMapsNames()
    {
        var emp = new Employee { EmployeeCode = "EMP_MAP", NameLocal = "فاطمة أحمد" };
        var requests = new List<ShortLeaveRequest>
        {
            new()
            {
                Id = 88,
                EmployeeCode = "EMP_MAP",
                Employee = emp,
                PermissionType = ShortLeaveTypes.LateArrival,
                PermissionDate = new DateTime(2026, 11, 15),
                StartTime = new TimeSpan(8, 0, 0),
                EndTime = new TimeSpan(8, 30, 0),
                DurationMinutes = 30,
                Reason = "Traffic",
                Status = ShortLeaveStatus.Approved,
                CompensationType = ShortLeaveCompensationTypes.Paid
            }
        };

        _repoMock.Setup(r => r.GetByEmployeeAsync("EMP_MAP", null, null, It.IsAny<CancellationToken>()))
            .ReturnsAsync(requests);

        var dtos = await _service.GetByEmployeeAsync("EMP_MAP");

        var dto = Assert.Single(dtos);
        Assert.Equal(88, dto.Id);
        Assert.Equal("EMP_MAP", dto.EmployeeCode);
        Assert.Equal("فاطمة أحمد", dto.EmployeeName);
        Assert.Equal(30, dto.DurationMinutes);
    }

    [Fact]
    public async Task GetRequestsAsync_DelegatesToRepositoryAndMapsNames()
    {
        var emp = new Employee { EmployeeCode = "EMP_MAP2", NameLocal = "خالد عمر" };
        var requests = new List<ShortLeaveRequest>
        {
            new()
            {
                Id = 89,
                CompanyId = 3,
                BranchId = 5,
                EmployeeCode = "EMP_MAP2",
                Employee = emp,
                PermissionType = ShortLeaveTypes.EarlyDeparture,
                PermissionDate = new DateTime(2026, 11, 16),
                StartTime = new TimeSpan(16, 0, 0),
                EndTime = new TimeSpan(17, 0, 0),
                DurationMinutes = 60,
                Reason = "Family necessity",
                Status = ShortLeaveStatus.PendingApproval,
                CompensationType = ShortLeaveCompensationTypes.Paid
            }
        };

        _repoMock.Setup(r => r.GetRequestsAsync(3, 5, ShortLeaveStatus.PendingApproval, null, It.IsAny<CancellationToken>()))
            .ReturnsAsync(requests);

        var dtos = await _service.GetRequestsAsync(3, 5, ShortLeaveStatus.PendingApproval);

        var dto = Assert.Single(dtos);
        Assert.Equal(89, dto.Id);
        Assert.Equal("EMP_MAP2", dto.EmployeeCode);
        Assert.Equal("خالد عمر", dto.EmployeeName);
        Assert.Equal(60, dto.DurationMinutes);
    }

    [Fact]
    public void CalculateExcusedMinutes_WithMixedPermissions_CalculatesAccurately()
    {
        var requests = new List<ShortLeaveRequest>
        {
            new() { PermissionType = ShortLeaveTypes.LateArrival, DurationMinutes = 30, Status = ShortLeaveStatus.Approved },
            new() { PermissionType = ShortLeaveTypes.EarlyDeparture, DurationMinutes = 45, Status = ShortLeaveStatus.Approved },
            new() { PermissionType = ShortLeaveTypes.MiddayExit, DurationMinutes = 60, Status = ShortLeaveStatus.Approved },
            new() { PermissionType = ShortLeaveTypes.LateArrival, DurationMinutes = 15, Status = ShortLeaveStatus.PendingApproval } // Non-approved should be ignored
        };

        var (late, early, midday) = _service.CalculateExcusedMinutes(requests);

        Assert.Equal(30, late);
        Assert.Equal(45, early);
        Assert.Equal(60, midday);
    }

    [Fact]
    public async Task SubmitRequest_ThrowsArgumentNullException_WhenDtoIsNull()
    {
        await Assert.ThrowsAsync<ArgumentNullException>(() => _service.SubmitRequestAsync(null!, "USER"));
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData(null)]
    public async Task SubmitRequest_ThrowsArgumentException_WhenEmployeeCodeIsWhitespace(string? invalidCode)
    {
        var dto = new CreateShortLeaveDto(invalidCode!, ShortLeaveTypes.LateArrival, DateTime.UtcNow, TimeSpan.FromHours(8), TimeSpan.FromHours(9), "Traffic");
        await Assert.ThrowsAsync<ArgumentException>(() => SubmitAsync(dto, "USER"));
    }

    [Theory]
    [InlineData("INVALID_TYPE")]
    [InlineData("")]
    [InlineData(null)]
    public async Task SubmitRequest_ThrowsArgumentException_WhenPermissionTypeIsInvalid(string? invalidType)
    {
        var dto = new CreateShortLeaveDto("EMP001", invalidType!, DateTime.UtcNow, TimeSpan.FromHours(8), TimeSpan.FromHours(9), "Traffic");
        await Assert.ThrowsAsync<ArgumentException>(() => SubmitAsync(dto, "USER"));
    }

    [Theory]
    [InlineData(9, 9)] // Equal times
    [InlineData(10, 8)] // End time earlier than start time
    public async Task SubmitRequest_ThrowsArgumentException_WhenEndTimeEarlierOrEqualToStartTime(int startHour, int endHour)
    {
        var dto = new CreateShortLeaveDto("EMP001", ShortLeaveTypes.LateArrival, DateTime.UtcNow, TimeSpan.FromHours(startHour), TimeSpan.FromHours(endHour), "Traffic");
        await Assert.ThrowsAsync<ArgumentException>(() => SubmitAsync(dto, "USER"));
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData(null)]
    public async Task SubmitRequest_ThrowsArgumentException_WhenReasonIsWhitespace(string? invalidReason)
    {
        var dto = new CreateShortLeaveDto("EMP001", ShortLeaveTypes.LateArrival, DateTime.UtcNow, TimeSpan.FromHours(8), TimeSpan.FromHours(9), invalidReason!);
        await Assert.ThrowsAsync<ArgumentException>(() => SubmitAsync(dto, "USER"));
    }

    [Fact]
    public async Task SubmitRequest_ThrowsKeyNotFoundException_WhenEmployeeNotFound()
    {
        _empRepoMock.Setup(r => r.GetEmployeeByCodeAsync("NOT_FOUND", It.IsAny<CancellationToken>()))
            .ReturnsAsync((Employee?)null);

        var dto = new CreateShortLeaveDto("NOT_FOUND", ShortLeaveTypes.LateArrival, DateTime.UtcNow, TimeSpan.FromHours(8), TimeSpan.FromHours(9), "Traffic");
        await Assert.ThrowsAsync<KeyNotFoundException>(() => SubmitAsync(dto, "USER"));
    }

    [Theory]
    [InlineData(false, "ACTIVE")]
    [InlineData(true, "TERMINATED")]
    [InlineData(true, "SUSPENDED")]
    public async Task SubmitRequest_ThrowsInvalidOperationException_WhenEmployeeInactiveOrTerminated(bool isActive, string employmentStatus)
    {
        var emp = new Employee { EmployeeCode = "EMP_INACTIVE", IsActive = isActive, EmploymentStatus = employmentStatus };
        _empRepoMock.Setup(r => r.GetEmployeeByCodeAsync(emp.EmployeeCode, It.IsAny<CancellationToken>()))
            .ReturnsAsync(emp);

        var dto = new CreateShortLeaveDto(emp.EmployeeCode, ShortLeaveTypes.LateArrival, DateTime.UtcNow, TimeSpan.FromHours(8), TimeSpan.FromHours(9), "Traffic");
        await Assert.ThrowsAsync<InvalidOperationException>(() => SubmitAsync(dto, "USER"));
    }

    [Fact]
    public async Task SubmitRequest_ThrowsInvalidOperationException_WhenDurationExceedsPolicyLimit()
    {
        var (emp, policy) = CreateStandardFixture();
        policy.MaxMinutesPerRequest = 60; // Max 1 hour
        _empRepoMock.Setup(r => r.GetEmployeeByCodeAsync(emp.EmployeeCode, It.IsAny<CancellationToken>()))
            .ReturnsAsync(emp);
        _repoMock.Setup(r => r.GetActivePolicyAsync(1, It.IsAny<CancellationToken>()))
            .ReturnsAsync(policy);

        // Request 120 minutes (2 hours)
        var dto = new CreateShortLeaveDto(emp.EmployeeCode, ShortLeaveTypes.LateArrival, DateTime.UtcNow, TimeSpan.FromHours(8), TimeSpan.FromHours(10), "Traffic");
        await Assert.ThrowsAsync<InvalidOperationException>(() => SubmitAsync(dto, "USER"));
    }

    [Fact]
    public async Task SubmitRequest_ThrowsInvalidOperationException_WhenMiddayExitDisallowedByPolicy()
    {
        var (emp, policy) = CreateStandardFixture();
        policy.AllowMiddayExit = false;
        _empRepoMock.Setup(r => r.GetEmployeeByCodeAsync(emp.EmployeeCode, It.IsAny<CancellationToken>()))
            .ReturnsAsync(emp);
        _repoMock.Setup(r => r.GetActivePolicyAsync(1, It.IsAny<CancellationToken>()))
            .ReturnsAsync(policy);

        var dto = new CreateShortLeaveDto(emp.EmployeeCode, ShortLeaveTypes.MiddayExit, DateTime.UtcNow, TimeSpan.FromHours(12), TimeSpan.FromHours(13), "Doctor appointment");
        await Assert.ThrowsAsync<InvalidOperationException>(() => SubmitAsync(dto, "USER"));
    }

    [Fact]
    public async Task SubmitRequest_ThrowsInvalidOperationException_WhenOverlappingRequestExists()
    {
        var (emp, policy) = CreateStandardFixture();
        _empRepoMock.Setup(r => r.GetEmployeeByCodeAsync(emp.EmployeeCode, It.IsAny<CancellationToken>()))
            .ReturnsAsync(emp);
        _repoMock.Setup(r => r.GetActivePolicyAsync(1, It.IsAny<CancellationToken>()))
            .ReturnsAsync(policy);
        _repoMock.Setup(r => r.HasOverlappingRequestAsync(emp.EmployeeCode, It.IsAny<DateTime>(), It.IsAny<TimeSpan>(), It.IsAny<TimeSpan>(), null, It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        var dto = new CreateShortLeaveDto(emp.EmployeeCode, ShortLeaveTypes.LateArrival, DateTime.UtcNow, TimeSpan.FromHours(8), TimeSpan.FromHours(9), "Traffic");
        await Assert.ThrowsAsync<InvalidOperationException>(() => SubmitAsync(dto, "USER"));
    }

    [Fact]
    public async Task SubmitRequest_ThrowsInvalidOperationException_WhenExcessActionIsRejectAndQuotaExceeded()
    {
        var (emp, policy) = CreateStandardFixture();
        policy.MonthlyPaidMinutesQuota = 60;
        policy.ExcessAction = "REJECT";
        _empRepoMock.Setup(r => r.GetEmployeeByCodeAsync(emp.EmployeeCode, It.IsAny<CancellationToken>()))
            .ReturnsAsync(emp);
        _repoMock.Setup(r => r.GetActivePolicyAsync(1, It.IsAny<CancellationToken>()))
            .ReturnsAsync(policy);
        _repoMock.Setup(r => r.GetMonthlyApprovedMinutesAsync(emp.EmployeeCode, It.IsAny<int>(), It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(50); // 50 mins used

        // Request 30 mins: 50 + 30 = 80 > 60 quota
        var dto = new CreateShortLeaveDto(emp.EmployeeCode, ShortLeaveTypes.LateArrival, DateTime.UtcNow, TimeSpan.FromHours(8), TimeSpan.FromHours(8).Add(TimeSpan.FromMinutes(30)), "Traffic");
        await Assert.ThrowsAsync<InvalidOperationException>(() => SubmitAsync(dto, "USER"));
    }

    [Fact]
    public async Task ApproveRequest_ThrowsKeyNotFoundException_WhenRequestNotFound()
    {
        _repoMock.Setup(r => r.GetByIdAsync(999L, It.IsAny<CancellationToken>()))
            .ReturnsAsync((ShortLeaveRequest?)null);

        await Assert.ThrowsAsync<KeyNotFoundException>(() => _service.ApproveRequestAsync(999L, "ADMIN"));
    }

    [Theory]
    [InlineData(ShortLeaveStatus.Approved)]
    [InlineData(ShortLeaveStatus.Rejected)]
    [InlineData(ShortLeaveStatus.Cancelled)]
    public async Task ApproveRequest_ThrowsInvalidOperationException_WhenStatusNotPendingApproval(string nonPendingStatus)
    {
        var request = new ShortLeaveRequest { Id = 100L, Status = nonPendingStatus };
        _repoMock.Setup(r => r.GetByIdAsync(100L, It.IsAny<CancellationToken>()))
            .ReturnsAsync(request);

        await Assert.ThrowsAsync<InvalidOperationException>(() => _service.ApproveRequestAsync(100L, "ADMIN"));
    }

    [Fact]
    public async Task ApproveRequest_WhenExcessActionIsDeductLeaveBalance_CalculatesConvertedDaysAccurately()
    {
        var (emp, policy) = CreateStandardFixture();
        policy.MonthlyPaidMinutesQuota = 60;
        policy.ExcessAction = ShortLeaveCompensationTypes.DeductLeaveBalance;
        policy.StandardWorkdayHours = 8.0m;

        var request = new ShortLeaveRequest
        {
            Id = 200L,
            CompanyId = 1,
            EmployeeCode = emp.EmployeeCode,
            Employee = emp,
            PermissionType = ShortLeaveTypes.MiddayExit,
            PermissionDate = new DateTime(2026, 11, 20),
            StartTime = new TimeSpan(12, 0, 0),
            EndTime = new TimeSpan(14, 0, 0),
            DurationMinutes = 120, // 2 hours
            Status = ShortLeaveStatus.PendingApproval
        };

        _repoMock.Setup(r => r.GetByIdAsync(200L, It.IsAny<CancellationToken>()))
            .ReturnsAsync(request);
        _repoMock.Setup(r => r.GetActivePolicyAsync(1, It.IsAny<CancellationToken>()))
            .ReturnsAsync(policy);
        _repoMock.Setup(r => r.GetMonthlyApprovedMinutesAsync(emp.EmployeeCode, 2026, 11, It.IsAny<CancellationToken>()))
            .ReturnsAsync(60); // Quota already exhausted

        var result = await _service.ApproveRequestAsync(200L, "ADMIN");

        Assert.Equal(ShortLeaveStatus.Approved, result.Status);
        Assert.Equal(ShortLeaveCompensationTypes.DeductLeaveBalance, result.CompensationType);
        // Workday = 8 * 60 = 480 mins; Converted days = 120 / 480 = 0.2500
        Assert.Equal(0.2500m, result.ConvertedLeaveDays);
        Assert.Equal(0m, result.DeductedSalaryAmount);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData(null)]
    public async Task RejectRequest_ThrowsArgumentException_WhenReasonWhitespace(string? invalidReason)
    {
        await Assert.ThrowsAsync<ArgumentException>(() => _service.RejectRequestAsync(100L, invalidReason!, "ADMIN"));
    }

    [Fact]
    public async Task RejectRequest_ThrowsKeyNotFoundException_WhenRequestNotFound()
    {
        _repoMock.Setup(r => r.GetByIdAsync(999L, It.IsAny<CancellationToken>()))
            .ReturnsAsync((ShortLeaveRequest?)null);

        await Assert.ThrowsAsync<KeyNotFoundException>(() => _service.RejectRequestAsync(999L, "Valid reason", "ADMIN"));
    }

    [Theory]
    [InlineData(ShortLeaveStatus.Approved)]
    [InlineData(ShortLeaveStatus.Rejected)]
    [InlineData(ShortLeaveStatus.Cancelled)]
    public async Task RejectRequest_ThrowsInvalidOperationException_WhenStatusNotPendingApproval(string nonPendingStatus)
    {
        var request = new ShortLeaveRequest { Id = 100L, Status = nonPendingStatus };
        _repoMock.Setup(r => r.GetByIdAsync(100L, It.IsAny<CancellationToken>()))
            .ReturnsAsync(request);

        await Assert.ThrowsAsync<InvalidOperationException>(() => _service.RejectRequestAsync(100L, "Valid reason", "ADMIN"));
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData(null)]
    public async Task CancelRequest_ThrowsArgumentException_WhenReasonWhitespace(string? invalidReason)
    {
        await Assert.ThrowsAsync<ArgumentException>(() => _service.CancelRequestAsync(100L, invalidReason!, "USER"));
    }

    [Fact]
    public async Task CancelRequest_ThrowsKeyNotFoundException_WhenRequestNotFound()
    {
        _repoMock.Setup(r => r.GetByIdAsync(999L, It.IsAny<CancellationToken>()))
            .ReturnsAsync((ShortLeaveRequest?)null);

        await Assert.ThrowsAsync<KeyNotFoundException>(() => _service.CancelRequestAsync(999L, "Valid reason", "USER"));
    }

    [Fact]
    public async Task CancelRequest_ThrowsInvalidOperationException_WhenAlreadyCancelled()
    {
        var request = new ShortLeaveRequest { Id = 100L, Status = ShortLeaveStatus.Cancelled };
        _repoMock.Setup(r => r.GetByIdAsync(100L, It.IsAny<CancellationToken>()))
            .ReturnsAsync(request);

        await Assert.ThrowsAsync<InvalidOperationException>(() => _service.CancelRequestAsync(100L, "Valid reason", "USER"));
    }

    [Fact]
    public async Task CancelRequest_ThrowsInvalidOperationException_WhenAlreadyRejected()
    {
        var request = new ShortLeaveRequest { Id = 100L, Status = ShortLeaveStatus.Rejected };
        _repoMock.Setup(r => r.GetByIdAsync(100L, It.IsAny<CancellationToken>()))
            .ReturnsAsync(request);

        await Assert.ThrowsAsync<InvalidOperationException>(() => _service.CancelRequestAsync(100L, "Valid reason", "USER"));
    }

    [Fact]
    public async Task SavePolicy_ThrowsArgumentNullException_WhenDtoIsNull()
    {
        await Assert.ThrowsAsync<ArgumentNullException>(() => _service.SavePolicyAsync(null!, "ADMIN"));
    }

    [Theory]
    [InlineData(-10, 120, 8.0)] // Negative quota
    [InlineData(60, 0, 8.0)]   // Zero max minutes
    [InlineData(60, 120, 0.0)] // Zero workday hours
    public async Task SavePolicy_ThrowsArgumentException_WhenParametersInvalid(int quota, int maxMinutes, decimal workdayHours)
    {
        var dto = new ShortLeavePolicyDto(1, 1, quota, maxMinutes, true, ShortLeaveCompensationTypes.DeductSalary, string.Empty, workdayHours, true);
        await Assert.ThrowsAsync<ArgumentException>(() => _service.SavePolicyAsync(dto, "ADMIN"));
    }

    [Fact]
    public async Task GetByIdAsync_ReturnsNullWhenNotFound_AndMappedDtoWhenFound()
    {
        _repoMock.Setup(r => r.GetByIdAsync(501L, It.IsAny<CancellationToken>()))
            .ReturnsAsync((ShortLeaveRequest?)null);

        var nullResult = await _service.GetByIdAsync(501L);
        Assert.Null(nullResult);

        var request = new ShortLeaveRequest
        {
            Id = 502L,
            EmployeeCode = "EMP_502",
            PermissionType = ShortLeaveTypes.LateArrival,
            Status = ShortLeaveStatus.Approved,
            DurationMinutes = 45
        };
        _repoMock.Setup(r => r.GetByIdAsync(502L, It.IsAny<CancellationToken>()))
            .ReturnsAsync(request);

        var foundResult = await _service.GetByIdAsync(502L);
        Assert.NotNull(foundResult);
        Assert.Equal(502L, foundResult.Id);
        Assert.Equal("EMP_502", foundResult.EmployeeCode);
        Assert.Equal(45, foundResult.DurationMinutes);
    }

    [Fact]
    public async Task GetPolicyAsync_ReturnsDefaultPolicyWhenNotFound_AndMappedDtoWhenPresent()
    {
        _repoMock.Setup(r => r.GetActivePolicyAsync(99L, It.IsAny<CancellationToken>()))
            .ReturnsAsync((ShortLeavePolicy?)null);

        var defaultPolicy = await _service.GetPolicyAsync(99L);
        Assert.NotNull(defaultPolicy);
        Assert.Equal(99L, defaultPolicy.CompanyId);

        var policy = new ShortLeavePolicy
        {
            Id = 15L,
            CompanyId = 100L,
            MonthlyPaidMinutesQuota = 180,
            MaxMinutesPerRequest = 90,
            AllowMiddayExit = true,
            ExcessAction = ShortLeaveCompensationTypes.DeductSalary,
            StandardWorkdayHours = 7.5m,
            IsActive = true
        };
        _repoMock.Setup(r => r.GetActivePolicyAsync(100L, It.IsAny<CancellationToken>()))
            .ReturnsAsync(policy);

        var foundPolicy = await _service.GetPolicyAsync(100L);
        Assert.NotNull(foundPolicy);
        Assert.Equal(15L, foundPolicy.Id);
        Assert.Equal(100L, foundPolicy.CompanyId);
        Assert.Equal(180, foundPolicy.MonthlyPaidMinutesQuota);
        Assert.Equal(7.5m, foundPolicy.StandardWorkdayHours);
    }

    [Fact]
    public async Task SubmitRequestAsync_PropagatesCancellationToken()
    {
        var (emp, _) = CreateStandardFixture();
        using var cts = new CancellationTokenSource();
        cts.Cancel();
        _empRepoMock.Setup(r => r.GetEmployeeByCodeAsync(emp.EmployeeCode, cts.Token))
            .ThrowsAsync(new OperationCanceledException(cts.Token));

        var dto = new CreateShortLeaveDto(
            emp.EmployeeCode,
            ShortLeaveTypes.LateArrival,
            DateTime.Today,
            new TimeSpan(8, 0, 0),
            new TimeSpan(9, 0, 0),
            "Doctor visit",
            1,
            null
        );

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => SubmitAsync(dto, "USER", cts.Token));
    }

    [Fact]
    public async Task ApproveRequestAsync_PropagatesCancellationToken()
    {
        using var cts = new CancellationTokenSource();
        cts.Cancel();
        _repoMock.Setup(r => r.GetByIdAsync(10L, cts.Token))
            .ThrowsAsync(new OperationCanceledException(cts.Token));

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => _service.ApproveRequestAsync(10L, "USER", cts.Token));
    }

    [Fact]
    public async Task RejectRequestAsync_PropagatesCancellationToken()
    {
        using var cts = new CancellationTokenSource();
        cts.Cancel();
        _repoMock.Setup(r => r.GetByIdAsync(10L, cts.Token))
            .ThrowsAsync(new OperationCanceledException(cts.Token));

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => _service.RejectRequestAsync(10L, "Reason", "USER", cts.Token));
    }

    [Fact]
    public async Task CancelRequestAsync_PropagatesCancellationToken()
    {
        using var cts = new CancellationTokenSource();
        cts.Cancel();
        _repoMock.Setup(r => r.GetByIdAsync(10L, cts.Token))
            .ThrowsAsync(new OperationCanceledException(cts.Token));

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => _service.CancelRequestAsync(10L, "Reason", "USER", cts.Token));
    }

    [Fact]
    public async Task GetByIdAsync_PropagatesCancellationToken()
    {
        using var cts = new CancellationTokenSource();
        cts.Cancel();
        _repoMock.Setup(r => r.GetByIdAsync(10L, cts.Token))
            .ThrowsAsync(new OperationCanceledException(cts.Token));

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => _service.GetByIdAsync(10L, cts.Token));
    }

    [Fact]
    public async Task GetByEmployeeAsync_PropagatesCancellationToken()
    {
        using var cts = new CancellationTokenSource();
        cts.Cancel();
        _repoMock.Setup(r => r.GetByEmployeeAsync("EMP", null, null, cts.Token))
            .ThrowsAsync(new OperationCanceledException(cts.Token));

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => _service.GetByEmployeeAsync("EMP", null, null, cts.Token));
    }

    [Fact]
    public async Task GetRequestsAsync_PropagatesCancellationToken()
    {
        using var cts = new CancellationTokenSource();
        cts.Cancel();
        _repoMock.Setup(r => r.GetRequestsAsync(1L, null, null, null, cts.Token))
            .ThrowsAsync(new OperationCanceledException(cts.Token));

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => _service.GetRequestsAsync(1L, null, null, null, cts.Token));
    }

    [Fact]
    public async Task GetPolicyAsync_PropagatesCancellationToken()
    {
        using var cts = new CancellationTokenSource();
        cts.Cancel();
        _repoMock.Setup(r => r.GetActivePolicyAsync(1L, cts.Token))
            .ThrowsAsync(new OperationCanceledException(cts.Token));

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => _service.GetPolicyAsync(1L, cts.Token));
    }

    [Fact]
    public async Task SavePolicyAsync_PropagatesCancellationToken()
    {
        using var cts = new CancellationTokenSource();
        cts.Cancel();
        _repoMock.Setup(r => r.GetActivePolicyAsync(1L, cts.Token))
            .ThrowsAsync(new OperationCanceledException(cts.Token));

        var dto = new ShortLeavePolicyDto(1L, 1L, 120, 60, true, ShortLeaveCompensationTypes.DeductSalary, "", 8.0m, true);
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => _service.SavePolicyAsync(dto, "USER", cts.Token));
    }

    [Fact]
    public async Task CancelRequestAsync_WhenApproved_SucceedsAndSetsStatusCancelled()
    {
        var request = new ShortLeaveRequest
        {
            Id = 50L,
            CompanyId = 1L,
            EmployeeCode = "EMP_PERM",
            PermissionType = ShortLeaveTypes.LateArrival,
            PermissionDate = DateTime.Today,
            StartTime = new TimeSpan(8, 0, 0),
            EndTime = new TimeSpan(9, 0, 0),
            DurationMinutes = 60,
            Reason = "Medical",
            Status = ShortLeaveStatus.Approved,
            ApprovedBy = "SUPERVISOR",
            ApprovedAtUtc = DateTime.UtcNow.AddHours(-1)
        };

        _repoMock.Setup(r => r.GetByIdAsync(50L, It.IsAny<CancellationToken>()))
            .ReturnsAsync(request);

        var result = await _service.CancelRequestAsync(50L, "Appointment rescheduled", "USER");

        Assert.Equal(ShortLeaveStatus.Cancelled, result.Status);
        Assert.Equal("Appointment rescheduled", result.CancellationReason);
        Assert.Equal("USER", result.CancelledBy);
        _repoMock.Verify(r => r.UpdateRequestAsync(request, It.IsAny<CancellationToken>()), Times.Once);
        _repoMock.Verify(r => r.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task CancelRequestAsync_WhenAlreadyCancelled_ThrowsInvalidOperationException()
    {
        var request = new ShortLeaveRequest
        {
            Id = 51L,
            Status = ShortLeaveStatus.Cancelled
        };

        _repoMock.Setup(r => r.GetByIdAsync(51L, It.IsAny<CancellationToken>()))
            .ReturnsAsync(request);

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() => _service.CancelRequestAsync(51L, "Try cancel again", "USER"));
        Assert.Contains("already cancelled", ex.Message);
    }

    [Fact]
    public async Task CancelRequestAsync_WhenRejected_ThrowsInvalidOperationException()
    {
        var request = new ShortLeaveRequest
        {
            Id = 52L,
            Status = ShortLeaveStatus.Rejected
        };

        _repoMock.Setup(r => r.GetByIdAsync(52L, It.IsAny<CancellationToken>()))
            .ReturnsAsync(request);

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() => _service.CancelRequestAsync(52L, "Try cancel rejected", "USER"));
        Assert.Contains("Cannot cancel an already rejected short leave request", ex.Message);
    }

    [Fact]
    public async Task SavePolicyAsync_WhenDtoNull_ThrowsArgumentNullException()
    {
        await Assert.ThrowsAsync<ArgumentNullException>(() => _service.SavePolicyAsync(null!, "USER"));
    }

    [Theory]
    [InlineData(-1)]
    [InlineData(-60)]
    public async Task SavePolicyAsync_WhenMonthlyQuotaNegative_ThrowsArgumentException(int invalidQuota)
    {
        var dto = new ShortLeavePolicyDto(1L, 1L, invalidQuota, 60, true, ShortLeaveCompensationTypes.DeductSalary, "", 8.0m, true);
        var ex = await Assert.ThrowsAsync<ArgumentException>(() => _service.SavePolicyAsync(dto, "USER"));
        Assert.Contains("quota minutes cannot be negative", ex.Message);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-30)]
    public async Task SavePolicyAsync_WhenMaxMinutesZeroOrNegative_ThrowsArgumentException(int invalidMax)
    {
        var dto = new ShortLeavePolicyDto(1L, 1L, 120, invalidMax, true, ShortLeaveCompensationTypes.DeductSalary, "", 8.0m, true);
        var ex = await Assert.ThrowsAsync<ArgumentException>(() => _service.SavePolicyAsync(dto, "USER"));
        Assert.Contains("Max minutes per request must be greater than zero", ex.Message);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-8.0)]
    public async Task SavePolicyAsync_WhenStandardWorkdayHoursZeroOrNegative_ThrowsArgumentException(decimal invalidHours)
    {
        var dto = new ShortLeavePolicyDto(1L, 1L, 120, 60, true, ShortLeaveCompensationTypes.DeductSalary, "", invalidHours, true);
        var ex = await Assert.ThrowsAsync<ArgumentException>(() => _service.SavePolicyAsync(dto, "USER"));
        Assert.Contains("Standard workday hours must be greater than zero", ex.Message);
    }

    [Fact]
    public void CalculateExcusedMinutes_WhenRequestsHaveDifferentStatuses_CountsOnlyApproved()
    {
        var requests = new List<ShortLeaveRequest>
        {
            new() { Status = ShortLeaveStatus.Approved, PermissionType = ShortLeaveTypes.LateArrival, DurationMinutes = 45 },
            new() { Status = ShortLeaveStatus.PendingApproval, PermissionType = ShortLeaveTypes.LateArrival, DurationMinutes = 60 },
            new() { Status = ShortLeaveStatus.Rejected, PermissionType = ShortLeaveTypes.LateArrival, DurationMinutes = 30 },
            new() { Status = ShortLeaveStatus.Cancelled, PermissionType = ShortLeaveTypes.LateArrival, DurationMinutes = 45 }
        };

        var (late, early, midday) = _service.CalculateExcusedMinutes(requests);

        Assert.Equal(45, late);
        Assert.Equal(0, early);
        Assert.Equal(0, midday);
    }

    [Fact]
    public void CalculateExcusedMinutes_WhenAllThreeTypesPresent_SumsEachTypeCorrectly()
    {
        var requests = new List<ShortLeaveRequest>
        {
            new() { Status = ShortLeaveStatus.Approved, PermissionType = ShortLeaveTypes.LateArrival, DurationMinutes = 30 },
            new() { Status = ShortLeaveStatus.Approved, PermissionType = ShortLeaveTypes.LateArrival, DurationMinutes = 15 },
            new() { Status = ShortLeaveStatus.Approved, PermissionType = ShortLeaveTypes.EarlyDeparture, DurationMinutes = 40 },
            new() { Status = ShortLeaveStatus.Approved, PermissionType = ShortLeaveTypes.MiddayExit, DurationMinutes = 90 }
        };

        var (late, early, midday) = _service.CalculateExcusedMinutes(requests);

        Assert.Equal(45, late);
        Assert.Equal(40, early);
        Assert.Equal(90, midday);
    }

    [Fact]
    public async Task ApproveRequestAsync_WhenDeductLeaveBalance_AndStandardWorkdayHoursIsSevenPointFive_CalculatesConvertedDays()
    {
        var (emp, policy) = CreateStandardFixture();
        policy.ExcessAction = ShortLeaveCompensationTypes.DeductLeaveBalance;
        policy.StandardWorkdayHours = 7.5m; // 450 minutes per workday

        var request = new ShortLeaveRequest
        {
            Id = 60L,
            CompanyId = 1L,
            EmployeeCode = emp.EmployeeCode,
            PermissionType = ShortLeaveTypes.MiddayExit,
            PermissionDate = new DateTime(2026, 11, 10),
            StartTime = new TimeSpan(11, 0, 0),
            EndTime = new TimeSpan(12, 30, 0),
            DurationMinutes = 90, // 90 / 450 = 0.2 days
            Reason = "Personal banking",
            Status = ShortLeaveStatus.PendingApproval,
            Employee = emp
        };

        _repoMock.Setup(r => r.GetByIdAsync(60L, It.IsAny<CancellationToken>()))
            .ReturnsAsync(request);
        _repoMock.Setup(r => r.GetActivePolicyAsync(1L, It.IsAny<CancellationToken>()))
            .ReturnsAsync(policy);
        _repoMock.Setup(r => r.GetMonthlyApprovedMinutesAsync(emp.EmployeeCode, 2026, 11, It.IsAny<CancellationToken>()))
            .ReturnsAsync(120); // Quota already fully used

        var result = await _service.ApproveRequestAsync(60L, "SUPERVISOR");

        Assert.Equal(ShortLeaveStatus.Approved, result.Status);
        Assert.Equal(ShortLeaveCompensationTypes.DeductLeaveBalance, result.CompensationType);
        Assert.Equal(0.2m, result.ConvertedLeaveDays);
    }
}

