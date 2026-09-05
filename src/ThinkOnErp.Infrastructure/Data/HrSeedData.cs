using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using ThinkOnErp.Domain.Entities.Hr;

namespace ThinkOnErp.Infrastructure.Data;

public static class HrSeedData
{
    public static async Task SeedStatutoryRulesAsync(OracleDbContext context)
    {
        if (await context.StatutoryRules.AnyAsync())
        {
            return;
        }

        var effectiveFrom = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc);
        var now = DateTime.UtcNow;
        const string user = "system_seed";

        var rules = new List<StatutoryRule>
        {
            // Social Security Corporation (SSC) Contributions
            new()
            {
                RuleType = "SSC_EMPLOYER_RATE",
                RuleName = "Jordan SSC Employer Contribution Rate",
                RatePercent = 0.1425m,
                EffectiveFrom = effectiveFrom,
                Description = "14.25% standard employer contribution rate",
                CreationUser = user,
                CreationDate = now
            },
            new()
            {
                RuleType = "SSC_EMPLOYEE_RATE",
                RuleName = "Jordan SSC Employee Contribution Rate",
                RatePercent = 0.0750m,
                EffectiveFrom = effectiveFrom,
                Description = "7.50% standard employee contribution deduction rate",
                CreationUser = user,
                CreationDate = now
            },
            new()
            {
                RuleType = "SSC_HIGH_RISK_SURCHARGE",
                RuleName = "Jordan SSC High Risk Hazardous Role Surcharge",
                RatePercent = 0.0100m,
                EffectiveFrom = effectiveFrom,
                Description = "1.00% additional employer surcharge for hazardous / high-risk jobs",
                CreationUser = user,
                CreationDate = now
            },
            new()
            {
                RuleType = "SSC_CEILING",
                RuleName = "Jordan SSC Monthly Wage Ceiling Cap",
                Value = 3349.00m,
                EffectiveFrom = effectiveFrom,
                Description = "3,349.00 JOD statutory monthly wage cap for SSC calculations",
                CreationUser = user,
                CreationDate = now
            },

            // Minimum Wage
            new()
            {
                RuleType = "MIN_WAGE",
                RuleName = "Jordan Statutory Minimum Monthly Wage",
                Value = 290.00m,
                EffectiveFrom = effectiveFrom,
                Description = "290.00 JOD statutory minimum monthly wage",
                CreationUser = user,
                CreationDate = now
            },

            // Income & Sales Tax Department (ISTD) Personal Exemptions
            new()
            {
                RuleType = "ISTD_PERSONAL_EXEMPTION",
                RuleName = "Jordan ISTD Annual Personal Exemption (Self)",
                Value = 9000.00m,
                EffectiveFrom = effectiveFrom,
                Description = "9,000.00 JOD annual resident personal tax exemption",
                CreationUser = user,
                CreationDate = now
            },
            new()
            {
                RuleType = "ISTD_DEPENDENT_EXEMPTION",
                RuleName = "Jordan ISTD Annual Dependent Exemption (Per Dependent)",
                Value = 1000.00m,
                EffectiveFrom = effectiveFrom,
                Description = "1,000.00 JOD annual tax exemption per claimed dependent (capped at 9,000 JOD)",
                CreationUser = user,
                CreationDate = now
            },
            new()
            {
                RuleType = "ISTD_MAX_DEPENDENT_EXEMPTION",
                RuleName = "Jordan ISTD Max Total Annual Dependent Exemption",
                Value = 9000.00m,
                EffectiveFrom = effectiveFrom,
                Description = "9,000.00 JOD max total annual dependents exemption cap",
                CreationUser = user,
                CreationDate = now
            },

            // Progressive Income Tax Brackets (Annual Taxable Income in JOD)
            new()
            {
                RuleType = "ISTD_TAX_BRACKET",
                RuleName = "Bracket 1: 0 - 5,000 JOD (5%)",
                BracketLow = 0.00m,
                BracketHigh = 5000.00m,
                RatePercent = 0.05m,
                EffectiveFrom = effectiveFrom,
                Description = "5% income tax on first 5,000 JOD taxable income",
                CreationUser = user,
                CreationDate = now
            },
            new()
            {
                RuleType = "ISTD_TAX_BRACKET",
                RuleName = "Bracket 2: 5,000.01 - 10,000 JOD (10%)",
                BracketLow = 5000.00m,
                BracketHigh = 10000.00m,
                RatePercent = 0.10m,
                EffectiveFrom = effectiveFrom,
                Description = "10% income tax on taxable income between 5,000 and 10,000 JOD",
                CreationUser = user,
                CreationDate = now
            },
            new()
            {
                RuleType = "ISTD_TAX_BRACKET",
                RuleName = "Bracket 3: 10,000.01 - 15,000 JOD (15%)",
                BracketLow = 10000.00m,
                BracketHigh = 15000.00m,
                RatePercent = 0.15m,
                EffectiveFrom = effectiveFrom,
                Description = "15% income tax on taxable income between 10,000 and 15,000 JOD",
                CreationUser = user,
                CreationDate = now
            },
            new()
            {
                RuleType = "ISTD_TAX_BRACKET",
                RuleName = "Bracket 4: 15,000.01 - 20,000 JOD (20%)",
                BracketLow = 15000.00m,
                BracketHigh = 20000.00m,
                RatePercent = 0.20m,
                EffectiveFrom = effectiveFrom,
                Description = "20% income tax on taxable income between 15,000 and 20,000 JOD",
                CreationUser = user,
                CreationDate = now
            },
            new()
            {
                RuleType = "ISTD_TAX_BRACKET",
                RuleName = "Bracket 5: 20,000.01 - 1,000,000 JOD (25%)",
                BracketLow = 20000.00m,
                BracketHigh = 1000000.00m,
                RatePercent = 0.25m,
                EffectiveFrom = effectiveFrom,
                Description = "25% income tax on taxable income between 20,000 and 1,000,000 JOD",
                CreationUser = user,
                CreationDate = now
            },
            new()
            {
                RuleType = "ISTD_TAX_BRACKET",
                RuleName = "Bracket 6: > 1,000,000 JOD (30%)",
                BracketLow = 1000000.00m,
                BracketHigh = null,
                RatePercent = 0.30m,
                EffectiveFrom = effectiveFrom,
                Description = "30% income tax on taxable income exceeding 1,000,000 JOD",
                CreationUser = user,
                CreationDate = now
            },

            // National Contribution Surcharge (1% above 200,000 JOD)
            new()
            {
                RuleType = "NATIONAL_CONTRIBUTION_SURCHARGE",
                RuleName = "Jordan National Contribution Surcharge (>200k JOD)",
                BracketLow = 200000.00m,
                RatePercent = 0.0100m,
                EffectiveFrom = effectiveFrom,
                Description = "1.00% solidarity national contribution on taxable income above 200,000 JOD",
                CreationUser = user,
                CreationDate = now
            },

            // Statutory Overtime Multipliers
            new()
            {
                RuleType = "OVERTIME_REGULAR_RATE",
                RuleName = "Jordan Regular Workday Overtime Multiplier",
                Value = 1.25m,
                EffectiveFrom = effectiveFrom,
                Description = "1.25x hourly rate for overtime on standard working days",
                CreationUser = user,
                CreationDate = now
            },
            new()
            {
                RuleType = "OVERTIME_HOLIDAY_RATE",
                RuleName = "Jordan Weekend & Holiday Overtime Multiplier",
                Value = 1.50m,
                EffectiveFrom = effectiveFrom,
                Description = "1.50x hourly rate for overtime on weekends and official holidays",
                CreationUser = user,
                CreationDate = now
            }
        };

