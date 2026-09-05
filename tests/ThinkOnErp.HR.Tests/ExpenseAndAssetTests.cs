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

public sealed class ExpenseAndAssetTests
{
    private readonly Mock<IExpenseClaimRepository> _expenseRepoMock = new();
    private readonly Mock<IAssetAssignmentRepository> _assetRepoMock = new();
    private readonly Mock<IEmployeeRepository> _employeeRepoMock = new();
    private readonly ExpenseClaimService _expenseService;
    private readonly AssetAssignmentService _assetService;

    public ExpenseAndAssetTests()
    {
        _expenseService = new ExpenseClaimService(_expenseRepoMock.Object, _employeeRepoMock.Object, NullLogger<ExpenseClaimService>.Instance);
        _assetService = new AssetAssignmentService(_assetRepoMock.Object, _employeeRepoMock.Object, NullLogger<AssetAssignmentService>.Instance);
    }

    [Fact]
    public async Task SubmitExpenseClaim_MultipleLines_ComputesTotalAmountAccurately()
    {
        // Arrange
        _employeeRepoMock.Setup(e => e.GetByCodeAsync("EMP-001", false, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Employee { EmployeeCode = "EMP-001", NameEn = "Test Employee" });

        var dto = new SubmitExpenseClaimDto
        {
            EmployeeCode = "EMP-001",
            Description = "Client Onsite Meeting Expenses",
            ReimbursementMethod = "NEXT_PAYROLL_RUN",
            Lines = new List<CreateExpenseClaimLineDto>
            {
                new() { Category = "TRAVEL", Description = "Flight to Aqaba", Amount = 120.00m, ExpenseDate = DateTime.UtcNow },
                new() { Category = "HOTEL", Description = "Hotel Accommodation", Amount = 80.00m, ExpenseDate = DateTime.UtcNow },
                new() { Category = "MEALS", Description = "Client Dinner", Amount = 45.50m, ExpenseDate = DateTime.UtcNow }
            }
        };

        _expenseRepoMock.Setup(r => r.GetClaimByIdAsync(It.IsAny<long>(), true, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ExpenseClaim
            {
                Id = 1,
                EmployeeCode = "EMP-001",
                TotalAmount = 245.50m,
                Status = "SUBMITTED",
                Lines = new List<ExpenseClaimLine>
                {
                    new() { Category = "TRAVEL", Description = "Flight", Amount = 120.00m },
                    new() { Category = "HOTEL", Description = "Hotel", Amount = 80.00m },
                    new() { Category = "MEALS", Description = "Dinner", Amount = 45.50m }
                }
            });

        // Act
        var result = await _expenseService.SubmitClaimAsync(dto, "EMP-001");

        // Assert: 120 + 80 + 45.50 = 245.50 JOD
        result.TotalAmount.Should().Be(245.50m);
        result.Status.Should().Be("SUBMITTED");
        result.Lines.Should().HaveCount(3);
        _expenseRepoMock.Verify(r => r.AddClaimAsync(It.Is<ExpenseClaim>(c => c.TotalAmount == 245.50m), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task AssignAsset_AndReturnAsset_UpdatesConditionAndCompletesReturn()
    {
        // Arrange
        _employeeRepoMock.Setup(e => e.GetByCodeAsync("EMP-001", false, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Employee { EmployeeCode = "EMP-001", NameEn = "Test Employee" });

        var assignDto = new CreateAssetAssignmentDto
        {
            EmployeeCode = "EMP-001",
            AssetTag = "LAPTOP-2026-009",
            AssetDescription = "ThinkPad P16 Gen 2",
            Category = "LAPTOP",
            IssuedCondition = "NEW",
            IssuedDate = DateTime.UtcNow
        };

        var assetRecord = new AssetAssignment
        {
            Id = 50,
            EmployeeCode = "EMP-001",
            AssetTag = "LAPTOP-2026-009",
            AssetDescription = "ThinkPad P16 Gen 2",
            IssuedCondition = "NEW",
            Status = "ASSIGNED"
        };

        _assetRepoMock.Setup(r => r.GetAssignmentByIdAsync(It.IsAny<long>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(assetRecord);

        // Act 1: Assign
        var assigned = await _assetService.AssignAssetAsync(assignDto, "HR_ADMIN");
        assigned.AssetTag.Should().Be("LAPTOP-2026-009");
        _assetRepoMock.Verify(r => r.AddAssignmentAsync(It.Is<AssetAssignment>(a => a.AssetTag == "LAPTOP-2026-009"), It.IsAny<CancellationToken>()), Times.Once);

        // Act 2: Return
        var returnDto = new ReturnAssetDto
        {
            ReturnedDate = DateTime.UtcNow,
            ReturnedCondition = "GOOD",
            Status = "RETURNED",
            Notes = "Returned upon project completion"
        };
        var returned = await _assetService.ReturnAssetAsync(50, returnDto, "HR_ADMIN");

        // Assert: Return
        returned.Status.Should().Be("RETURNED");
        assetRecord.ReturnedCondition.Should().Be("GOOD");
        _assetRepoMock.Verify(r => r.UpdateAssignment(It.Is<AssetAssignment>(a => a.Status == "RETURNED")), Times.Once);
    }
}
