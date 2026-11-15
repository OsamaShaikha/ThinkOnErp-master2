using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Moq;
using ThinkOnErp.Application.Services.Hr;
using ThinkOnErp.Domain.Entities.Hr;
using ThinkOnErp.Domain.Interfaces.Hr;
using Xunit;

namespace ThinkOnErp.HR.Tests;

public class PayrollPolicyAndSegmentTests
{
    [Fact]
    public void PayrollCurrencyRules_GetCurrencyDecimals_ReturnsExpectedDecimals()
    {
        Assert.Equal(3, PayrollCurrencyRules.GetCurrencyDecimals("JOD"));
        Assert.Equal(3, PayrollCurrencyRules.GetCurrencyDecimals("KWD"));
        Assert.Equal(3, PayrollCurrencyRules.GetCurrencyDecimals("BHD"));
        Assert.Equal(3, PayrollCurrencyRules.GetCurrencyDecimals("OMR"));

        Assert.Equal(2, PayrollCurrencyRules.GetCurrencyDecimals("SAR"));
        Assert.Equal(2, PayrollCurrencyRules.GetCurrencyDecimals("AED"));
        Assert.Equal(2, PayrollCurrencyRules.GetCurrencyDecimals("USD"));
        Assert.Equal(2, PayrollCurrencyRules.GetCurrencyDecimals("EUR"));
        Assert.Equal(2, PayrollCurrencyRules.GetCurrencyDecimals("EGP"));

        Assert.Equal(0, PayrollCurrencyRules.GetCurrencyDecimals("JPY"));
    }

    [Fact]
    public void SSCCalculationService_WithCustomCeilingAndRates_CalculatesAccurately()
    {
        var mockRepo = new Mock<IPolicyRepository>();
        var sscService = new SSCCalculationService(mockRepo.Object);

        // Custom company policy: Saudi GOSI style (e.g. 9.75% employee, 11.75% employer, ceiling 45,000 SAR)
        var policy = new SSCPolicy
        {
            Code = "GOSI_SA",
            EmployeeContribRate = 0.0975m,
            EmployerContribRate = 0.1175m,
            HighRiskSurchargeRate = 0.01m,
            MonthlyCeilingCap = 45000m
        };

        var result = sscService.CalculateSSC(10000m, isHighRiskRole: false, policy, roundingDecimals: 2);

        Assert.Equal(10000m, result.EligibleSalary);
        Assert.Equal(975.00m, result.EmployeeContribution); // 10000 * 9.75%
        Assert.Equal(1175.00m, result.EmployerContribution); // 10000 * 11.75%
        Assert.Equal("GOSI_SA", result.PolicyCode);
    }

    [Fact]
    public void SSCCalculationService_WithUncappedCeiling_CalculatesOnFullGross()
    {
        var mockRepo = new Mock<IPolicyRepository>();
        var sscService = new SSCCalculationService(mockRepo.Object);

        // Uncapped ceiling (MonthlyCeilingCap = 0)
        var policy = new SSCPolicy
        {
            Code = "UNCAPPED_SSC",
            EmployeeContribRate = 0.05m,
            EmployerContribRate = 0.10m,
            MonthlyCeilingCap = 0m
        };

        var result = sscService.CalculateSSC(50000m, isHighRiskRole: false, policy, roundingDecimals: 2);

        Assert.Equal(50000m, result.EligibleSalary);
        Assert.Equal(2500.00m, result.EmployeeContribution); // 50000 * 5%
        Assert.Equal(5000.00m, result.EmployerContribution); // 50000 * 10%
    }

    [Fact]
    public void TaxCalculationEngine_WithCustomMaxDependents_ClampsDependentsAccurately()
    {
        var mockRepo = new Mock<IPolicyRepository>();
        var taxEngine = new TaxCalculationEngine(mockRepo.Object);

        // Policy allows up to 5 dependents (instead of fixed 3)
        var policy = new TaxPolicy
        {
            Code = "CUSTOM_TAX",
            PersonalExemptionSelf = 10000m,
            PersonalExemptionDependent = 2000m,
            MaxExemptedDependents = 5,
            IsSscTaxDeductible = false
        };

        // Employee has 4 dependents: all 4 should be counted (10000 + 4*2000 = 18000)
        var result = taxEngine.CalculateIncomeTax(1000m, 0m, dependentCount: 4, policy, roundingDecimals: 2);
        Assert.Equal(18000m, result.TotalAnnualExemptions);

        // Employee has 7 dependents: clamped to max 5 (10000 + 5*2000 = 20000)
        var resultClamped = taxEngine.CalculateIncomeTax(1000m, 0m, dependentCount: 7, policy, roundingDecimals: 2);
        Assert.Equal(20000m, resultClamped.TotalAnnualExemptions);
    }

