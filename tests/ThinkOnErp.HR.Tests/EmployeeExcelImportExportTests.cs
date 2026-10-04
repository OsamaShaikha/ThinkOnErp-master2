using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using ThinkOnErp.Application.DTOs.Hr;
using ThinkOnErp.Application.Services.Hr;
using ThinkOnErp.Domain.Entities.Hr;
using ThinkOnErp.Domain.Interfaces.Hr;
using ThinkOnErp.Infrastructure.Services.Hr;
using Xunit;

namespace ThinkOnErp.HR.Tests;

public class EmployeeExcelImportExportTests
{
    private readonly XlsxEmployeeWorkbookWriter _writer;
    private readonly XlsxEmployeeWorkbookReader _reader;
    private readonly Mock<IEmployeeRepository> _repoMock;
    private readonly EmployeeExcelService _service;

    public EmployeeExcelImportExportTests()
    {
        _writer = new XlsxEmployeeWorkbookWriter();
        _reader = new XlsxEmployeeWorkbookReader();
        _repoMock = new Mock<IEmployeeRepository>();
        _service = new EmployeeExcelService(
            _repoMock.Object,
            _reader,
            _writer,
            NullLogger<EmployeeExcelService>.Instance);
    }

    [Fact]
    public void GenerateTemplate_ReturnsValidXlsxBytes()
    {
        // Act
        var bytes = _writer.WriteTemplateWorkbook();

        // Assert
        Assert.NotNull(bytes);
        Assert.True(bytes.Length > 0);
        // First 2 bytes of any zip/xlsx file are 'PK' (0x50, 0x4B)
        Assert.Equal(0x50, bytes[0]);
        Assert.Equal(0x4B, bytes[1]);
    }

    [Fact]
    public async Task ReadTemplateWorkbook_ParsesSampleRowSuccessfully()
    {
        // Arrange
        var templateBytes = _writer.WriteTemplateWorkbook();
        using var stream = new MemoryStream(templateBytes);

        // Act
        var (rows, result) = await _reader.ReadAsync(stream);

        // Assert
        Assert.NotNull(rows);
        Assert.Single(rows); // The sample guide row
        var sample = rows[0];
        Assert.Equal("EMP001", sample.EmployeeCode);
        Assert.Equal("محمد أحمد خليل", sample.NameLocal);
        Assert.Equal("9901020304", sample.NationalId);
        Assert.Equal("M", sample.Gender);
        Assert.Equal("SINGLE", sample.MaritalStatus);
        Assert.Equal("FULL_TIME", sample.EmploymentType);
        Assert.True(sample.IsActive);
        Assert.Equal(750.00m, sample.BasicSalary);
    }

    [Fact]
    public void WriteExportWorkbook_ContainsSuppliedEmployees()
    {
        // Arrange
        var employees = new List<EmployeeExportRowDto>
        {
            new()
            {
                EmployeeCode = "EMP100",
                NameLocal = "خالد سليم",
                NameEn = "Khaled Saleem",
                NationalId = "1234567890",
                Nationality = "Jordanian",
                DateOfBirth = new DateTime(1988, 3, 20),
                Gender = "M",
                MaritalStatus = "MARRIED",
                Email = "khaled@example.com",
                Phone = "0790000000",
                HireDate = new DateTime(2022, 1, 1),
                EmploymentType = "FULL_TIME",
                EmploymentStatus = "ACTIVE",
                DepartmentCode = "DEV",
                PositionCode = "ENG",
                BasicSalary = 1500m,
                IsActive = true
            },
            new()
            {
                EmployeeCode = "EMP101",
                NameLocal = "فاطمة عمر",
                NameEn = "Fatima Omar",
                NationalId = "0987654321",
                Nationality = "Jordanian",
                DateOfBirth = new DateTime(1995, 7, 10),
                Gender = "F",
                MaritalStatus = "SINGLE",
                Email = "fatima@example.com",
                Phone = "0780000000",
                HireDate = new DateTime(2023, 5, 1),
                EmploymentType = "FULL_TIME",
                EmploymentStatus = "ACTIVE",
                DepartmentCode = "QA",
                PositionCode = "TESTER",
                BasicSalary = 900m,
                IsActive = true
            }
        };

        // Act
        var bytes = _writer.WriteExportWorkbook(employees);

        // Assert
        Assert.NotNull(bytes);
        Assert.True(bytes.Length > 0);
        Assert.Equal(0x50, bytes[0]);
        Assert.Equal(0x4B, bytes[1]);
    }

