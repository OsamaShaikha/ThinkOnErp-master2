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
using ThinkOnErp.Domain.Interfaces.Accounting;
using ThinkOnErp.Domain.Interfaces.Hr;
using Xunit;

namespace ThinkOnErp.HR.Tests;

public sealed class OrganizationStructureTests
{
    private readonly Mock<IDepartmentRepository> _deptRepoMock = new();
    private readonly Mock<IGlCostCenterRepository> _costCenterRepoMock = new();
    private readonly Mock<IPositionRepository> _positionRepoMock = new();
    private readonly Mock<IJobGradeRepository> _gradeRepoMock = new();
    private readonly DepartmentService _departmentService;
    private readonly PositionService _positionService;

    public OrganizationStructureTests()
    {
        _departmentService = new DepartmentService(_deptRepoMock.Object, _costCenterRepoMock.Object, NullLogger<DepartmentService>.Instance);
        _positionService = new PositionService(_positionRepoMock.Object, _deptRepoMock.Object, _gradeRepoMock.Object, NullLogger<PositionService>.Instance);
    }

    [Fact]
    public async Task GetDepartmentTree_WithNestedDepartments_BuildsCorrectHierarchy()
    {
        // Arrange
        var rootDept = new Department { DepartmentCode = "CORP", NameEn = "Corporate HQ", ParentDepartmentCode = null, IsActive = true };
        var childDept1 = new Department { DepartmentCode = "TECH", NameEn = "Technology", ParentDepartmentCode = "CORP", IsActive = true };
        var childDept2 = new Department { DepartmentCode = "SWE", NameEn = "Software Engineering", ParentDepartmentCode = "TECH", IsActive = true };

        _deptRepoMock.Setup(r => r.GetAllAsync(null, true, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<Department> { rootDept, childDept1, childDept2 });

        // Act
        var tree = await _departmentService.GetTreeAsync();

        // Assert
        tree.Should().HaveCount(1);
        tree[0].DepartmentCode.Should().Be("CORP");
        tree[0].Children.Should().HaveCount(1);
        tree[0].Children[0].DepartmentCode.Should().Be("TECH");
        tree[0].Children[0].Children.Should().HaveCount(1);
        tree[0].Children[0].Children[0].DepartmentCode.Should().Be("SWE");
    }

    [Fact]
    public async Task GetOrgChart_WithReportingLines_BuildsHierarchicalPositionTree()
    {
        // Arrange
        var ceo = new Position { PositionCode = "CEO", TitleEn = "Chief Executive Officer", ReportsToPositionCode = null, IsActive = true };
        var cto = new Position { PositionCode = "CTO", TitleEn = "Chief Technology Officer", ReportsToPositionCode = "CEO", IsActive = true };
        var dev = new Position { PositionCode = "DEV", TitleEn = "Senior Developer", ReportsToPositionCode = "CTO", IsActive = true };

        _positionRepoMock.Setup(r => r.GetAllAsync(null, true, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<Position> { ceo, cto, dev });

        // Act
        var orgChart = await _positionService.GetOrgChartAsync();

        // Assert
        orgChart.Should().HaveCount(1);
        orgChart[0].PositionCode.Should().Be("CEO");
        orgChart[0].DirectReports.Should().HaveCount(1);
        orgChart[0].DirectReports[0].PositionCode.Should().Be("CTO");
        orgChart[0].DirectReports[0].DirectReports.Should().HaveCount(1);
        orgChart[0].DirectReports[0].DirectReports[0].PositionCode.Should().Be("DEV");
    }
}
