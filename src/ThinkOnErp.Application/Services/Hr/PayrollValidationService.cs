using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using ThinkOnErp.Application.DTOs.Hr;
using ThinkOnErp.Domain.Exceptions;
using ThinkOnErp.Domain.Interfaces.Hr;

namespace ThinkOnErp.Application.Services.Hr;

public sealed class PayrollValidationService : IPayrollValidationService
{
    private readonly IPayrollRepository _payrollRepo;
    private readonly IEmployeeRepository _employeeRepo;
    private readonly ICompensationRepository _compensationRepo;
    private readonly ILogger<PayrollValidationService> _logger;

    public PayrollValidationService(
        IPayrollRepository payrollRepo,
        IEmployeeRepository employeeRepo,
        ICompensationRepository compensationRepo,
        ILogger<PayrollValidationService> logger)
    {
        _payrollRepo = payrollRepo ?? throw new ArgumentNullException(nameof(payrollRepo));
        _employeeRepo = employeeRepo ?? throw new ArgumentNullException(nameof(employeeRepo));
        _compensationRepo = compensationRepo ?? throw new ArgumentNullException(nameof(compensationRepo));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task<PayrollValidationResultDto> ValidatePayrollRunAsync(long payrollRunId)
    {
        var run = await _payrollRepo.GetRunByIdAsync(payrollRunId);
        if (run == null)
        {
            throw new HrNotFoundException($"مسير الرواتب رقم ({payrollRunId}) غير موجود.", "RUN_NOT_FOUND");
        }

        var result = new PayrollValidationResultDto
        {
            TotalEmployees = run.Lines.Count
        };

        if (run.Lines.Count == 0)
        {
            result.Errors.Add(new PayrollValidationMessageDto
            {
                Code = "EMPTY_PAYROLL_RUN",
                Message = "مسير الرواتب لا يحتوي على أي موظفين محسوبين.",
                Severity = "ERROR"
            });
            result.IsValid = false;
            return result;
        }

        foreach (var line in run.Lines)
        {
            var emp = await _employeeRepo.GetByCodeAsync(line.EmployeeCode);

            // 1. Negative Net Pay Check
            if (line.NetPay < 0)
            {
                result.Errors.Add(new PayrollValidationMessageDto
                {
                    EmployeeCode = line.EmployeeCode,
                    Code = "NEGATIVE_NET_PAY",
                    Message = $"صافي الراتب للموظف ({line.EmployeeCode}) سالب ({line.NetPay:F3} د.أ).",
                    Severity = "ERROR"
                });
            }

            // 2. Missing Bank details for Bank transfers
            if (line.PaymentMethod == "BANK" && string.IsNullOrWhiteSpace(line.Iban))
            {
                result.Errors.Add(new PayrollValidationMessageDto
                {
                    EmployeeCode = line.EmployeeCode,
                    Code = "MISSING_IBAN",
                    Message = $"طريقة الدفع بنكية ولكن رقم الآيبان (IBAN) مفقود للموظف ({line.EmployeeCode}).",
                    Severity = "ERROR"
                });
            }

            // 3. Check for 0 Basic Salary
            if (line.BasicSalary <= 0)
            {
                result.Warnings.Add(new PayrollValidationMessageDto
                {
                    EmployeeCode = line.EmployeeCode,
                    Code = "ZERO_BASIC_SALARY",
                    Message = $"الراتب الأساسي للموظف ({line.EmployeeCode}) يساوي صفر.",
                    Severity = "WARNING"
                });
            }

            // 4. Missing GL Cost Center mapping
            if (string.IsNullOrWhiteSpace(line.CostCenterCode))
            {
                result.Warnings.Add(new PayrollValidationMessageDto
                {
                    EmployeeCode = line.EmployeeCode,
                    Code = "MISSING_COST_CENTER",
                    Message = $"مركز التكلفة (Cost Center) غير محدد للموظف ({line.EmployeeCode}).",
                    Severity = "WARNING"
                });
            }

            // 5. Unmapped GL Account on components
            var unmappedComponents = line.Components.Where(c => string.IsNullOrWhiteSpace(c.GlAccountCode) && c.ComponentCode != "BASIC" && c.ComponentCode != "SSC_EMPLOYEE" && c.ComponentCode != "INCOME_TAX" && c.ComponentCode != "OVERTIME" && c.ComponentCode != "NATIONAL_CONTRIB").ToList();
            if (unmappedComponents.Count > 0)
            {
                foreach (var uc in unmappedComponents)
                {
                    result.Warnings.Add(new PayrollValidationMessageDto
                    {
                        EmployeeCode = line.EmployeeCode,
                        Code = "UNMAPPED_GL_ACCOUNT",
                        Message = $"بند الراتب ({uc.ComponentCode}) لا يحتوي على حساب أستاذ عام (GL Account) محدد.",
                        Severity = "WARNING"
                    });
                }
            }
        }

        result.IsValid = result.Errors.Count == 0;
        return result;
    }
}
