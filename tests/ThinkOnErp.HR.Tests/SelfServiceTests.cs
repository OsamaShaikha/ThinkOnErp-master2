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
using ThinkOnErp.Domain.Exceptions;
using ThinkOnErp.Domain.Interfaces.Hr;
using Xunit;

namespace ThinkOnErp.HR.Tests;

public sealed class SelfServiceTests
{
    private readonly Mock<IEmployeeService> _employeeServiceMock = new();
    private readonly Mock<IPayrollService> _payrollServiceMock = new();
    private readonly Mock<ILeaveService> _leaveServiceMock = new();
    private readonly Mock<IAttendanceService> _attendanceServiceMock = new();
    private readonly Mock<IAssetAssignmentService> _assetServiceMock = new();
    private readonly Mock<IExpenseClaimService> _expenseServiceMock = new();
    private readonly Mock<IEmployeeRepository> _employeeRepoMock = new();
    private readonly Mock<ILeaveRepository> _leaveRepoMock = new();
    private readonly Mock<IAttendanceRepository> _attendanceRepoMock = new();
    private readonly SelfServiceService _selfService;

    public SelfServiceTests()
    {
        _selfService = new SelfServiceService(
            _employeeServiceMock.Object,
            _payrollServiceMock.Object,
            _leaveServiceMock.Object,
            _attendanceServiceMock.Object,
            _assetServiceMock.Object,
            _expenseServiceMock.Object,
            _employeeRepoMock.Object,
            _leaveRepoMock.Object,
            _attendanceRepoMock.Object,
            NullLogger<SelfServiceService>.Instance);
    }

    [Fact]
    public async Task ApproveTeamLeave_ByDirectManager_ApprovesSuccessfully()
    {
        // Arrange
        var directReport = new EmployeeDto
        {
            EmployeeCode = "EMP-REPORT-01",
            NameEn = "Direct Report Dev",
            ManagerEmployeeCode = "MGR-001",
            IsActive = true
        };

        var leaveReq = new LeaveRequest
        {
            Id = 15,
            EmployeeCode = "EMP-REPORT-01",
            LeaveTypeCode = "ANNUAL",
            DaysRequested = 3,
            Status = "PENDING"
        };

        _employeeServiceMock.Setup(e => e.GetAllAsync(null, null, null, true))
            .ReturnsAsync(new List<EmployeeDto> { directReport });

        _leaveRepoMock.Setup(l => l.GetRequestByIdAsync(15, It.IsAny<CancellationToken>()))
            .ReturnsAsync(leaveReq);

        _leaveServiceMock.Setup(l => l.ApproveRequestAsync(15, "MGR-001"))
            .ReturnsAsync(new LeaveRequestDto
            {
                Id = 15,
                EmployeeCode = "EMP-REPORT-01",
                Status = "APPROVED",
                ApprovedBy = "MGR-001"
            });

        // Act
        var result = await _selfService.ApproveTeamLeaveAsync("MGR-001", 15);

        // Assert
        result.Status.Should().Be("APPROVED");
        result.ApprovedBy.Should().Be("MGR-001");
    }

    [Fact]
    public async Task ApproveTeamLeave_ByUnauthorizedManager_ThrowsValidationException()
    {
        // Arrange: Manager MGR-OTHER tries to approve leave for EMP-REPORT-01 (who reports to MGR-001)
        var directReport = new EmployeeDto
        {
            EmployeeCode = "EMP-REPORT-01",
            NameEn = "Direct Report Dev",
            ManagerEmployeeCode = "MGR-001",
            IsActive = true
        };

        var leaveReq = new LeaveRequest
        {
            Id = 15,
            EmployeeCode = "EMP-REPORT-01",
            LeaveTypeCode = "ANNUAL",
            DaysRequested = 3,
            Status = "PENDING"
        };

        _employeeServiceMock.Setup(e => e.GetAllAsync(null, null, null, true))
            .ReturnsAsync(new List<EmployeeDto> { directReport }); // Not reporting to MGR-OTHER

        _leaveRepoMock.Setup(l => l.GetRequestByIdAsync(15, It.IsAny<CancellationToken>()))
            .ReturnsAsync(leaveReq);

        // Act
        var act = async () => await _selfService.ApproveTeamLeaveAsync("MGR-OTHER", 15);

        // Assert: Segregation of Duties / Authorization check
        await act.Should().ThrowAsync<HrValidationException>();
    }
}