    [Fact]
    public async Task ReadExportedWorkbook_ReadsAllEmployeesCorrectly()
    {
        // Arrange
        var employees = new List<EmployeeExportRowDto>
        {
            new()
            {
                EmployeeCode = "TEST01",
                NameLocal = "سامي يوسف",
                NameEn = "Sami Yousef",
                NationalId = "8899776655",
                DateOfBirth = new DateTime(1985, 10, 1),
                Gender = "M",
                MaritalStatus = "MARRIED",
                HireDate = new DateTime(2020, 2, 15),
                EmploymentType = "FULL_TIME",
                EmploymentStatus = "ACTIVE",
                DepartmentCode = "SALES",
                BasicSalary = 1200m,
                IsActive = true
            }
        };

        var bytes = _writer.WriteExportWorkbook(employees);
        using var stream = new MemoryStream(bytes);

        // Act
        var (rows, result) = await _reader.ReadAsync(stream);

        // Assert
        Assert.Empty(result.Errors);
        Assert.Single(rows);
        var readEmp = rows[0];
        Assert.Equal("TEST01", readEmp.EmployeeCode);
        Assert.Equal("سامي يوسف", readEmp.NameLocal);
        Assert.Equal("8899776655", readEmp.NationalId);
        Assert.Equal("SALES", readEmp.DepartmentCode);
        Assert.Equal(1200m, readEmp.BasicSalary);
    }

