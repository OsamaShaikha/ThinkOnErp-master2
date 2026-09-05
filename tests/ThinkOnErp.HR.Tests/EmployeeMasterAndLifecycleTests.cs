using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using ThinkOnErp.Application.DTOs.Hr;
using ThinkOnErp.Application.Services.Hr;
using ThinkOnErp.Domain.Entities.Hr;
using ThinkOnErp.Domain.Interfaces.Hr;
using Xunit;

namespace ThinkOnErp.HR.Tests;

public sealed class EmployeeMasterAndLifecycleTests
{
    private readonly Mock<IEmployeeRepository> _employeeRepoMock = new();
    private readonly Mock<IDepartmentRepository> _departmentRepoMock = new();
    private readonly Mock<IPositionRepository> _positionRepoMock = new();
    private readonly Mock<IEmployeeDependentRepository> _dependentRepoMock = new();
    private readonly Mock<IEmploymentEventRepository> _eventRepoMock = new();
    private readonly EmployeeService _employeeService;

    public EmployeeMasterAndLifecycleTests()
    {
        _employeeService = new EmployeeService(
            _employeeRepoMock.Object,
            _departmentRepoMock.Object,
            _positionRepoMock.Object,
            _dependentRepoMock.Object,
            _eventRepoMock.Object,
            NullLogger<EmployeeService>.Instance);
    }

    [Fact]
    public async Task CreateEmployee_ValidDto_CreatesEmployeeAndRecordsHireEvent()
    {
        // Arrange
        var dto = new CreateEmployeeDto
        {
            EmployeeCode = "EMP-100",
            NameAr = "أحمد خالد",
            NameEn = "Ahmad Khaled",
            NationalId = "9901020304",
            PositionCode = "SR_SWE",
            DepartmentCode = "ENG",
            HireDate = new DateTime(2026, 1, 1),
            SscNumber = "SSC-123456"
        };

        _employeeRepoMock.Setup(r => r.CodeExistsAsync("EMP-100", It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);
        _employeeRepoMock.Setup(r => r.NationalIdExistsAsync("9901020304", null, It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);
        _positionRepoMock.Setup(r => r.GetByCodeAsync("SR_SWE", It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Position { PositionCode = "SR_SWE", DepartmentCode = "ENG" });
        _departmentRepoMock.Setup(r => r.GetByCodeAsync("ENG", It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Department { DepartmentCode = "ENG" });

        _employeeRepoMock.Setup(r => r.GetByCodeAsync("EMP-100", true, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Employee
            {
                EmployeeCode = "EMP-100",
                NameAr = dto.NameAr,
                NameEn = dto.NameEn,
                NationalId = dto.NationalId,
                PositionCode = dto.PositionCode,
                DepartmentCode = dto.DepartmentCode,
                HireDate = dto.HireDate,
                EmploymentStatus = "PROBATION"
            });

        // Act
        var result = await _employeeService.CreateAsync(dto, "HR_ADMIN");

        // Assert
        result.EmployeeCode.Should().Be("EMP-100");
        result.EmploymentStatus.Should().Be("PROBATION");

        // Verify Employee was added
        _employeeRepoMock.Verify(r => r.AddAsync(It.Is<Employee>(e => e.EmployeeCode == "EMP-100"), It.IsAny<CancellationToken>()), Times.Once);

        // Verify Immutable HIRE event was logged
        _eventRepoMock.Verify(r => r.AddAsync(It.Is<EmploymentEvent>(ev => ev.EmployeeCode == "EMP-100" && ev.EventType == "HIRE"), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task AddDependent_TaxExemptionClaimed_AutomaticallyUpdatesTaxExemptionCount()
    {
        // Arrange
        var emp = new Employee
        {
            EmployeeCode = "EMP-100",
            NameEn = "Ahmad Khaled",
            TaxExemptionCount = 0
        };

        _employeeRepoMock.Setup(r => r.GetByCodeAsync("EMP-100", false, It.IsAny<CancellationToken>()))
            .ReturnsAsync(emp);

        _dependentRepoMock.Setup(r => r.GetByEmployeeCodeAsync("EMP-100", It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<EmployeeDependent>());

        var dto = new CreateEmployeeDependentDto
        {
            NameAr = "الطفل الأول",
            NameEn = "Child 1",
            Relationship = "CHILD",
            DateOfBirth = DateTime.UtcNow.AddYears(-5),
            IsTaxExemptionClaimed = true
        };

        // Act
        var result = await _employeeService.AddDependentAsync("EMP-100", dto, "HR_ADMIN");

        // Assert
        result.NameEn.Should().Be("Child 1");
        // Employee tax exemption count should be updated to 1
        emp.TaxExemptionCount.Should().Be(1);
        _employeeRepoMock.Verify(r => r.Update(It.Is<Employee>(e => e.TaxExemptionCount == 1)), Times.Once);
    }

    [Fact]
    public async Task ChangeStatus_ToTerminated_SetsTerminationFieldsAndRecordsTerminationEvent()
    {
        // Arrange
        var emp = new Employee
        {
            EmployeeCode = "EMP-100",
            NameEn = "Ahmad Khaled",
            EmploymentStatus = "ACTIVE"
        };

        _employeeRepoMock.Setup(r => r.GetByCodeAsync("EMP-100", true, It.IsAny<CancellationToken>()))
            .ReturnsAsync(emp);

        var dto = new ChangeEmployeeStatusDto
        {
            NewStatus = "TERMINATED",
            EffectiveDate = new DateTime(2026, 6, 30),
            Reason = "Resignation for career advancement"
        };

        // Act
        var result = await _employeeService.ChangeStatusAsync("EMP-100", dto, "HR_ADMIN");

        // Assert
        emp.EmploymentStatus.Should().Be("TERMINATED");
        emp.TerminationDate.Should().Be(new DateTime(2026, 6, 30));
        emp.TerminationReason.Should().Be("Resignation for career advancement");

        // Verify Immutable TERMINATION event was logged
        _eventRepoMock.Verify(r => r.AddAsync(It.Is<EmploymentEvent>(ev => ev.EmployeeCode == "EMP-100" && ev.EventType == "TERMINATION"), It.IsAny<CancellationToken>()), Times.Once);
    }
}
