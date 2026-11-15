using System;
using System.Collections.Generic;
using System.Security.Claims;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using Moq;
using ThinkOnErp.API.Controllers.Hr;
using ThinkOnErp.Application.Common;
using ThinkOnErp.Application.DTOs.Hr;
using ThinkOnErp.Application.Services.Hr;
using ThinkOnErp.Domain.Entities.Hr;
using ThinkOnErp.Domain.Interfaces.Hr;
using Xunit;

namespace ThinkOnErp.HR.Api.Tests;

public class PayrollPolicyControllerTests
{
    private readonly Mock<IPolicyRepository> _mockRepo;
    private readonly Mock<ILogger<PayrollPoliciesController>> _mockLogger;
    private readonly PayrollPoliciesController _controller;

    public PayrollPolicyControllerTests()
    {
        _mockRepo = new Mock<IPolicyRepository>();
        _mockLogger = new Mock<ILogger<PayrollPoliciesController>>();
        _controller = new PayrollPoliciesController(_mockRepo.Object, _mockLogger.Object)
        {
            ControllerContext = new ControllerContext
            {
                HttpContext = new DefaultHttpContext
                {
                    User = new ClaimsPrincipal(new ClaimsIdentity(new[]
                    {
                        new Claim(ClaimTypes.Name, "test-admin")
                    }, "TestAuth"))
                }
            }
        };
    }

    private static ProrationPolicyDto CreateProrationDto(
        long companyId = 1,
        string code = "PROR_01",
        DateTime? effectiveFrom = null,
        DateTime? effectiveTo = null,
        string method = "CALENDAR_DAYS",
        string allocation = "UNCONFIGURED",
        bool isDefault = false) =>
        new(companyId, code, "نسبي", "Proration", effectiveFrom ?? new DateTime(2026, 1, 1), effectiveTo, method, isDefault, allocation);

    private static SSCPolicyDto CreateSSCDto(
        long companyId = 1,
        string code = "SSC_01",
        DateTime? effectiveFrom = null,
        DateTime? effectiveTo = null,
        decimal employeeRate = 0.0975m,
        decimal employerRate = 0.1175m,
        decimal highRiskRate = 0m,
        decimal ceilingCap = 45000m,
        decimal minWage = 0m) =>
        new(companyId, code, "تأمينات", "SSC", effectiveFrom ?? new DateTime(2026, 1, 1), effectiveTo, employeeRate, employerRate, highRiskRate, ceilingCap, minWage);

    private static TaxPolicyDto CreateTaxDto(
        long companyId = 1,
        string code = "TAX_01",
        DateTime? effectiveFrom = null,
        DateTime? effectiveTo = null,
        decimal selfExemption = 9000m,
        decimal depExemption = 1000m,
        decimal nationalThreshold = 0m,
        decimal nationalRate = 0m,
        bool isSscDeductible = false,
        string frequency = "MONTHLY",
        List<TaxBracketDto>? brackets = null,
        int? maxDependents = null) =>
        new(companyId, code, "ضريبة", "Tax", effectiveFrom ?? new DateTime(2026, 1, 1), effectiveTo, selfExemption, depExemption, nationalThreshold, nationalRate, isSscDeductible, frequency, brackets ?? new List<TaxBracketDto>(), maxDependents);

    private static DeductionPolicyDto CreateDeductionDto(
        long companyId = 1,
        string code = "DED_01",
        DateTime? effectiveFrom = null,
        DateTime? effectiveTo = null,
        decimal maxDeductionPct = 50m,
        decimal minNetPay = 250m,
        bool autoCap = true,
        bool allowNegative = false,
        bool isDefault = false) =>
        new(companyId, code, "اقتطاع", "Deduction", effectiveFrom ?? new DateTime(2026, 1, 1), effectiveTo, maxDeductionPct, minNetPay, autoCap, allowNegative, isDefault);

