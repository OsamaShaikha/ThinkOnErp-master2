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

public sealed class RecruitmentAndOnboardingTests
{
    private readonly Mock<IRecruitmentRepository> _recruitmentRepoMock = new();
    private readonly Mock<IEmployeeRepository> _employeeRepoMock = new();
    private readonly Mock<ICompensationRepository> _compensationRepoMock = new();
    private readonly Mock<IEmploymentEventRepository> _eventRepoMock = new();
    private readonly RecruitmentService _recruitmentService;

    public RecruitmentAndOnboardingTests()
    {
        _recruitmentService = new RecruitmentService(
            _recruitmentRepoMock.Object,
            _employeeRepoMock.Object,
            _compensationRepoMock.Object,
            _eventRepoMock.Object,
            NullLogger<RecruitmentService>.Instance);
    }

    [Fact]
    public async Task HireCandidate_ValidApplication_CreatesEmployeeAndSpawnsOnboardingTasks()
    {
        // Arrange
        var candidate = new Candidate
        {
            CandidateCode = "CAND-001",
            NameAr = "سمير عبد الله",
            NameEn = "Sameer Abdullah",
            Email = "sameer@example.com",
            Phone = "+962790000001",
            NationalId = "9951010101"
        };

        var req = new JobRequisition
        {
            RequisitionCode = "REQ-ENG-01",
            PositionCode = "SR_SWE",
            DepartmentCode = "ENG",
            Status = "APPROVED"
        };

        var app = new CandidateApplication
        {
            Id = 10,
            CandidateCode = "CAND-001",
            RequisitionCode = "REQ-ENG-01",
            Stage = "OFFER",
            Candidate = candidate,
            Requisition = req
        };

        _recruitmentRepoMock.Setup(r => r.GetApplicationByIdAsync(10, It.IsAny<CancellationToken>()))
            .ReturnsAsync(app);

        _recruitmentRepoMock.Setup(r => r.GetRequisitionByCodeAsync("REQ-ENG-01", true, It.IsAny<CancellationToken>()))
            .ReturnsAsync(req);

        _employeeRepoMock.Setup(e => e.CodeExistsAsync("EMP-SAMEER", It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);

        _employeeRepoMock.Setup(e => e.GetByCodeAsync("EMP-SAMEER", It.IsAny<bool>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Employee
            {
                EmployeeCode = "EMP-SAMEER",
                NameEn = "Sameer Abdullah",
                EmploymentStatus = "PROBATION"
            });

        var hireDto = new HireCandidateDto
        {
            EmployeeCode = "EMP-SAMEER",
            HireDate = new DateTime(2026, 9, 1),
            BasicSalary = 1500.00m,
            SscNumber = "SSC-998877"
        };

        // Act
        var result = await _recruitmentService.HireCandidateAsync(10, hireDto, "RECRUITER_USER");

        // Assert
        result.EmployeeCode.Should().Be("EMP-SAMEER");
        app.Stage.Should().Be("HIRED");

        // Verify Employee added with PROBATION status
        _employeeRepoMock.Verify(e => e.AddAsync(It.Is<Employee>(emp => emp.EmployeeCode == "EMP-SAMEER" && emp.EmploymentStatus == "PROBATION"), It.IsAny<CancellationToken>()), Times.Once);

        // Verify Salary Structure created
        _compensationRepoMock.Verify(c => c.AddStructureAsync(It.Is<EmployeeSalaryStructure>(s => s.EmployeeCode == "EMP-SAMEER" && s.BasicSalary == 1500.00m), It.IsAny<CancellationToken>()), Times.Once);

        // Verify Onboarding Tasks added
        _recruitmentRepoMock.Verify(r => r.AddOnboardingTaskAsync(It.IsAny<OnboardingTask>(), It.IsAny<CancellationToken>()), Times.AtLeastOnce);
    }
}