    [Fact]
    public void TaxCalculationEngine_WithCompanyBrackets_EvaluatesCustomBracketsAccurately()
    {
        var mockRepo = new Mock<IPolicyRepository>();
        var taxEngine = new TaxCalculationEngine(mockRepo.Object);

        // Flat tax bracket: 10% on everything above exemption
        var policy = new TaxPolicy
        {
            Code = "FLAT_10",
            PersonalExemptionSelf = 12000m, // 1000/month
            PersonalExemptionDependent = 0m,
            MaxExemptedDependents = 0,
            IsSscTaxDeductible = false,
            Brackets = new List<TaxBracket>
            {
                new() { BracketOrder = 1, LowerLimit = 0, UpperLimit = null, RatePercent = 10.0m }
            }
        };

        // Gross = 2000/month => Annual = 24000. Exemption = 12000 => Annual taxable = 12000.
        // Tax = 12000 * 10% = 1200 annual => 100 monthly.
        var result = taxEngine.CalculateIncomeTax(2000m, 0m, dependentCount: 0, policy, roundingDecimals: 2);

        Assert.Equal(12000m, result.AnnualTaxableNet);
        Assert.Equal(100.00m, result.MonthlyTax);
    }

    [Fact]
    public void TaxCalculationEngine_WithCurrencyDecimals_RoundsToExpectedPrecision()
    {
        var mockRepo = new Mock<IPolicyRepository>();
        var taxEngine = new TaxCalculationEngine(mockRepo.Object);

        var policy = new TaxPolicy
        {
            Code = "DECIMAL_CHECK",
            PersonalExemptionSelf = 0m,
            PersonalExemptionDependent = 0m,
            MaxExemptedDependents = 0,
            IsSscTaxDeductible = false,
            Brackets = new List<TaxBracket>
            {
                new() { BracketOrder = 1, LowerLimit = 0, UpperLimit = null, RatePercent = 10.0m }
            }
        };

        // Gross = 1000.3333 SAR => Tax = 100.03333 => rounded to 2 decimals = 100.03
        var result2 = taxEngine.CalculateIncomeTax(1000.3333m, 0m, dependentCount: 0, policy, roundingDecimals: 2);
        Assert.Equal(100.03m, result2.MonthlyTax);

        // Gross = 1000.3333 JOD => Tax = 100.03333 => rounded to 3 decimals = 100.033
        var result3 = taxEngine.CalculateIncomeTax(1000.3333m, 0m, dependentCount: 0, policy, roundingDecimals: 3);
        Assert.Equal(100.033m, result3.MonthlyTax);
    }

    [Fact]
    public void PayrollSalarySegments_Build_EmployeeHiredAfterPeriodEnd_ReturnsEmpty()
    {
        var period = new PayrollPeriod
        {
            PeriodCode = "2026-11",
            StartDate = new DateTime(2026, 11, 1),
            EndDate = new DateTime(2026, 11, 30)
        };

        var employee = new Employee
        {
            EmployeeCode = "EMP_FUTURE",
            HireDate = new DateTime(2026, 12, 1) // Hired next month
        };

        var segments = PayrollSalarySegments.Build(employee, period);
        Assert.Empty(segments);
    }

    [Fact]
    public void PayrollSalarySegments_Build_EmployeeTerminatedBeforePeriodStart_ReturnsEmpty()
    {
        var period = new PayrollPeriod
        {
            PeriodCode = "2026-11",
            StartDate = new DateTime(2026, 11, 1),
            EndDate = new DateTime(2026, 11, 30)
        };

        var employee = new Employee
        {
            EmployeeCode = "EMP_TERM_PAST",
            HireDate = new DateTime(2020, 1, 1),
            TerminationDate = new DateTime(2026, 10, 31) // Left last month
        };

        var segments = PayrollSalarySegments.Build(employee, period);
        Assert.Empty(segments);
    }

    [Fact]
    public void PayrollSalarySegments_Build_ContinuousSingleStructure_ReturnsSingleSegmentCoveringPeriod()
    {
        var period = new PayrollPeriod
        {
            PeriodCode = "2026-11",
            StartDate = new DateTime(2026, 11, 1),
            EndDate = new DateTime(2026, 11, 30)
        };

        var employee = new Employee
        {
            EmployeeCode = "EMP_NORMAL",
            HireDate = new DateTime(2020, 1, 1)
        };
        employee.SalaryStructures.Add(new SalaryStructure
        {
            EffectiveFrom = new DateTime(2020, 1, 1),
            BasicSalary = 1000m,
            CurrencyCode = "JOD",
            IsActive = true
        });

        var segments = PayrollSalarySegments.Build(employee, period);
        var segment = Assert.Single(segments);
        Assert.Equal(period.StartDate, segment.Start);
        Assert.Equal(period.EndDate, segment.End);
        Assert.Equal(1000m, segment.Structure.BasicSalary);
    }