    private static OvertimeRuleDto CreateOvertimeRuleDto(
        long companyId = 1,
        string code = "OT_REG",
        string dayType = "REGULAR",
        decimal multiplier = 1.25m,
        int minimumMinutes = 30,
        int maximumMinutes = 240,
        string formula = "MONTHLY_SALARY / 240") =>
        new(companyId, code, "إضافي", "Overtime", new DateTime(2026, 1, 1), null, dayType, multiplier,
            minimumMinutes, maximumMinutes, formula, true);

    [Fact]
    public async Task AddOvertimeRule_ValidCompanyConfig_IsNormalizedAndAudited()
    {
        var actionResult = await _controller.AddOvertimeRule(
            CreateOvertimeRuleDto(dayType: " weekend ", formula: " monthly_salary / 173.5 "), CancellationToken.None);

        var okResult = Assert.IsType<OkObjectResult>(actionResult.Result);
        var response = Assert.IsType<ApiResponse<OvertimeRule>>(okResult.Value);
        Assert.True(response.Success);
        Assert.Equal("WEEKEND", response.Data!.DayType);
        Assert.Equal("MONTHLY_SALARY / 173.5", response.Data.HourlyDivisorFormula);
        Assert.Equal("test-admin", response.Data.CreationUser);
        _mockRepo.Verify(r => r.AddOvertimeRuleAsync(It.Is<OvertimeRule>(rule =>
            rule.CompanyId == 1 && rule.RequiresApproval && rule.Multiplier == 1.25m), It.IsAny<CancellationToken>()), Times.Once);
        _mockRepo.Verify(r => r.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task AddProrationPolicy_UsesCompanyPayrollDateGuard()
    {
        var unitOfWork = new Mock<IHrUnitOfWork>();
        unitOfWork.Setup(u => u.ExecuteForCompanyPayrollDatesAsync(5, new DateTime(2026, 1, 1),
                new DateTime(2026, 12, 31), It.IsAny<Func<Task<bool>>>(), It.IsAny<CancellationToken>()))
            .Returns((long _, DateTime _, DateTime _, Func<Task<bool>> write, CancellationToken _) => write());
        var controller = new PayrollPoliciesController(_mockRepo.Object, _mockLogger.Object, unitOfWork.Object)
        {
            ControllerContext = _controller.ControllerContext
        };

        var result = await controller.AddProrationPolicy(CreateProrationDto(companyId: 5,
            effectiveFrom: new DateTime(2026, 1, 1), effectiveTo: new DateTime(2026, 12, 31)), CancellationToken.None);

        Assert.IsType<OkObjectResult>(result.Result);
        unitOfWork.Verify(u => u.ExecuteForCompanyPayrollDatesAsync(5, new DateTime(2026, 1, 1),
            new DateTime(2026, 12, 31), It.IsAny<Func<Task<bool>>>(), It.IsAny<CancellationToken>()), Times.Once);
        _mockRepo.Verify(r => r.AddProrationPolicyAsync(It.IsAny<ProrationPolicy>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Theory]
    [InlineData("OTHER")]
    [InlineData("")]
    public async Task AddOvertimeRule_InvalidDayType_IsRejected(string dayType)
    {
        await Assert.ThrowsAsync<ArgumentException>(() =>
            _controller.AddOvertimeRule(CreateOvertimeRuleDto(dayType: dayType), CancellationToken.None));
        _mockRepo.Verify(r => r.AddOvertimeRuleAsync(It.IsAny<OvertimeRule>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Theory]
    [InlineData("MONTHLY_SALARY / 0")]
    [InlineData("HOURLY_RATE")]
    public async Task AddOvertimeRule_UnsupportedHourlyDivisor_IsRejected(string formula)
    {
        await Assert.ThrowsAsync<ArgumentException>(() =>
            _controller.AddOvertimeRule(CreateOvertimeRuleDto(formula: formula), CancellationToken.None));
        _mockRepo.Verify(r => r.AddOvertimeRuleAsync(It.IsAny<OvertimeRule>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Theory]
    [InlineData(0.0, 30, 240)]
    [InlineData(1000.0, 30, 240)]
    [InlineData(1.25, 240, 120)]
    public async Task AddOvertimeRule_InvalidMultiplierOrMinuteRange_IsRejected(decimal multiplier, int minimumMinutes, int maximumMinutes)
    {
        await Assert.ThrowsAsync<ArgumentException>(() =>
            _controller.AddOvertimeRule(CreateOvertimeRuleDto(multiplier: multiplier, minimumMinutes: minimumMinutes, maximumMinutes: maximumMinutes), CancellationToken.None));
        _mockRepo.Verify(r => r.AddOvertimeRuleAsync(It.IsAny<OvertimeRule>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task AddOvertimeRule_OverlongName_IsRejectedBeforePersistence()
    {
        var dto = CreateOvertimeRuleDto() with { NameEn = new string('x', 201) };

        await Assert.ThrowsAsync<ArgumentException>(() => _controller.AddOvertimeRule(dto, CancellationToken.None));
        _mockRepo.Verify(r => r.AddOvertimeRuleAsync(It.IsAny<OvertimeRule>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Theory]
    [InlineData(0, "POL01")]
    [InlineData(-1, "POL01")]
    [InlineData(1, null)]
    [InlineData(1, "")]
    [InlineData(1, "   ")]
    public async Task AddProrationPolicy_InvalidCompanyOrCode_ThrowsArgumentException(long companyId, string? code)
    {
        var dto = CreateProrationDto(companyId: companyId, code: code!);
        await Assert.ThrowsAsync<ArgumentException>(() =>
            _controller.AddProrationPolicy(dto, CancellationToken.None));
    }

    [Fact]
    public async Task AddProrationPolicy_InvertedDateRange_ThrowsArgumentException()
    {
        var dto = CreateProrationDto(
            effectiveFrom: new DateTime(2026, 6, 1),
            effectiveTo: new DateTime(2026, 5, 31));

        var ex = await Assert.ThrowsAsync<ArgumentException>(() =>
            _controller.AddProrationPolicy(dto, CancellationToken.None));
        Assert.Contains("EffectiveTo cannot be earlier than EffectiveFrom", ex.Message);
    }

    [Theory]
    [InlineData("UNKNOWN_METHOD", "UNCONFIGURED")]
    [InlineData("CALENDAR_DAYS", "INVALID_ALLOCATION")]
    public async Task AddProrationPolicy_InvalidMethodOrAllocation_ThrowsArgumentException(string method, string allocation)
    {
        var dto = CreateProrationDto(method: method, allocation: allocation);
        await Assert.ThrowsAsync<ArgumentException>(() =>
            _controller.AddProrationPolicy(dto, CancellationToken.None));
    }

    [Fact]
    public async Task AddProrationPolicy_ValidInput_AddsPolicyAndReturnsSuccess()
    {
        var dto = CreateProrationDto(
            companyId: 5,
            code: "PROR_CAL",
            effectiveFrom: new DateTime(2026, 1, 1),
            effectiveTo: new DateTime(2026, 12, 31),
            method: "CALENDAR_DAYS",
            allocation: "CALENDAR_WEIGHTED",
            isDefault: true);

        var actionResult = await _controller.AddProrationPolicy(dto, CancellationToken.None);
        var okResult = Assert.IsType<OkObjectResult>(actionResult.Result);
        var response = Assert.IsType<ApiResponse<ProrationPolicy>>(okResult.Value);

        Assert.True(response.Success);
        Assert.NotNull(response.Data);
        Assert.Equal("PROR_CAL", response.Data.Code);
        Assert.Equal("test-admin", response.Data.CreationUser);
        _mockRepo.Verify(r => r.AddProrationPolicyAsync(It.Is<ProrationPolicy>(p => p.Code == "PROR_CAL"), It.IsAny<CancellationToken>()), Times.Once);
        _mockRepo.Verify(r => r.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Theory]
    [InlineData(0, "SSC01")]
    [InlineData(1, "")]
    public async Task AddSSCPolicy_InvalidCompanyOrCode_ThrowsArgumentException(long companyId, string code)
    {
        var dto = CreateSSCDto(companyId: companyId, code: code);
        await Assert.ThrowsAsync<ArgumentException>(() =>
            _controller.AddSSCPolicy(dto, CancellationToken.None));
    }

    [Fact]
    public async Task AddSSCPolicy_InvertedDateRange_ThrowsArgumentException()
    {
        var dto = CreateSSCDto(
            effectiveFrom: new DateTime(2026, 12, 1),
            effectiveTo: new DateTime(2026, 1, 1));

        var ex = await Assert.ThrowsAsync<ArgumentException>(() =>
            _controller.AddSSCPolicy(dto, CancellationToken.None));
        Assert.Contains("EffectiveTo cannot be earlier than EffectiveFrom", ex.Message);
    }

    [Theory]
    [InlineData(-0.01, 0.10, 0, 0)]
    [InlineData(0.05, -0.01, 0, 0)]
    [InlineData(0.05, 0.10, -500, 0)]
    [InlineData(0.05, 0.10, 0, -100)]
    public async Task AddSSCPolicy_NegativeRatesOrCaps_ThrowsArgumentException(
        decimal employeeRate, decimal employerRate, decimal ceilingCap, decimal minWage)
    {
        var dto = CreateSSCDto(
            employeeRate: employeeRate,
            employerRate: employerRate,
            ceilingCap: ceilingCap,
            minWage: minWage);

        await Assert.ThrowsAsync<ArgumentException>(() =>
            _controller.AddSSCPolicy(dto, CancellationToken.None));
    }

    [Fact]
    public async Task AddSSCPolicy_ValidInput_AddsPolicyAndReturnsSuccess()
    {
        var dto = CreateSSCDto(
            companyId: 3,
            code: "SSC_GOSI",
            employeeRate: 0.0975m,
            employerRate: 0.1175m,
            ceilingCap: 45000m,
            minWage: 4000m);

        var actionResult = await _controller.AddSSCPolicy(dto, CancellationToken.None);
        var okResult = Assert.IsType<OkObjectResult>(actionResult.Result);
        var response = Assert.IsType<ApiResponse<SSCPolicy>>(okResult.Value);

        Assert.True(response.Success);
        Assert.NotNull(response.Data);
        Assert.Equal("SSC_GOSI", response.Data.Code);
        _mockRepo.Verify(r => r.AddSSCPolicyAsync(It.Is<SSCPolicy>(p => p.Code == "SSC_GOSI"), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Theory]
    [InlineData(0, "TAX01")]
    [InlineData(1, "")]
    public async Task AddTaxPolicy_InvalidCompanyOrCode_ThrowsArgumentException(long companyId, string code)
    {
        var dto = CreateTaxDto(companyId: companyId, code: code);
        await Assert.ThrowsAsync<ArgumentException>(() =>
            _controller.AddTaxPolicy(dto, CancellationToken.None));
    }

    [Fact]
    public async Task AddTaxPolicy_InvertedDateRange_ThrowsArgumentException()
    {
        var dto = CreateTaxDto(
            effectiveFrom: new DateTime(2026, 6, 1),
            effectiveTo: new DateTime(2026, 5, 1));

        var ex = await Assert.ThrowsAsync<ArgumentException>(() =>
            _controller.AddTaxPolicy(dto, CancellationToken.None));
        Assert.Contains("EffectiveTo cannot be earlier than EffectiveFrom", ex.Message);
    }

    [Theory]
    [InlineData(-100, 0, 0)]
    [InlineData(0, -50, 0)]
    [InlineData(0, 0, -0.05)]
    public async Task AddTaxPolicy_NegativeExemptionsOrRates_ThrowsArgumentException(
        decimal selfExemption, decimal depExemption, decimal nationalRate)
    {
        var dto = CreateTaxDto(
            selfExemption: selfExemption,
            depExemption: depExemption,
            nationalRate: nationalRate);

        await Assert.ThrowsAsync<ArgumentException>(() =>
            _controller.AddTaxPolicy(dto, CancellationToken.None));
    }

    [Fact]
    public async Task AddTaxPolicy_InvalidBrackets_ThrowsArgumentException()
    {
        var dtoWithNegativeLower = CreateTaxDto(
            code: "TAX_BRK1",
            brackets: new List<TaxBracketDto>
            {
                new(1, -10m, 1000m, 5m, null)
            });
        await Assert.ThrowsAsync<ArgumentException>(() =>
            _controller.AddTaxPolicy(dtoWithNegativeLower, CancellationToken.None));

        var dtoWithInvertedLimits = CreateTaxDto(
            code: "TAX_BRK2",
            brackets: new List<TaxBracketDto>
            {
                new(1, 5000m, 4000m, 5m, null)
            });
        var ex = await Assert.ThrowsAsync<ArgumentException>(() =>
            _controller.AddTaxPolicy(dtoWithInvertedLimits, CancellationToken.None));
        Assert.Contains("Bracket upper limit cannot be less than lower limit", ex.Message);

        var dtoWithNegativeRate = CreateTaxDto(
            code: "TAX_BRK3",
            brackets: new List<TaxBracketDto>
            {
                new(1, 0m, 1000m, -5m, null)
            });
        await Assert.ThrowsAsync<ArgumentException>(() =>
            _controller.AddTaxPolicy(dtoWithNegativeRate, CancellationToken.None));
    }

    [Fact]
    public async Task AddTaxPolicy_ValidInput_AddsPolicyAndReturnsSuccess()
    {
        var dto = CreateTaxDto(
            companyId: 1,
            code: "TAX_PROG",
            selfExemption: 9000m,
            depExemption: 1000m,
            nationalRate: 0.01m,
            brackets: new List<TaxBracketDto>
            {
                new(1, 0m, 5000m, 5m, "الشريحة الأولى"),
                new(2, 5000m, 10000m, 10m, "الشريحة الثانية")
            });

        var actionResult = await _controller.AddTaxPolicy(dto, CancellationToken.None);
        var okResult = Assert.IsType<OkObjectResult>(actionResult.Result);
        var response = Assert.IsType<ApiResponse<TaxPolicy>>(okResult.Value);

        Assert.True(response.Success);
        Assert.NotNull(response.Data);
        Assert.Equal("TAX_PROG", response.Data.Code);
        Assert.Equal(2, response.Data.Brackets.Count);
        _mockRepo.Verify(r => r.AddTaxPolicyAsync(It.Is<TaxPolicy>(p => p.Code == "TAX_PROG"), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Theory]
    [InlineData(0, "DED01")]
    [InlineData(1, "")]
    public async Task AddDeductionPolicy_InvalidCompanyOrCode_ThrowsArgumentException(long companyId, string code)
    {
        var dto = CreateDeductionDto(companyId: companyId, code: code);
        await Assert.ThrowsAsync<ArgumentException>(() =>
            _controller.AddDeductionPolicy(dto, CancellationToken.None));
    }

    [Fact]
    public async Task AddDeductionPolicy_InvertedDateRange_ThrowsArgumentException()
    {
        var dto = CreateDeductionDto(
            effectiveFrom: new DateTime(2026, 12, 1),
            effectiveTo: new DateTime(2026, 11, 1));

        var ex = await Assert.ThrowsAsync<ArgumentException>(() =>
            _controller.AddDeductionPolicy(dto, CancellationToken.None));
        Assert.Contains("EffectiveTo cannot be earlier than EffectiveFrom", ex.Message);
    }

    [Theory]
    [InlineData(-1, 0)]
    [InlineData(101, 0)]
    [InlineData(50, -100)]
    public async Task AddDeductionPolicy_InvalidMaxDeductionOrGuarantee_ThrowsArgumentException(
        decimal maxDeduction, decimal minGuarantee)
    {
        var dto = CreateDeductionDto(
            maxDeductionPct: maxDeduction,
            minNetPay: minGuarantee);

        await Assert.ThrowsAsync<ArgumentException>(() =>
            _controller.AddDeductionPolicy(dto, CancellationToken.None));
    }

    [Fact]
    public async Task AddDeductionPolicy_ValidInput_AddsPolicyAndReturnsSuccess()
    {
        var dto = CreateDeductionDto(
            companyId: 2,
            code: "DED_STD",
            maxDeductionPct: 50.0m,
            minNetPay: 250.0m,
            autoCap: true,
            allowNegative: false,
            isDefault: true);

        var actionResult = await _controller.AddDeductionPolicy(dto, CancellationToken.None);
        var okResult = Assert.IsType<OkObjectResult>(actionResult.Result);
        var response = Assert.IsType<ApiResponse<DeductionPolicy>>(okResult.Value);

        Assert.True(response.Success);
        Assert.NotNull(response.Data);
        Assert.Equal("DED_STD", response.Data.Code);
        _mockRepo.Verify(r => r.AddDeductionPolicyAsync(It.Is<DeductionPolicy>(p => p.Code == "DED_STD"), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task GetPolicies_DispatchesToRepositoryAccurately()
    {
        _mockRepo.Setup(r => r.GetProrationPoliciesAsync(1L, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<ProrationPolicy> { new() { Code = "P1" } });
        _mockRepo.Setup(r => r.GetSSCPoliciesAsync(1L, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<SSCPolicy> { new() { Code = "S1" } });
        _mockRepo.Setup(r => r.GetTaxPoliciesAsync(1L, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<TaxPolicy> { new() { Code = "T1" } });
        _mockRepo.Setup(r => r.GetDeductionPoliciesAsync(1L, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<DeductionPolicy> { new() { Code = "D1" } });

        var pror = await _controller.GetProrationPolicies(1L, CancellationToken.None);
        var ssc = await _controller.GetSSCPolicies(1L, CancellationToken.None);
        var tax = await _controller.GetTaxPolicies(1L, CancellationToken.None);
        var ded = await _controller.GetDeductionPolicies(1L, CancellationToken.None);

        var prorResult = Assert.IsType<OkObjectResult>(pror.Result);
        var prorResponse = Assert.IsType<ApiResponse<IReadOnlyList<ProrationPolicy>>>(prorResult.Value);
        Assert.NotNull(prorResponse.Data);
        Assert.Single(prorResponse.Data);

        var sscResult = Assert.IsType<OkObjectResult>(ssc.Result);
        var sscResponse = Assert.IsType<ApiResponse<IReadOnlyList<SSCPolicy>>>(sscResult.Value);
        Assert.NotNull(sscResponse.Data);
        Assert.Single(sscResponse.Data);

        var taxResult = Assert.IsType<OkObjectResult>(tax.Result);
        var taxResponse = Assert.IsType<ApiResponse<IReadOnlyList<TaxPolicy>>>(taxResult.Value);
        Assert.NotNull(taxResponse.Data);
        Assert.Single(taxResponse.Data);

        var dedResult = Assert.IsType<OkObjectResult>(ded.Result);
        var dedResponse = Assert.IsType<ApiResponse<IReadOnlyList<DeductionPolicy>>>(dedResult.Value);
        Assert.NotNull(dedResponse.Data);
        Assert.Single(dedResponse.Data);
    }

    [Fact]
    public async Task GetPolicies_PropagatesCancellationToken()
    {
        using var cts = new CancellationTokenSource();
        var ct = cts.Token;

        _mockRepo.Setup(r => r.GetProrationPoliciesAsync(1L, ct)).ReturnsAsync(new List<ProrationPolicy>());
        _mockRepo.Setup(r => r.GetSSCPoliciesAsync(1L, ct)).ReturnsAsync(new List<SSCPolicy>());
        _mockRepo.Setup(r => r.GetTaxPoliciesAsync(1L, ct)).ReturnsAsync(new List<TaxPolicy>());
        _mockRepo.Setup(r => r.GetDeductionPoliciesAsync(1L, ct)).ReturnsAsync(new List<DeductionPolicy>());

        await _controller.GetProrationPolicies(1L, ct);
        await _controller.GetSSCPolicies(1L, ct);
        await _controller.GetTaxPolicies(1L, ct);
        await _controller.GetDeductionPolicies(1L, ct);

        _mockRepo.Verify(r => r.GetProrationPoliciesAsync(1L, ct), Times.Once);
        _mockRepo.Verify(r => r.GetSSCPoliciesAsync(1L, ct), Times.Once);
        _mockRepo.Verify(r => r.GetTaxPoliciesAsync(1L, ct), Times.Once);
        _mockRepo.Verify(r => r.GetDeductionPoliciesAsync(1L, ct), Times.Once);
    }

    [Fact]
    public async Task AddProrationPolicy_PropagatesCancellationToken()
    {
        using var cts = new CancellationTokenSource();
        var ct = cts.Token;
        var dto = CreateProrationDto();

        var result = await _controller.AddProrationPolicy(dto, ct);

        Assert.IsType<OkObjectResult>(result.Result);
        _mockRepo.Verify(r => r.AddProrationPolicyAsync(It.IsAny<ProrationPolicy>(), ct), Times.Once);
        _mockRepo.Verify(r => r.SaveChangesAsync(ct), Times.Once);
    }

    [Fact]
    public async Task AddSSCPolicy_PropagatesCancellationToken()
    {
        using var cts = new CancellationTokenSource();
        var ct = cts.Token;
        var dto = CreateSSCDto();

        var result = await _controller.AddSSCPolicy(dto, ct);

        Assert.IsType<OkObjectResult>(result.Result);
        _mockRepo.Verify(r => r.AddSSCPolicyAsync(It.IsAny<SSCPolicy>(), ct), Times.Once);
        _mockRepo.Verify(r => r.SaveChangesAsync(ct), Times.Once);
    }

    [Fact]
    public async Task AddTaxPolicy_PropagatesCancellationToken()
    {
        using var cts = new CancellationTokenSource();
        var ct = cts.Token;
        var dto = CreateTaxDto();

        var result = await _controller.AddTaxPolicy(dto, ct);

        Assert.IsType<OkObjectResult>(result.Result);
        _mockRepo.Verify(r => r.AddTaxPolicyAsync(It.IsAny<TaxPolicy>(), ct), Times.Once);
        _mockRepo.Verify(r => r.SaveChangesAsync(ct), Times.Once);
    }

    [Fact]
    public async Task AddDeductionPolicy_PropagatesCancellationToken()
    {
        using var cts = new CancellationTokenSource();
        var ct = cts.Token;
        var dto = CreateDeductionDto();

        var result = await _controller.AddDeductionPolicy(dto, ct);

        Assert.IsType<OkObjectResult>(result.Result);
        _mockRepo.Verify(r => r.AddDeductionPolicyAsync(It.IsAny<DeductionPolicy>(), ct), Times.Once);
        _mockRepo.Verify(r => r.SaveChangesAsync(ct), Times.Once);
    }

    [Theory]
    [InlineData("PRORATION")]
    [InlineData("SSC")]
    [InlineData("TAX")]
    [InlineData("DEDUCTION")]
    public async Task AddPolicy_WhenEffectiveToEarlierThanEffectiveFrom_ThrowsArgumentException(string policyType)
    {
        var from = new DateTime(2026, 6, 1);
        var to = new DateTime(2026, 5, 31); // Earlier than From

        switch (policyType)
        {
            case "PRORATION":
                await Assert.ThrowsAsync<ArgumentException>(() =>
                    _controller.AddProrationPolicy(CreateProrationDto(effectiveFrom: from, effectiveTo: to), CancellationToken.None));
                break;
            case "SSC":
                await Assert.ThrowsAsync<ArgumentException>(() =>
                    _controller.AddSSCPolicy(CreateSSCDto(effectiveFrom: from, effectiveTo: to), CancellationToken.None));
                break;
            case "TAX":
                await Assert.ThrowsAsync<ArgumentException>(() =>
                    _controller.AddTaxPolicy(CreateTaxDto(effectiveFrom: from, effectiveTo: to), CancellationToken.None));
                break;
            case "DEDUCTION":
                await Assert.ThrowsAsync<ArgumentException>(() =>
                    _controller.AddDeductionPolicy(CreateDeductionDto(effectiveFrom: from, effectiveTo: to), CancellationToken.None));
                break;
        }
    }

    [Theory]
    [InlineData(-1, 50, 10)] // Lower limit negative
    [InlineData(100, 50, 10)] // Upper limit < Lower limit
    [InlineData(0, 100, -5)] // Rate percent negative
    public async Task AddTaxPolicy_WhenBracketsInvalid_ThrowsArgumentException(decimal lower, decimal upper, decimal rate)
    {
        var brackets = new List<TaxBracketDto>
        {
            new(1, lower, upper, rate, "Bracket 1")
        };
        var dto = CreateTaxDto(brackets: brackets);

        await Assert.ThrowsAsync<ArgumentException>(() => _controller.AddTaxPolicy(dto, CancellationToken.None));
    }

    [Theory]
    [InlineData(-5, 250)] // MaxDeductionPct negative
    [InlineData(105, 250)] // MaxDeductionPct > 100
    [InlineData(50, -10)] // MinNetPay negative
    public async Task AddDeductionPolicy_WhenLimitsInvalid_ThrowsArgumentException(decimal maxPct, decimal minNet)
    {
        var dto = CreateDeductionDto(maxDeductionPct: maxPct, minNetPay: minNet);

        await Assert.ThrowsAsync<ArgumentException>(() => _controller.AddDeductionPolicy(dto, CancellationToken.None));
    }

    [Theory]
    [InlineData(-0.01, 0.11, 0, 1000, 0)]
    [InlineData(0.09, -0.01, 0, 1000, 0)]
    [InlineData(0.09, 0.11, -0.01, 1000, 0)]
    [InlineData(0.09, 0.11, 0, -100, 0)]
    [InlineData(0.09, 0.11, 0, 1000, -10)]
    public async Task AddSSCPolicy_WhenValuesNegative_ThrowsArgumentException(decimal eeRate, decimal erRate, decimal hrRate, decimal cap, decimal floor)
    {
        var dto = CreateSSCDto(employeeRate: eeRate, employerRate: erRate, highRiskRate: hrRate, ceilingCap: cap, minWage: floor);

        await Assert.ThrowsAsync<ArgumentException>(() => _controller.AddSSCPolicy(dto, CancellationToken.None));
    }

    [Theory]
    [InlineData("UNKNOWN_METHOD", "UNCONFIGURED")]
    [InlineData("CALENDAR_DAYS", "INVALID_ALLOCATION")]
    public async Task AddProrationPolicy_WhenMethodOrAllocationInvalid_ThrowsArgumentException(string method, string allocation)
    {
        var dto = CreateProrationDto(method: method, allocation: allocation);

        await Assert.ThrowsAsync<ArgumentException>(() => _controller.AddProrationPolicy(dto, CancellationToken.None));
    }
}