        context.StatutoryRules.AddRange(rules);
        await context.SaveChangesAsync();
    }

    public static async Task SeedLeaveTypesAndPoliciesAsync(OracleDbContext context)
    {
        if (await context.LeaveTypes.AnyAsync())
        {
            return;
        }

        var now = DateTime.UtcNow;
        const string user = "system_seed";

        var leaveTypes = new List<LeaveType>
        {
            new()
            {
                LeaveTypeCode = "ANNUAL",
                NameAr = "إجازة سنوية",
                NameEn = "Annual Leave",
                IsPaid = true,
                IsStatutory = true,
                MaxDaysPerYear = 14m,
                CarryForwardAllowed = true,
                CarryForwardCapDays = 7m,
                RequiresDocumentation = false,
                IsActive = true,
                CreationUser = user,
                CreationDate = now
            },
            new()
            {
                LeaveTypeCode = "SICK",
                NameAr = "إجازة مرضية",
                NameEn = "Sick Leave",
                IsPaid = true,
                IsStatutory = true,
                MaxDaysPerYear = 14m,
                CarryForwardAllowed = false,
                CarryForwardCapDays = 0m,
                RequiresDocumentation = true,
                IsActive = true,
                CreationUser = user,
                CreationDate = now
            },
            new()
            {
                LeaveTypeCode = "MATERNITY",
                NameAr = "إجازة أمومة",
                NameEn = "Maternity Leave",
                IsPaid = true,
                IsStatutory = true,
                MaxDaysPerYear = 70m,
                CarryForwardAllowed = false,
                CarryForwardCapDays = 0m,
                RequiresDocumentation = true,
                IsActive = true,
                CreationUser = user,
                CreationDate = now
            },
            new()
            {
                LeaveTypeCode = "PATERNITY",
                NameAr = "إجازة أبوة",
                NameEn = "Paternity Leave",
                IsPaid = true,
                IsStatutory = true,
                MaxDaysPerYear = 3m,
                CarryForwardAllowed = false,
                CarryForwardCapDays = 0m,
                RequiresDocumentation = true,
                IsActive = true,
                CreationUser = user,
                CreationDate = now
            },
            new()
            {
                LeaveTypeCode = "HAJJ",
                NameAr = "إجازة حج",
                NameEn = "Hajj Pilgrimage Leave",
                IsPaid = true,
                IsStatutory = true,
                MaxDaysPerYear = 14m,
                CarryForwardAllowed = false,
                CarryForwardCapDays = 0m,
                RequiresDocumentation = true,
                IsActive = true,
                CreationUser = user,
                CreationDate = now
            },
            new()
            {
                LeaveTypeCode = "BEREAVEMENT",
                NameAr = "إجازة وفاة (عزاء)",
                NameEn = "Bereavement Leave",
                IsPaid = true,
                IsStatutory = true,
                MaxDaysPerYear = 3m,
                CarryForwardAllowed = false,
                CarryForwardCapDays = 0m,
                RequiresDocumentation = false,
                IsActive = true,
                CreationUser = user,
                CreationDate = now
            },
            new()
            {
                LeaveTypeCode = "UNPAID",
                NameAr = "إجازة بدون راتب",
                NameEn = "Unpaid Leave",
                IsPaid = false,
                IsStatutory = false,
                MaxDaysPerYear = 30m,
                CarryForwardAllowed = false,
                CarryForwardCapDays = 0m,
                RequiresDocumentation = false,
                IsActive = true,
                CreationUser = user,
                CreationDate = now
            }
        };

        context.LeaveTypes.AddRange(leaveTypes);
        await context.SaveChangesAsync();

        var policies = new List<LeavePolicy>
        {
            new()
            {
                LeaveTypeCode = "ANNUAL",
                PolicyName = "Jordan Statutory Tiered Annual Leave Policy (14/21 days)",
                ApplicableTo = "ALL",
                AccrualMethod = "SERVICE_TIERED",
                AccrualRate = 1.1667m,
                MinServiceMonths = 0,
                Tier1YearsThreshold = 5,
                Tier1Days = 14m,
                Tier2Days = 21m,
                CreationUser = user,
                CreationDate = now
            },
            new()
            {
                LeaveTypeCode = "SICK",
                PolicyName = "Jordan Statutory Sick Leave Policy (14 days)",
                ApplicableTo = "ALL",
                AccrualMethod = "ANNUAL_LUMP_SUM",
                AccrualRate = 14m,
                MinServiceMonths = 0,
                Tier1Days = 14m,
                CreationUser = user,
                CreationDate = now
            }
        };

        context.LeavePolicies.AddRange(policies);
        await context.SaveChangesAsync();
    }

    public static async Task SeedSalaryComponentsAndShiftsAsync(OracleDbContext context)
    {
        var now = DateTime.UtcNow;
        const string user = "system_seed";

        if (!await context.SalaryComponents.AnyAsync())
        {
            var components = new List<SalaryComponent>
            {
                new() { ComponentCode = "BASIC", NameAr = "الراتب الأساسي", NameEn = "Basic Salary", ComponentType = "EARNING", IsTaxable = true, IsSscApplicable = true, CalculationType = "FIXED_AMOUNT", CreationUser = user, CreationDate = now },
                new() { ComponentCode = "HOUSING", NameAr = "بدل سكن", NameEn = "Housing Allowance", ComponentType = "EARNING", IsTaxable = true, IsSscApplicable = true, CalculationType = "FIXED_AMOUNT", CreationUser = user, CreationDate = now },
                new() { ComponentCode = "TRANSPORT", NameAr = "بدل مواصلات", NameEn = "Transportation Allowance", ComponentType = "EARNING", IsTaxable = true, IsSscApplicable = true, CalculationType = "FIXED_AMOUNT", CreationUser = user, CreationDate = now },
                new() { ComponentCode = "MOBILE", NameAr = "بدل هاتف واتصالات", NameEn = "Mobile Allowance", ComponentType = "EARNING", IsTaxable = true, IsSscApplicable = false, CalculationType = "FIXED_AMOUNT", CreationUser = user, CreationDate = now },
                new() { ComponentCode = "FAMILY", NameAr = "علاوة عائلية", NameEn = "Family Allowance", ComponentType = "EARNING", IsTaxable = true, IsSscApplicable = true, CalculationType = "FIXED_AMOUNT", CreationUser = user, CreationDate = now },
                new() { ComponentCode = "OVERTIME", NameAr = "أجر العمل الإضافي", NameEn = "Overtime Pay", ComponentType = "EARNING", IsTaxable = true, IsSscApplicable = true, CalculationType = "FORMULA", CreationUser = user, CreationDate = now },
                new() { ComponentCode = "BONUS", NameAr = "مكافأة أداء", NameEn = "Performance Bonus", ComponentType = "EARNING", IsTaxable = true, IsSscApplicable = true, CalculationType = "FIXED_AMOUNT", CreationUser = user, CreationDate = now },
                new() { ComponentCode = "OTHER_ALLOWANCE", NameAr = "بدلات أخرى", NameEn = "Other Allowance", ComponentType = "EARNING", IsTaxable = true, IsSscApplicable = false, CalculationType = "FIXED_AMOUNT", CreationUser = user, CreationDate = now },
                new() { ComponentCode = "SSC_EMPLOYEE", NameAr = "اقتطاع الضمان الاجتماعي (الموظف)", NameEn = "SSC Employee Contribution", ComponentType = "DEDUCTION", IsTaxable = false, IsSscApplicable = false, CalculationType = "FORMULA", CreationUser = user, CreationDate = now },
                new() { ComponentCode = "INCOME_TAX", NameAr = "ضريبة الدخل المستقطعة", NameEn = "Income Tax Withholding", ComponentType = "DEDUCTION", IsTaxable = false, IsSscApplicable = false, CalculationType = "FORMULA", CreationUser = user, CreationDate = now },
                new() { ComponentCode = "LOAN_DEDUCTION", NameAr = "سداد سلفة / قرض موظف", NameEn = "Employee Loan Deduction", ComponentType = "DEDUCTION", IsTaxable = false, IsSscApplicable = false, CalculationType = "FIXED_AMOUNT", CreationUser = user, CreationDate = now },
                new() { ComponentCode = "OTHER_DEDUCTION", NameAr = "اقتطاعات أخرى", NameEn = "Other Deduction", ComponentType = "DEDUCTION", IsTaxable = false, IsSscApplicable = false, CalculationType = "FIXED_AMOUNT", CreationUser = user, CreationDate = now }
            };

            context.SalaryComponents.AddRange(components);
            await context.SaveChangesAsync();
        }

        if (!await context.ShiftSchedules.AnyAsync())
        {
            var generalShift = new ShiftSchedule
            {
                ShiftCode = "GENERAL",
                NameAr = "الدوام الرسمي العام",
                NameEn = "General Standard Shift",
                StartTime = new TimeSpan(8, 30, 0),
                EndTime = new TimeSpan(17, 0, 0),
                BreakMinutes = 60,
                WorkingDaysJson = "[\"Sunday\",\"Monday\",\"Tuesday\",\"Wednesday\",\"Thursday\"]",
                IsActive = true,
                CreationUser = user,
                CreationDate = now
            };

            context.ShiftSchedules.Add(generalShift);
            await context.SaveChangesAsync();
        }

        if (!await context.Departments.AnyAsync())
        {
            var departments = new List<Department>
            {
                new() { DepartmentCode = "HR", NameAr = "الموارد البشرية", NameEn = "Human Resources", CostCenterCode = "CC-101", IsActive = true, CreationUser = user, CreationDate = now },
                new() { DepartmentCode = "ENG", NameAr = "الهندسة وتطوير البرمجيات", NameEn = "Engineering & Software", CostCenterCode = "CC-102", IsActive = true, CreationUser = user, CreationDate = now },
                new() { DepartmentCode = "FIN", NameAr = "الإدارة المالية والمحاسبة", NameEn = "Finance & Accounting", CostCenterCode = "CC-103", IsActive = true, CreationUser = user, CreationDate = now },
                new() { DepartmentCode = "OPS", NameAr = "العمليات والخدمات اللوجستية", NameEn = "Operations & Logistics", CostCenterCode = "CC-104", IsActive = true, CreationUser = user, CreationDate = now }
            };

            context.Departments.AddRange(departments);
            await context.SaveChangesAsync();
        }

        if (!await context.JobGrades.AnyAsync())
        {
            var grades = new List<JobGrade>
            {
                new() { GradeCode = "EXEC", NameAr = "الدرجة التنفيذية العليا", NameEn = "Executive Grade", Level = 1, MinSalary = 3000m, MidSalary = 5000m, MaxSalary = 8000m, IsActive = true, CreationUser = user, CreationDate = now },
                new() { GradeCode = "SENIOR", NameAr = "الدرجة المتقدمة / الخبراء", NameEn = "Senior Specialist Grade", Level = 2, MinSalary = 1500m, MidSalary = 2200m, MaxSalary = 3200m, IsActive = true, CreationUser = user, CreationDate = now },
                new() { GradeCode = "MID", NameAr = "الدرجة المتوسطة", NameEn = "Mid-Level Professional", Level = 3, MinSalary = 800m, MidSalary = 1200m, MaxSalary = 1600m, IsActive = true, CreationUser = user, CreationDate = now },
                new() { GradeCode = "ENTRY", NameAr = "الدرجة التشغيلية / المبتدئة", NameEn = "Entry / Operational Grade", Level = 4, MinSalary = 350m, MidSalary = 500m, MaxSalary = 750m, IsActive = true, CreationUser = user, CreationDate = now }
            };

            context.JobGrades.AddRange(grades);
            await context.SaveChangesAsync();
        }

        if (!await context.Positions.AnyAsync())
        {
            var positions = new List<Position>
            {
                new() { PositionCode = "HR_DIR", TitleAr = "مدير الموارد البشرية", TitleEn = "HR Director", DepartmentCode = "HR", JobGradeCode = "EXEC", IsActive = true, CreationUser = user, CreationDate = now },
                new() { PositionCode = "SR_SWE", TitleAr = "مطور برمجيات أول", TitleEn = "Senior Software Engineer", DepartmentCode = "ENG", JobGradeCode = "SENIOR", ReportsToPositionCode = "HR_DIR", IsActive = true, CreationUser = user, CreationDate = now },
                new() { PositionCode = "FIN_ACC", TitleAr = "محاسب عام أول", TitleEn = "Senior General Accountant", DepartmentCode = "FIN", JobGradeCode = "SENIOR", ReportsToPositionCode = "HR_DIR", IsActive = true, CreationUser = user, CreationDate = now },
                new() { PositionCode = "OPS_SUP", TitleAr = "مشرف العمليات", TitleEn = "Operations Supervisor", DepartmentCode = "OPS", JobGradeCode = "MID", ReportsToPositionCode = "HR_DIR", IsActive = true, CreationUser = user, CreationDate = now },
                new() { PositionCode = "JR_SWE", TitleAr = "مطور برمجيات مبتدئ", TitleEn = "Junior Software Engineer", DepartmentCode = "ENG", JobGradeCode = "ENTRY", ReportsToPositionCode = "SR_SWE", IsActive = true, CreationUser = user, CreationDate = now }
            };

            context.Positions.AddRange(positions);
            await context.SaveChangesAsync();
        }
    }

    public static async Task SeedComprehensiveEmployeesAndPayrollDataAsync(OracleDbContext context)
    {
        var now = DateTime.UtcNow;
        const string user = "system_seed";

        // 1. Employees Master Data
        if (!await context.Employees.AnyAsync())
        {
            var employees = new List<Employee>
            {
                new()
                {
                    EmployeeCode = "EMP-001",
                    NationalId = "9901020304",
                    SscNumber = "SSC-1001",
                    NameAr = "أحمد خالد الخالدي",
                    NameEn = "Ahmad Khaled Al-Khalidi",
                    DepartmentCode = "ENG",
                    PositionCode = "SR_SWE",
                    Gender = "MALE",
                    MaritalStatus = "MARRIED",
                    Nationality = "Jordanian",
                    DateOfBirth = new DateTime(1990, 5, 14, 0, 0, 0, DateTimeKind.Utc),
                    HireDate = new DateTime(2021, 3, 15, 0, 0, 0, DateTimeKind.Utc),
                    EmploymentStatus = "ACTIVE",
                    EmploymentType = "FULL_TIME",
                    TaxExemptionCount = 2,
                    IsHighRiskRole = false,
                    BankName = "Arab Bank",
                    BankIban = "JO00ARAB0000000000012345678900",
                    ManagerEmployeeCode = "EMP-002",
                    Email = "ahmad.khalidi@thinkon.jo",
                    Phone = "+962791112233",
                    IsActive = true,
                    CreationUser = user,
                    CreationDate = now
                },
                new()
                {
                    EmployeeCode = "EMP-002",
                    NationalId = "9881020304",
                    SscNumber = "SSC-1002",
                    NameAr = "سارة إبراهيم الحسيني",
                    NameEn = "Sarah Ibrahim Al-Husseini",
                    DepartmentCode = "HR",
                    PositionCode = "HR_DIR",
                    Gender = "FEMALE",
                    MaritalStatus = "MARRIED",
                    Nationality = "Jordanian",
                    DateOfBirth = new DateTime(1988, 8, 22, 0, 0, 0, DateTimeKind.Utc),
                    HireDate = new DateTime(2019, 1, 10, 0, 0, 0, DateTimeKind.Utc),
                    EmploymentStatus = "ACTIVE",
                    EmploymentType = "FULL_TIME",
                    TaxExemptionCount = 1,
                    IsHighRiskRole = false,
                    BankName = "Bank of Jordan",
                    BankIban = "JO00BOJO0000000000098765432100",
                    Email = "sarah.husseini@thinkon.jo",
                    Phone = "+962792223344",
                    IsActive = true,
                    CreationUser = user,
                    CreationDate = now
                },
                new()
                {
                    EmployeeCode = "EMP-003",
                    NationalId = "9931020304",
                    SscNumber = "SSC-1003",
                    NameAr = "طارق محمود المجالي",
                    NameEn = "Tareq Mahmoud Al-Majali",
                    DepartmentCode = "FIN",
                    PositionCode = "FIN_ACC",
                    Gender = "MALE",
                    MaritalStatus = "SINGLE",
                    Nationality = "Jordanian",
                    DateOfBirth = new DateTime(1993, 11, 5, 0, 0, 0, DateTimeKind.Utc),
                    HireDate = new DateTime(2022, 6, 1, 0, 0, 0, DateTimeKind.Utc),
                    EmploymentStatus = "ACTIVE",
                    EmploymentType = "FULL_TIME",
                    TaxExemptionCount = 0,
                    IsHighRiskRole = false,
                    BankName = "Housing Bank",
                    BankIban = "JO00HBTF0000000000055555555500",
                    ManagerEmployeeCode = "EMP-002",
                    Email = "tareq.majali@thinkon.jo",
                    Phone = "+962793334455",
                    IsActive = true,
                    CreationUser = user,
                    CreationDate = now
                },
                new()
                {
                    EmployeeCode = "EMP-004",
                    NationalId = "9951020304",
                    SscNumber = "SSC-1004",
                    NameAr = "عمر زياد الزعبي",
                    NameEn = "Omar Ziad Al-Zoubi",
                    DepartmentCode = "OPS",
                    PositionCode = "OPS_SUP",
                    Gender = "MALE",
                    MaritalStatus = "MARRIED",
                    Nationality = "Jordanian",
                    DateOfBirth = new DateTime(1995, 2, 18, 0, 0, 0, DateTimeKind.Utc),
                    HireDate = new DateTime(2023, 11, 1, 0, 0, 0, DateTimeKind.Utc),
                    EmploymentStatus = "ACTIVE",
                    EmploymentType = "FULL_TIME",
                    TaxExemptionCount = 1,
                    IsHighRiskRole = true, // High Risk surcharge test
                    BankName = "Jordan Kuwait Bank",
                    BankIban = "JO00JKBO0000000000077777777700",
                    ManagerEmployeeCode = "EMP-002",
                    Email = "omar.zoubi@thinkon.jo",
                    Phone = "+962794445566",
                    IsActive = true,
                    CreationUser = user,
                    CreationDate = now
                },
                new()
                {
                    EmployeeCode = "EMP-005",
                    NationalId = "2001020304",
                    SscNumber = "SSC-1005",
                    NameAr = "لينا سامي المصري",
                    NameEn = "Lina Sami Al-Masri",
                    DepartmentCode = "ENG",
                    PositionCode = "JR_SWE",
                    Gender = "FEMALE",
                    MaritalStatus = "SINGLE",
                    Nationality = "Jordanian",
                    DateOfBirth = new DateTime(2001, 7, 30, 0, 0, 0, DateTimeKind.Utc),
                    HireDate = new DateTime(2026, 6, 1, 0, 0, 0, DateTimeKind.Utc),
                    EmploymentStatus = "PROBATION",
                    ProbationEndDate = new DateTime(2026, 8, 31, 0, 0, 0, DateTimeKind.Utc),
                    EmploymentType = "CONTRACT",
                    TaxExemptionCount = 0,
                    IsHighRiskRole = false,
                    BankName = "Arab Bank",
                    BankIban = "JO00ARAB0000000000033333333300",
                    ManagerEmployeeCode = "EMP-001",
                    Email = "lina.masri@thinkon.jo",
                    Phone = "+962795556677",
                    IsActive = true,
                    CreationUser = user,
                    CreationDate = now
                }
            };

            context.Employees.AddRange(employees);
            await context.SaveChangesAsync();
        }

        // 2. Employee Dependents
        if (!await context.EmployeeDependents.AnyAsync())
        {
            var dependents = new List<EmployeeDependent>
            {
                new() { EmployeeCode = "EMP-001", NameAr = "رانيا خليل", NameEn = "Rania Khalil", Relationship = "SPOUSE", NationalId = "9921020304", DateOfBirth = new DateTime(1992, 4, 10, 0, 0, 0, DateTimeKind.Utc), Gender = "FEMALE", IsTaxExemptionClaimed = true, IsActive = true, CreationUser = user, CreationDate = now },
                new() { EmployeeCode = "EMP-001", NameAr = "زيد أحمد الخالدي", NameEn = "Zaid Ahmad Al-Khalidi", Relationship = "CHILD", NationalId = "2181020304", DateOfBirth = new DateTime(2018, 9, 12, 0, 0, 0, DateTimeKind.Utc), Gender = "MALE", IsTaxExemptionClaimed = true, IsActive = true, CreationUser = user, CreationDate = now },
                new() { EmployeeCode = "EMP-002", NameAr = "مايا يوسف", NameEn = "Maya Yousef", Relationship = "CHILD", NationalId = "2201020304", DateOfBirth = new DateTime(2020, 1, 15, 0, 0, 0, DateTimeKind.Utc), Gender = "FEMALE", IsTaxExemptionClaimed = true, IsActive = true, CreationUser = user, CreationDate = now },
                new() { EmployeeCode = "EMP-004", NameAr = "يوسف عمر الزعبي", NameEn = "Yousef Omar Al-Zoubi", Relationship = "CHILD", NationalId = "2221020304", DateOfBirth = new DateTime(2022, 5, 20, 0, 0, 0, DateTimeKind.Utc), Gender = "MALE", IsTaxExemptionClaimed = true, IsActive = true, CreationUser = user, CreationDate = now }
            };

            context.EmployeeDependents.AddRange(dependents);
            await context.SaveChangesAsync();
        }

        // 3. Employee Documents
        if (!await context.EmployeeDocuments.AnyAsync())
        {
            var docs = new List<EmployeeDocument>
            {
                new() { EmployeeCode = "EMP-001", DocumentType = "NATIONAL_ID", FileName = "National Identity Card", DocumentNumber = "9901020304", IssuedDate = new DateTime(2020, 1, 1, 0, 0, 0, DateTimeKind.Utc), ExpiryDate = new DateTime(2030, 1, 1, 0, 0, 0, DateTimeKind.Utc), FileReference = "/uploads/hr/docs/emp001_id.pdf", IsActive = true, CreationUser = user, CreationDate = now },
                new() { EmployeeCode = "EMP-001", DocumentType = "CONTRACT", FileName = "Signed Employment Agreement", DocumentNumber = "CTR-2021-001", IssuedDate = new DateTime(2021, 3, 15, 0, 0, 0, DateTimeKind.Utc), FileReference = "/uploads/hr/docs/emp001_contract.pdf", IsActive = true, CreationUser = user, CreationDate = now },
                new() { EmployeeCode = "EMP-002", DocumentType = "NATIONAL_ID", FileName = "National Identity Card", DocumentNumber = "9881020304", IssuedDate = new DateTime(2019, 5, 1, 0, 0, 0, DateTimeKind.Utc), ExpiryDate = new DateTime(2029, 5, 1, 0, 0, 0, DateTimeKind.Utc), FileReference = "/uploads/hr/docs/emp002_id.pdf", IsActive = true, CreationUser = user, CreationDate = now }
            };

            context.EmployeeDocuments.AddRange(docs);
            await context.SaveChangesAsync();
        }

        // 4. Employment Events
        if (!await context.EmploymentEvents.AnyAsync())
        {
            var events = new List<EmploymentEvent>
            {
                new() { EmployeeCode = "EMP-001", EventType = "HIRE", EffectiveDate = new DateTime(2021, 3, 15, 0, 0, 0, DateTimeKind.Utc), Reason = "Initial Employment as Software Engineer", ApprovedBy = "system_seed", CreationDate = now },
                new() { EmployeeCode = "EMP-001", EventType = "PROMOTION", EffectiveDate = new DateTime(2024, 1, 1, 0, 0, 0, DateTimeKind.Utc), FromValue = "SWE", ToValue = "SR_SWE", Reason = "Annual performance promotion", ApprovedBy = "EMP-002", CreationDate = now },
                new() { EmployeeCode = "EMP-002", EventType = "HIRE", EffectiveDate = new DateTime(2019, 1, 10, 0, 0, 0, DateTimeKind.Utc), Reason = "Joined as HR Director", ApprovedBy = "system_seed", CreationDate = now }
            };

            context.EmploymentEvents.AddRange(events);
            await context.SaveChangesAsync();
        }

        // 5. Shift Assignments
        if (!await context.EmployeeShiftAssignments.AnyAsync())
        {
            var shiftAssignments = new List<EmployeeShiftAssignment>
            {
                new() { EmployeeCode = "EMP-001", ShiftCode = "GENERAL", EffectiveFrom = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc), IsActive = true, CreationUser = user, CreationDate = now },
                new() { EmployeeCode = "EMP-002", ShiftCode = "GENERAL", EffectiveFrom = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc), IsActive = true, CreationUser = user, CreationDate = now },
                new() { EmployeeCode = "EMP-003", ShiftCode = "GENERAL", EffectiveFrom = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc), IsActive = true, CreationUser = user, CreationDate = now },
                new() { EmployeeCode = "EMP-004", ShiftCode = "GENERAL", EffectiveFrom = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc), IsActive = true, CreationUser = user, CreationDate = now },
                new() { EmployeeCode = "EMP-005", ShiftCode = "GENERAL", EffectiveFrom = new DateTime(2026, 6, 1, 0, 0, 0, DateTimeKind.Utc), IsActive = true, CreationUser = user, CreationDate = now }
            };

            context.EmployeeShiftAssignments.AddRange(shiftAssignments);
            await context.SaveChangesAsync();
        }

        // 6. Attendance & Overtime Records
        if (!await context.AttendanceRecords.AnyAsync())
        {
            var today = DateTime.UtcNow.Date;
            var attendances = new List<AttendanceRecord>
            {
                new() { EmployeeCode = "EMP-001", AttendanceDate = today, ClockIn = today.AddHours(8).AddMinutes(25), ClockOut = today.AddHours(17).AddMinutes(5), LateMinutes = 0, EarlyLeaveMinutes = 0, TotalWorkHours = 8.5m, Status = "ON_TIME", CreationUser = user, CreationDate = now },
                new() { EmployeeCode = "EMP-002", AttendanceDate = today, ClockIn = today.AddHours(8).AddMinutes(30), ClockOut = today.AddHours(17).AddMinutes(0), LateMinutes = 0, EarlyLeaveMinutes = 0, TotalWorkHours = 8.5m, Status = "ON_TIME", CreationUser = user, CreationDate = now },
                new() { EmployeeCode = "EMP-003", AttendanceDate = today, ClockIn = today.AddHours(9).AddMinutes(15), ClockOut = today.AddHours(17).AddMinutes(15), LateMinutes = 30, EarlyLeaveMinutes = 0, TotalWorkHours = 8.0m, Status = "LATE", CreationUser = user, CreationDate = now },
                new() { EmployeeCode = "EMP-004", AttendanceDate = today, ClockIn = today.AddHours(8).AddMinutes(20), ClockOut = today.AddHours(17).AddMinutes(30), LateMinutes = 0, EarlyLeaveMinutes = 0, TotalWorkHours = 9.0m, Status = "ON_TIME", CreationUser = user, CreationDate = now }
            };

            context.AttendanceRecords.AddRange(attendances);
            await context.SaveChangesAsync();
        }

        if (!await context.OvertimeRecords.AnyAsync())
        {
            var today = DateTime.UtcNow.Date;
            var overtimes = new List<OvertimeRecord>
            {
                new() { EmployeeCode = "EMP-001", OvertimeDate = today.AddDays(-2), Hours = 2.5m, RateMultiplier = 1.25m, Status = "APPROVED", ApprovedBy = "EMP-002", ApprovalDate = now, Reason = "Critical production release deployment", CreationUser = "EMP-001", CreationDate = now },
                new() { EmployeeCode = "EMP-004", OvertimeDate = today.AddDays(-1), Hours = 4.0m, RateMultiplier = 1.50m, Status = "PENDING", Reason = "Emergency warehouse stock count", CreationUser = "EMP-004", CreationDate = now }
            };

            context.OvertimeRecords.AddRange(overtimes);
            await context.SaveChangesAsync();
        }

        // 7. Leave Balances & Leave Requests
        if (!await context.LeaveBalances.AnyAsync())
        {
            var balances = new List<LeaveBalance>
            {
                new() { EmployeeCode = "EMP-001", LeaveTypeCode = "ANNUAL", Year = 2026, CarriedForwardDays = 5m, AccruedDays = 9.33m, UsedDays = 3m, CreationUser = user, CreationDate = now },
                new() { EmployeeCode = "EMP-001", LeaveTypeCode = "SICK", Year = 2026, CarriedForwardDays = 0m, AccruedDays = 14m, UsedDays = 1m, CreationUser = user, CreationDate = now },
                new() { EmployeeCode = "EMP-002", LeaveTypeCode = "ANNUAL", Year = 2026, CarriedForwardDays = 7m, AccruedDays = 14m, UsedDays = 4m, CreationUser = user, CreationDate = now },
                new() { EmployeeCode = "EMP-003", LeaveTypeCode = "ANNUAL", Year = 2026, CarriedForwardDays = 2m, AccruedDays = 9.33m, UsedDays = 2m, CreationUser = user, CreationDate = now },
                new() { EmployeeCode = "EMP-004", LeaveTypeCode = "ANNUAL", Year = 2026, CarriedForwardDays = 0m, AccruedDays = 9.33m, UsedDays = 0m, CreationUser = user, CreationDate = now },
                new() { EmployeeCode = "EMP-005", LeaveTypeCode = "ANNUAL", Year = 2026, CarriedForwardDays = 0m, AccruedDays = 3.5m, UsedDays = 0m, CreationUser = user, CreationDate = now }
            };

            context.LeaveBalances.AddRange(balances);
            await context.SaveChangesAsync();
        }

        if (!await context.LeaveRequests.AnyAsync())
        {
            var today = DateTime.UtcNow.Date;
            var requests = new List<LeaveRequest>
            {
                new() { EmployeeCode = "EMP-001", LeaveTypeCode = "ANNUAL", StartDate = today.AddDays(5), EndDate = today.AddDays(7), DaysRequested = 3m, Status = "PENDING", Reason = "Personal Family Vacation", CreationUser = "EMP-001", CreationDate = now },
                new() { EmployeeCode = "EMP-003", LeaveTypeCode = "ANNUAL", StartDate = today.AddDays(-10), EndDate = today.AddDays(-9), DaysRequested = 2m, Status = "APPROVED", ApprovedBy = "EMP-002", ApprovalDate = now.AddDays(-11), Reason = "Renew personal documents", CreationUser = "EMP-003", CreationDate = now.AddDays(-12) }
            };

            context.LeaveRequests.AddRange(requests);
            await context.SaveChangesAsync();
        }

        // 8. Compensation & Salary Structures
        if (!await context.EmployeeSalaryStructures.AnyAsync())
        {
            var effDate = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc);

            var s1 = new EmployeeSalaryStructure { EmployeeCode = "EMP-001", EffectiveFrom = effDate, BasicSalary = 1600m, CurrencyCode = "JOD", PaymentMethod = "BANK_TRANSFER", IsActive = true, CreationUser = user, CreationDate = now };
            s1.Lines.Add(new EmployeeSalaryStructureLine { ComponentCode = "HOUSING", Amount = 300m, IsActive = true, CreationUser = user, CreationDate = now });
            s1.Lines.Add(new EmployeeSalaryStructureLine { ComponentCode = "TRANSPORT", Amount = 100m, IsActive = true, CreationUser = user, CreationDate = now });
            s1.Lines.Add(new EmployeeSalaryStructureLine { ComponentCode = "MOBILE", Amount = 50m, IsActive = true, CreationUser = user, CreationDate = now });

            var s2 = new EmployeeSalaryStructure { EmployeeCode = "EMP-002", EffectiveFrom = effDate, BasicSalary = 3000m, CurrencyCode = "JOD", PaymentMethod = "BANK_TRANSFER", IsActive = true, CreationUser = user, CreationDate = now };
            s2.Lines.Add(new EmployeeSalaryStructureLine { ComponentCode = "HOUSING", Amount = 500m, IsActive = true, CreationUser = user, CreationDate = now });
            s2.Lines.Add(new EmployeeSalaryStructureLine { ComponentCode = "TRANSPORT", Amount = 200m, IsActive = true, CreationUser = user, CreationDate = now });
            s2.Lines.Add(new EmployeeSalaryStructureLine { ComponentCode = "MOBILE", Amount = 100m, IsActive = true, CreationUser = user, CreationDate = now });

            var s3 = new EmployeeSalaryStructure { EmployeeCode = "EMP-003", EffectiveFrom = effDate, BasicSalary = 1150m, CurrencyCode = "JOD", PaymentMethod = "BANK_TRANSFER", IsActive = true, CreationUser = user, CreationDate = now };
            s3.Lines.Add(new EmployeeSalaryStructureLine { ComponentCode = "HOUSING", Amount = 200m, IsActive = true, CreationUser = user, CreationDate = now });
            s3.Lines.Add(new EmployeeSalaryStructureLine { ComponentCode = "TRANSPORT", Amount = 100m, IsActive = true, CreationUser = user, CreationDate = now });

            var s4 = new EmployeeSalaryStructure { EmployeeCode = "EMP-004", EffectiveFrom = effDate, BasicSalary = 750m, CurrencyCode = "JOD", PaymentMethod = "BANK_TRANSFER", IsActive = true, CreationUser = user, CreationDate = now };
            s4.Lines.Add(new EmployeeSalaryStructureLine { ComponentCode = "HOUSING", Amount = 120m, IsActive = true, CreationUser = user, CreationDate = now });
            s4.Lines.Add(new EmployeeSalaryStructureLine { ComponentCode = "TRANSPORT", Amount = 80m, IsActive = true, CreationUser = user, CreationDate = now });

            var s5 = new EmployeeSalaryStructure { EmployeeCode = "EMP-005", EffectiveFrom = new DateTime(2026, 6, 1, 0, 0, 0, DateTimeKind.Utc), BasicSalary = 500m, CurrencyCode = "JOD", PaymentMethod = "BANK_TRANSFER", IsActive = true, CreationUser = user, CreationDate = now };
            s5.Lines.Add(new EmployeeSalaryStructureLine { ComponentCode = "TRANSPORT", Amount = 100m, IsActive = true, CreationUser = user, CreationDate = now });

            context.EmployeeSalaryStructures.AddRange(s1, s2, s3, s4, s5);
            await context.SaveChangesAsync();
        }

        // 9. Employment Contracts
        if (!await context.EmploymentContracts.AnyAsync())
        {
            var contracts = new List<EmploymentContract>
            {
                new() { EmployeeCode = "EMP-001", ContractType = "UNLIMITED", StartDate = new DateTime(2021, 3, 15, 0, 0, 0, DateTimeKind.Utc), Status = "ACTIVE", IsActive = true, CreationUser = user, CreationDate = now },
                new() { EmployeeCode = "EMP-002", ContractType = "UNLIMITED", StartDate = new DateTime(2019, 1, 10, 0, 0, 0, DateTimeKind.Utc), Status = "ACTIVE", IsActive = true, CreationUser = user, CreationDate = now },
                new() { EmployeeCode = "EMP-003", ContractType = "UNLIMITED", StartDate = new DateTime(2022, 6, 1, 0, 0, 0, DateTimeKind.Utc), Status = "ACTIVE", IsActive = true, CreationUser = user, CreationDate = now },
                new() { EmployeeCode = "EMP-004", ContractType = "UNLIMITED", StartDate = new DateTime(2023, 11, 1, 0, 0, 0, DateTimeKind.Utc), Status = "ACTIVE", IsActive = true, CreationUser = user, CreationDate = now },
                new() { EmployeeCode = "EMP-005", ContractType = "LIMITED", StartDate = new DateTime(2026, 6, 1, 0, 0, 0, DateTimeKind.Utc), EndDate = new DateTime(2027, 5, 31, 0, 0, 0, DateTimeKind.Utc), Status = "ACTIVE", IsActive = true, CreationUser = user, CreationDate = now }
            };

            context.EmploymentContracts.AddRange(contracts);
            await context.SaveChangesAsync();
        }

        // 10. Recruitment & ATS Pipeline
        if (!await context.JobRequisitions.AnyAsync())
        {
            var req1 = new JobRequisition
            {
                RequisitionCode = "REQ-2026-001",
                PositionCode = "SR_SWE",
                DepartmentCode = "ENG",
                Headcount = 2,
                Status = "OPEN",
                TargetHireDate = new DateTime(2026, 10, 1, 0, 0, 0, DateTimeKind.Utc),
                RequestedBy = "EMP-002",
                JobDescription = "Seeking senior full-stack .NET & Angular engineers with ERP domain experience",
                CreationUser = user,
                CreationDate = now
            };

            var req2 = new JobRequisition
            {
                RequisitionCode = "REQ-2026-002",
                PositionCode = "FIN_ACC",
                DepartmentCode = "FIN",
                Headcount = 1,
                Status = "OPEN",
                TargetHireDate = new DateTime(2026, 9, 15, 0, 0, 0, DateTimeKind.Utc),
                RequestedBy = "EMP-002",
                JobDescription = "Senior general accountant with IFRS compliance background",
                CreationUser = user,
                CreationDate = now
            };

            context.JobRequisitions.AddRange(req1, req2);
            await context.SaveChangesAsync();

            var cand1 = new Candidate
            {
                CandidateCode = "CAND-2026-001",
                NameAr = "رامي خوري",
                NameEn = "Rami Khoury",
                Email = "rami.khoury@gmail.com",
                Phone = "+962788889900",
                Source = "LINKEDIN",
                CreationUser = user,
                CreationDate = now
            };

            var cand2 = new Candidate
            {
                CandidateCode = "CAND-2026-002",
                NameAr = "نور حداد",
                NameEn = "Nour Haddad",
                Email = "nour.haddad@yahoo.com",
                Phone = "+962777778899",
                Source = "REFERRAL",
                CreationUser = user,
                CreationDate = now
            };

            context.Candidates.AddRange(cand1, cand2);
            await context.SaveChangesAsync();

            var app1 = new CandidateApplication
            {
                CandidateCode = "CAND-2026-001",
                RequisitionCode = "REQ-2026-001",
                Stage = "INTERVIEW",
                InterviewDate = now.AddDays(2),
                CreationUser = user,
                CreationDate = now
            };

            var app2 = new CandidateApplication
            {
                CandidateCode = "CAND-2026-002",
                RequisitionCode = "REQ-2026-002",
                Stage = "SCREENING",
                CreationUser = user,
                CreationDate = now
            };

            context.CandidateApplications.AddRange(app1, app2);
            await context.SaveChangesAsync();

            var tasks = new List<OnboardingTask>
            {
                new() { EmployeeCode = "EMP-005", TaskName = "Issue Employee ID Smart Badge", AssignedTo = "EMP-002", DueDate = now.AddDays(3), IsCompleted = true, CompletedDate = now.AddDays(-1), CompletedBy = "EMP-002", CreationUser = user, CreationDate = now },
                new() { EmployeeCode = "EMP-005", TaskName = "Provision Development Laptop & GitHub Access", AssignedTo = "EMP-001", DueDate = now.AddDays(2), IsCompleted = true, CompletedDate = now.AddDays(-2), CompletedBy = "EMP-001", CreationUser = user, CreationDate = now },
                new() { EmployeeCode = "EMP-005", TaskName = "Submit SSC Form 1 Registration to Social Security", AssignedTo = "EMP-002", DueDate = now.AddDays(5), IsCompleted = false, CreationUser = user, CreationDate = now }
            };

            context.OnboardingTasks.AddRange(tasks);
            await context.SaveChangesAsync();
        }

        // 11. Historical Payroll Runs
        if (!await context.PayrollRuns.AnyAsync())
        {
            var run = new PayrollRun
            {
                PayPeriod = "2026-07",
                RunDate = new DateTime(2026, 7, 28, 0, 0, 0, DateTimeKind.Utc),
                Status = "APPROVED",
                TotalGrossSalary = 8850.00m,
                TotalNetSalary = 7422.25m,
                TotalEmployeeSsc = 663.75m,
                TotalEmployerSsc = 1261.13m,
                TotalIncomeTax = 764.00m,
                ApprovedBy = "EMP-002",
                ApprovalDate = new DateTime(2026, 7, 28, 0, 0, 0, DateTimeKind.Utc),
                JournalVoucherId = 1001,
                CreationUser = user,
                CreationDate = now
            };

            var line1 = new PayrollRunLine
            {
                EmployeeCode = "EMP-001",
                BasicSalary = 1600.00m,
                TotalEarnings = 450.00m,
                GrossSalary = 2050.00m,
                SscEligibleSalary = 2000.00m,
                SscEmployeeContribution = 150.00m,
                SscEmployerContribution = 285.00m,
                TaxableGross = 1900.00m,
                AnnualExemptions = 11000.00m,
                AnnualTaxableNet = 11800.00m,
                IncomeTaxWithheld = 104.17m,
                TotalDeductions = 254.17m,
                NetPay = 1795.83m,
                Status = "APPROVED",
                CreationUser = user,
                CreationDate = now
            };
            line1.Components.Add(new PayrollRunLineComponent { ComponentCode = "BASIC", ComponentNameEn = "Basic Salary", ComponentNameAr = "الراتب الأساسي", ComponentType = "EARNING", Amount = 1600m });
            line1.Components.Add(new PayrollRunLineComponent { ComponentCode = "HOUSING", ComponentNameEn = "Housing Allowance", ComponentNameAr = "بدل سكن", ComponentType = "EARNING", Amount = 300m });
            line1.Components.Add(new PayrollRunLineComponent { ComponentCode = "TRANSPORT", ComponentNameEn = "Transportation Allowance", ComponentNameAr = "بدل مواصلات", ComponentType = "EARNING", Amount = 100m });
            line1.Components.Add(new PayrollRunLineComponent { ComponentCode = "MOBILE", ComponentNameEn = "Mobile Allowance", ComponentNameAr = "بدل هاتف", ComponentType = "EARNING", Amount = 50m });
            line1.Components.Add(new PayrollRunLineComponent { ComponentCode = "SSC_EMPLOYEE", ComponentNameEn = "SSC Employee Contribution", ComponentNameAr = "اقتطاع الضمان (الموظف)", ComponentType = "DEDUCTION", Amount = 150m });
            line1.Components.Add(new PayrollRunLineComponent { ComponentCode = "INCOME_TAX", ComponentNameEn = "Income Tax Withholding", ComponentNameAr = "ضريبة الدخل المستقطعة", ComponentType = "DEDUCTION", Amount = 104.17m });

            var line2 = new PayrollRunLine
            {
                EmployeeCode = "EMP-002",
                BasicSalary = 3000.00m,
                TotalEarnings = 800.00m,
                GrossSalary = 3800.00m,
                SscEligibleSalary = 3349.00m, // Capped at 3,349 JOD ceiling
                SscEmployeeContribution = 251.18m,
                SscEmployerContribution = 477.23m,
                TaxableGross = 3548.82m,
                AnnualExemptions = 10000.00m,
                AnnualTaxableNet = 32585.84m,
                IncomeTaxWithheld = 432.50m,
                TotalDeductions = 683.68m,
                NetPay = 3116.32m,
                Status = "APPROVED",
                CreationUser = user,
                CreationDate = now
            };
            line2.Components.Add(new PayrollRunLineComponent { ComponentCode = "BASIC", ComponentNameEn = "Basic Salary", ComponentNameAr = "الراتب الأساسي", ComponentType = "EARNING", Amount = 3000m });
            line2.Components.Add(new PayrollRunLineComponent { ComponentCode = "HOUSING", ComponentNameEn = "Housing Allowance", ComponentNameAr = "بدل سكن", ComponentType = "EARNING", Amount = 500m });
            line2.Components.Add(new PayrollRunLineComponent { ComponentCode = "TRANSPORT", ComponentNameEn = "Transportation Allowance", ComponentNameAr = "بدل مواصلات", ComponentType = "EARNING", Amount = 200m });
            line2.Components.Add(new PayrollRunLineComponent { ComponentCode = "MOBILE", ComponentNameEn = "Mobile Allowance", ComponentNameAr = "بدل هاتف", ComponentType = "EARNING", Amount = 100m });
            line2.Components.Add(new PayrollRunLineComponent { ComponentCode = "SSC_EMPLOYEE", ComponentNameEn = "SSC Employee Contribution", ComponentNameAr = "اقتطاع الضمان (الموظف)", ComponentType = "DEDUCTION", Amount = 251.18m });
            line2.Components.Add(new PayrollRunLineComponent { ComponentCode = "INCOME_TAX", ComponentNameEn = "Income Tax Withholding", ComponentNameAr = "ضريبة الدخل المستقطعة", ComponentType = "DEDUCTION", Amount = 432.50m });

            run.Lines.Add(line1);
            run.Lines.Add(line2);

            context.PayrollRuns.Add(run);
            await context.SaveChangesAsync();
        }

        // 12. End of Service Provisions
        if (!await context.EndOfServiceProvisionAccruals.AnyAsync())
        {
            var eosAccruals = new List<EndOfServiceProvisionAccrual>
            {
                new() { PayPeriod = "2026-07", EmployeeCode = "EMP-001", ServiceYears = 5.38m, BasicSalary = 1600m, MonthlyAccrualAmount = 133.33m, TotalAccumulatedProvision = 8608.00m, JournalVoucherId = 1002, CreationUser = user, CreationDate = now },
                new() { PayPeriod = "2026-07", EmployeeCode = "EMP-002", ServiceYears = 7.55m, BasicSalary = 3000m, MonthlyAccrualAmount = 250.00m, TotalAccumulatedProvision = 22650.00m, JournalVoucherId = 1002, CreationUser = user, CreationDate = now },
                new() { PayPeriod = "2026-07", EmployeeCode = "EMP-003", ServiceYears = 4.16m, BasicSalary = 1150m, MonthlyAccrualAmount = 95.83m, TotalAccumulatedProvision = 4784.00m, JournalVoucherId = 1002, CreationUser = user, CreationDate = now }
            };

            context.EndOfServiceProvisionAccruals.AddRange(eosAccruals);
            await context.SaveChangesAsync();
        }

        // 13. Expense Claims & Lines
        if (!await context.ExpenseClaims.AnyAsync())
        {
            var claim1 = new ExpenseClaim
            {
                ClaimNumber = "EXP-20260801-101",
                EmployeeCode = "EMP-001",
                ClaimDate = now.AddDays(-15),
                TotalAmount = 245.50m,
                CurrencyCode = "JOD",
                ReimbursementMethod = "NEXT_PAYROLL_RUN",
                Status = "APPROVED",
                ApprovedBy = "EMP-002",
                ApprovalDate = now.AddDays(-14),
                Description = "Aqaba client on-site cloud deployment and transport",
                CreationUser = "EMP-001",
                CreationDate = now.AddDays(-15)
            };
            claim1.Lines.Add(new ExpenseClaimLine { ExpenseDate = now.AddDays(-16), Category = "TRAVEL", Description = "Amman - Aqaba Flight & Transport", Amount = 120.00m });
            claim1.Lines.Add(new ExpenseClaimLine { ExpenseDate = now.AddDays(-16), Category = "HOTEL", Description = "Hotel accommodation 1 night", Amount = 80.00m });
            claim1.Lines.Add(new ExpenseClaimLine { ExpenseDate = now.AddDays(-15), Category = "MEALS", Description = "Client technical dinner", Amount = 45.50m });

            var claim2 = new ExpenseClaim
            {
                ClaimNumber = "EXP-20260820-202",
                EmployeeCode = "EMP-003",
                ClaimDate = now.AddDays(-2),
                TotalAmount = 65.00m,
                CurrencyCode = "JOD",
                ReimbursementMethod = "DIRECT_BANK_TRANSFER",
                Status = "SUBMITTED",
                Description = "Quarterly audit office supplies & courier expenses",
                CreationUser = "EMP-003",
                CreationDate = now.AddDays(-2)
            };
            claim2.Lines.Add(new ExpenseClaimLine { ExpenseDate = now.AddDays(-3), Category = "OFFICE_SUPPLIES", Description = "Tax filing binder materials", Amount = 35.00m });
            claim2.Lines.Add(new ExpenseClaimLine { ExpenseDate = now.AddDays(-2), Category = "COURIER", Description = "Express courier for bank audit confirmations", Amount = 30.00m });

            context.ExpenseClaims.AddRange(claim1, claim2);
            await context.SaveChangesAsync();
        }

        // 14. Asset Assignments
        if (!await context.AssetAssignments.AnyAsync())
        {
            var assets = new List<AssetAssignment>
            {
                new()
                {
                    EmployeeCode = "EMP-001",
                    AssetTag = "LAPTOP-2026-001",
                    AssetDescription = "ThinkPad P16 Gen 2 (i9, 64GB RAM, 2TB SSD)",
                    Category = "LAPTOP",
                    SerialNumber = "PF49A012",
                    IssuedDate = new DateTime(2021, 3, 15, 0, 0, 0, DateTimeKind.Utc),
                    Status = "ASSIGNED",
                    IssuedCondition = "BRAND_NEW",
                    Notes = "Assigned for primary software development",
                    CreationUser = user,
                    CreationDate = now
                },
                new()
                {
                    EmployeeCode = "EMP-001",
                    AssetTag = "MONITOR-4K-01",
                    AssetDescription = "Dell UltraSharp 32-inch 4K USB-C Monitor",
                    Category = "MONITOR",
                    SerialNumber = "CN09B334",
                    IssuedDate = new DateTime(2021, 3, 15, 0, 0, 0, DateTimeKind.Utc),
                    Status = "ASSIGNED",
                    IssuedCondition = "BRAND_NEW",
                    CreationUser = user,
                    CreationDate = now
                },
                new()
                {
                    EmployeeCode = "EMP-002",
                    AssetTag = "LAPTOP-2026-002",
                    AssetDescription = "MacBook Pro 16-inch M3 Pro (36GB RAM)",
                    Category = "LAPTOP",
                    SerialNumber = "C02G89A2",
                    IssuedDate = new DateTime(2024, 1, 10, 0, 0, 0, DateTimeKind.Utc),
                    Status = "ASSIGNED",
                    IssuedCondition = "BRAND_NEW",
                    CreationUser = user,
                    CreationDate = now
                },
                new()
                {
                    EmployeeCode = "EMP-005",
                    AssetTag = "LAPTOP-2026-005",
                    AssetDescription = "ThinkPad T14s Gen 4 (i7, 32GB RAM)",
                    Category = "LAPTOP",
                    SerialNumber = "PF52B771",
                    IssuedDate = new DateTime(2026, 6, 1, 0, 0, 0, DateTimeKind.Utc),
                    Status = "ASSIGNED",
                    IssuedCondition = "EXCELLENT",
                    CreationUser = user,
                    CreationDate = now
                }
            };

            context.AssetAssignments.AddRange(assets);
            await context.SaveChangesAsync();
        }
    }
}