    [Fact]
    public void PayrollSalarySegments_Build_TwoContiguousStructures_ReturnsTwoSegments()
    {
        var period = new PayrollPeriod
        {
            PeriodCode = "2026-11",
            StartDate = new DateTime(2026, 11, 1),
            EndDate = new DateTime(2026, 11, 30)
        };

        var employee = new Employee
        {
            EmployeeCode = "EMP_TRANS",
            HireDate = new DateTime(2020, 1, 1)
        };
        // Seg 1: Nov 1 to Nov 15
        employee.SalaryStructures.Add(new SalaryStructure
        {
            EffectiveFrom = new DateTime(2020, 1, 1),
            EffectiveTo = new DateTime(2026, 11, 15),
            BasicSalary = 1000m,
            CurrencyCode = "JOD",
            IsActive = true
        });
        // Seg 2: Nov 16 to open
        employee.SalaryStructures.Add(new SalaryStructure
        {
            EffectiveFrom = new DateTime(2026, 11, 16),
            BasicSalary = 1200m,
            CurrencyCode = "JOD",
            IsActive = true
        });

        var segments = PayrollSalarySegments.Build(employee, period);
        Assert.Equal(2, segments.Count);
        Assert.Equal(new DateTime(2026, 11, 1), segments[0].Start);
        Assert.Equal(new DateTime(2026, 11, 15), segments[0].End);
        Assert.Equal(1000m, segments[0].Structure.BasicSalary);

        Assert.Equal(new DateTime(2026, 11, 16), segments[1].Start);
        Assert.Equal(new DateTime(2026, 11, 30), segments[1].End);
        Assert.Equal(1200m, segments[1].Structure.BasicSalary);
    }

    [Fact]
    public void PayrollSalarySegments_Build_OverlappingStructures_ThrowsInvalidOperationException()
    {
        var period = new PayrollPeriod
        {
            PeriodCode = "2026-11",
            StartDate = new DateTime(2026, 11, 1),
            EndDate = new DateTime(2026, 11, 30)
        };

        var employee = new Employee
        {
            EmployeeCode = "EMP_OVERLAP",
            HireDate = new DateTime(2020, 1, 1)
        };
        // Seg 1 ends on Nov 20, but Seg 2 starts on Nov 15 (overlap 15..20)
        employee.SalaryStructures.Add(new SalaryStructure
        {
            EffectiveFrom = new DateTime(2020, 1, 1),
            EffectiveTo = new DateTime(2026, 11, 20),
            BasicSalary = 1000m,
            CurrencyCode = "JOD",
            IsActive = true
        });
        employee.SalaryStructures.Add(new SalaryStructure
        {
            EffectiveFrom = new DateTime(2026, 11, 15),
            BasicSalary = 1200m,
            CurrencyCode = "JOD",
            IsActive = true
        });

        var ex = Assert.Throws<InvalidOperationException>(() => PayrollSalarySegments.Build(employee, period));
        Assert.Contains("overlapping salary structures", ex.Message);
    }

    [Fact]
    public void PayrollSalarySegments_Build_GapBetweenStructures_ThrowsInvalidOperationException()
    {
        var period = new PayrollPeriod
        {
            PeriodCode = "2026-11",
            StartDate = new DateTime(2026, 11, 1),
            EndDate = new DateTime(2026, 11, 30)
        };

        var employee = new Employee
        {
            EmployeeCode = "EMP_GAP",
            HireDate = new DateTime(2020, 1, 1)
        };
        // Seg 1 ends Nov 10, Seg 2 starts Nov 15 (gap Nov 11..14)
        employee.SalaryStructures.Add(new SalaryStructure
        {
            EffectiveFrom = new DateTime(2020, 1, 1),
            EffectiveTo = new DateTime(2026, 11, 10),
            BasicSalary = 1000m,
            CurrencyCode = "JOD",
            IsActive = true
        });
        employee.SalaryStructures.Add(new SalaryStructure
        {
            EffectiveFrom = new DateTime(2026, 11, 15),
            BasicSalary = 1200m,
            CurrencyCode = "JOD",
            IsActive = true
        });

        var ex = Assert.Throws<InvalidOperationException>(() => PayrollSalarySegments.Build(employee, period));
        Assert.Contains("coverage gap inside payroll period", ex.Message);
    }