    [Fact]
    public async Task ValidateWorkbookAsync_DetectsDuplicateCodesInSameFile()
    {
        // Arrange: Export two rows with identical EmployeeCode
        var employees = new List<EmployeeExportRowDto>
        {
            new()
            {
                EmployeeCode = "DUP01",
                NameLocal = "موظف أ",
                NameEn = "Emp A",
                NationalId = "1111111111",
                DateOfBirth = new DateTime(1990, 1, 1),
                HireDate = new DateTime(2021, 1, 1),
                Gender = "M"
            },
            new()
            {
                EmployeeCode = "DUP01",
                NameLocal = "موظف ب",
                NameEn = "Emp B",
                NationalId = "2222222222",
                DateOfBirth = new DateTime(1992, 1, 1),
                HireDate = new DateTime(2021, 1, 1),
                Gender = "F"
            }
        };

        var bytes = _writer.WriteExportWorkbook(employees);
        using var stream = new MemoryStream(bytes);

        // Act
        var result = await _service.ValidateWorkbookAsync(stream);

        // Assert
        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.Code == "DUPLICATE_IN_FILE" && e.Column == "EmployeeCode");
    }

    [Fact]
    public async Task ImportEmployeesAsync_InsertsNewEmployee_WhenNotExist()
    {
        // Arrange
        var employees = new List<EmployeeExportRowDto>
        {
            new()
            {
                EmployeeCode = "NEW001",
                NameLocal = "طارق حامد",
                NameEn = "Tareq Hamed",
                NationalId = "3333444455",
                DateOfBirth = new DateTime(1993, 4, 12),
                HireDate = new DateTime(2024, 6, 1),
                Gender = "M",
                MaritalStatus = "SINGLE",
                EmploymentType = "FULL_TIME",
                EmploymentStatus = "ACTIVE",
                BasicSalary = 800m
            }
        };

        var bytes = _writer.WriteExportWorkbook(employees);
        using var stream = new MemoryStream(bytes);

        _repoMock.Setup(r => r.GetEmployeeByCodeAsync("NEW001", It.IsAny<CancellationToken>()))
            .ReturnsAsync((Employee?)null);
        _repoMock.Setup(r => r.ExistsByNationalIdAsync("3333444455", null, It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);

        // Act
        var result = await _service.ImportEmployeesAsync(stream, updateExisting: false, user: "ADMIN");

        // Assert
        Assert.True(result.IsValid);
        Assert.Equal(1, result.InsertedCount);
        Assert.Equal(0, result.UpdatedCount);
        _repoMock.Verify(r => r.AddEmployeeAsync(It.Is<Employee>(e => e.EmployeeCode == "NEW001" && e.NameLocal == "طارق حامد"), It.IsAny<CancellationToken>()), Times.Once);
        _repoMock.Verify(r => r.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task ImportEmployeesAsync_RejectsDuplicate_WhenUpdateExistingIsFalse()
    {
        // Arrange
        var employees = new List<EmployeeExportRowDto>
        {
            new()
            {
                EmployeeCode = "EXISTING01",
                NameLocal = "علي حسن",
                NationalId = "7777888899",
                DateOfBirth = new DateTime(1989, 1, 1),
                HireDate = new DateTime(2020, 1, 1),
                Gender = "M"
            }
        };

        var bytes = _writer.WriteExportWorkbook(employees);
        using var stream = new MemoryStream(bytes);

        _repoMock.Setup(r => r.GetEmployeeByCodeAsync("EXISTING01", It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Employee
            {
                EmployeeCode = "EXISTING01",
                NameLocal = "علي القديم",
                NationalId = "7777888899"
            });

        // Act
        var result = await _service.ImportEmployeesAsync(stream, updateExisting: false, user: "ADMIN");

        // Assert
        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.Code == "EMPLOYEE_EXISTS");
        _repoMock.Verify(r => r.AddEmployeeAsync(It.IsAny<Employee>(), It.IsAny<CancellationToken>()), Times.Never);
        _repoMock.Verify(r => r.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task ImportEmployeesAsync_UpdatesEmployee_WhenUpdateExistingIsTrue()
    {
        // Arrange
        var employees = new List<EmployeeExportRowDto>
        {
            new()
            {
                EmployeeCode = "EXISTING01",
                NameLocal = "علي حسن المحدث",
                NameEn = "Ali Hassan Updated",
                NationalId = "7777888899",
                DateOfBirth = new DateTime(1989, 1, 1),
                HireDate = new DateTime(2020, 1, 1),
                Gender = "M",
                BasicSalary = 1350m
            }
        };

        var bytes = _writer.WriteExportWorkbook(employees);
        using var stream = new MemoryStream(bytes);

        var existingEmp = new Employee
        {
            EmployeeCode = "EXISTING01",
            NameLocal = "علي القديم",
            NationalId = "7777888899",
            SalaryStructures = new List<SalaryStructure>
            {
                new()
                {
                    EmployeeCode = "EXISTING01",
                    BasicSalary = 1000m,
                    IsActive = true
                }
            }
        };

        _repoMock.Setup(r => r.GetEmployeeByCodeAsync("EXISTING01", It.IsAny<CancellationToken>()))
            .ReturnsAsync(existingEmp);
        _repoMock.Setup(r => r.ExistsByNationalIdAsync("7777888899", "EXISTING01", It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);

        // Act
        var result = await _service.ImportEmployeesAsync(stream, updateExisting: true, user: "ADMIN");

        // Assert
        Assert.True(result.IsValid);
        Assert.Equal(0, result.InsertedCount);
        Assert.Equal(1, result.UpdatedCount);
        Assert.Equal("علي حسن المحدث", existingEmp.NameLocal);
        Assert.Equal(1350m, existingEmp.SalaryStructures[0].BasicSalary);
        _repoMock.Verify(r => r.UpdateEmployeeAsync(existingEmp, It.IsAny<CancellationToken>()), Times.Once);
        _repoMock.Verify(r => r.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }
}