    [Fact]
    public void PayrollSalarySegments_Build_IncompleteCoverageAtStart_ThrowsInvalidOperationException()
    {
        var period = new PayrollPeriod
        {
            PeriodCode = "2026-11",
            StartDate = new DateTime(2026, 11, 1),
            EndDate = new DateTime(2026, 11, 30)
        };

        var employee = new Employee
        {
            EmployeeCode = "EMP_LATE_START",
            HireDate = new DateTime(2020, 1, 1) // Hired long ago
        };
        // But salary structure only starts on Nov 5 (uncovered Nov 1..4)
        employee.SalaryStructures.Add(new SalaryStructure
        {
            EffectiveFrom = new DateTime(2026, 11, 5),
            BasicSalary = 1000m,
            CurrencyCode = "JOD",
            IsActive = true
        });

        var ex = Assert.Throws<InvalidOperationException>(() => PayrollSalarySegments.Build(employee, period));
        Assert.Contains("coverage gap", ex.Message);
    }

    [Fact]
    public void PayrollSalarySegments_Build_IncompleteCoverageAtEnd_ThrowsInvalidOperationException()
    {
        var period = new PayrollPeriod
        {
            PeriodCode = "2026-11",
            StartDate = new DateTime(2026, 11, 1),
            EndDate = new DateTime(2026, 11, 30)
        };

        var employee = new Employee
        {
            EmployeeCode = "EMP_EARLY_END",
            HireDate = new DateTime(2020, 1, 1)
        };
        // Salary structure ends on Nov 25, while period ends on Nov 30 (uncovered Nov 26..30)
        employee.SalaryStructures.Add(new SalaryStructure
        {
            EffectiveFrom = new DateTime(2020, 1, 1),
            EffectiveTo = new DateTime(2026, 11, 25),
            BasicSalary = 1000m,
            CurrencyCode = "JOD",
            IsActive = true
        });

        var ex = Assert.Throws<InvalidOperationException>(() => PayrollSalarySegments.Build(employee, period));
        Assert.Contains("has no salary covering all eligible payroll dates", ex.Message);
    }

    [Fact]
    public void PayrollSalarySegments_Build_DifferentCurrenciesAcrossSegments_ThrowsInvalidOperationException()
    {
        var period = new PayrollPeriod
        {
            PeriodCode = "2026-11",
            StartDate = new DateTime(2026, 11, 1),
            EndDate = new DateTime(2026, 11, 30)
        };

        var employee = new Employee
        {
            EmployeeCode = "EMP_MIX_CURR",
            HireDate = new DateTime(2020, 1, 1)
        };
        // Seg 1 JOD, Seg 2 USD
        employee.SalaryStructures.Add(new SalaryStructure
        {
            EffectiveFrom = new DateTime(2020, 1, 1),
            EffectiveTo = new DateTime(2026, 11, 15),
            BasicSalary = 1000m,
            CurrencyCode = "JOD",
            IsActive = true
        });
        employee.SalaryStructures.Add(new SalaryStructure
        {
            EffectiveFrom = new DateTime(2026, 11, 16),
            BasicSalary = 1500m,
            CurrencyCode = "USD",
            IsActive = true
        });

        var ex = Assert.Throws<InvalidOperationException>(() => PayrollSalarySegments.Build(employee, period));
        Assert.Contains("Payroll cannot combine different currencies", ex.Message);
    }

    [Fact]
    public void PayrollSalarySegments_Build_MidPeriodHireAndTerm_ClampsSegmentsToEligibleDates()
    {
        var period = new PayrollPeriod
        {
            PeriodCode = "2026-11",
            StartDate = new DateTime(2026, 11, 1),
            EndDate = new DateTime(2026, 11, 30)
        };

        var employee = new Employee
        {
            EmployeeCode = "EMP_MID_SPAN",
            HireDate = new DateTime(2026, 11, 10),
            TerminationDate = new DateTime(2026, 11, 25)
        };
        employee.SalaryStructures.Add(new SalaryStructure
        {
            EffectiveFrom = new DateTime(2026, 11, 1), // Starts before hire
            EffectiveTo = null,                        // Open-ended
            BasicSalary = 1000m,
            CurrencyCode = "JOD",
            IsActive = true
        });

        var segments = PayrollSalarySegments.Build(employee, period);
        var segment = Assert.Single(segments);
        Assert.Equal(new DateTime(2026, 11, 10), segment.Start); // Clamped to HireDate
        Assert.Equal(new DateTime(2026, 11, 25), segment.End);   // Clamped to TerminationDate
    }
}

