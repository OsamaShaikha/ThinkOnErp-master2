using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using ThinkOnErp.Application.DTOs.Hr;
using ThinkOnErp.Domain.Entities.Hr;
using ThinkOnErp.Domain.Exceptions;
using ThinkOnErp.Domain.Interfaces.Hr;

namespace ThinkOnErp.Application.Services.Hr;

public sealed class ExpenseClaimService : IExpenseClaimService
{
    private readonly IExpenseClaimRepository _expenseRepository;
    private readonly IEmployeeRepository _employeeRepository;
    private readonly ILogger<ExpenseClaimService> _logger;

    public ExpenseClaimService(
        IExpenseClaimRepository expenseRepository,
        IEmployeeRepository employeeRepository,
        ILogger<ExpenseClaimService> logger)
    {
        _expenseRepository = expenseRepository ?? throw new ArgumentNullException(nameof(expenseRepository));
        _employeeRepository = employeeRepository ?? throw new ArgumentNullException(nameof(employeeRepository));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task<List<ExpenseClaimDto>> GetAllClaimsAsync(string? employeeCode = null, string? status = null, DateTime? fromDate = null, DateTime? toDate = null)
    {
        var list = await _expenseRepository.GetAllClaimsAsync(employeeCode, status, fromDate, toDate);
        return list.Select(MapToClaimDto).ToList();
    }

    public async Task<ExpenseClaimDto?> GetClaimByIdAsync(long id)
    {
        var claim = await _expenseRepository.GetClaimByIdAsync(id, includeLines: true);
        return claim == null ? null : MapToClaimDto(claim);
    }

    public async Task<ExpenseClaimDto> SubmitClaimAsync(SubmitExpenseClaimDto dto, string currentUser)
    {
        ArgumentNullException.ThrowIfNull(dto);
        var empCode = dto.EmployeeCode.Trim().ToUpperInvariant();

        var emp = await _employeeRepository.GetByCodeAsync(empCode);
        if (emp == null)
        {
            throw new HrNotFoundException($"الموظف ({empCode}) غير موجود.", "EMPLOYEE_NOT_FOUND");
        }

        if (dto.Lines == null || dto.Lines.Count == 0)
        {
            throw new HrValidationException("يجب إضافة بند واحد على الأقل في مطالبة المصروفات.", "NO_EXPENSE_LINES");
        }

        var total = dto.Lines.Sum(l => l.Amount);
        var claimNumber = $"EXP-{DateTime.UtcNow:yyyyMMddHHmmss}-{new Random().Next(100, 999)}";

        var claim = new ExpenseClaim
        {
            EmployeeCode = empCode,
            ClaimNumber = claimNumber,
            ClaimDate = DateTime.UtcNow,
            TotalAmount = total,
            CurrencyCode = "JOD",
            ReimbursementMethod = string.IsNullOrWhiteSpace(dto.ReimbursementMethod) ? "NEXT_PAYROLL_RUN" : dto.ReimbursementMethod.Trim().ToUpperInvariant(),
            Status = "SUBMITTED",
            Description = dto.Description?.Trim(),
            CreationUser = string.IsNullOrWhiteSpace(currentUser) ? "SYSTEM" : currentUser,
            CreationDate = DateTime.UtcNow
        };

        foreach (var l in dto.Lines)
        {
            claim.Lines.Add(new ExpenseClaimLine
            {
                ExpenseDate = l.ExpenseDate.Date,
                Category = string.IsNullOrWhiteSpace(l.Category) ? "TRAVEL" : l.Category.Trim().ToUpperInvariant(),
                Description = l.Description.Trim(),
                Amount = l.Amount,
                ReceiptFileReference = l.ReceiptFileReference?.Trim(),
                ExpenseGlAccountCode = l.ExpenseGlAccountCode?.Trim()
            });
        }

        await _expenseRepository.AddClaimAsync(claim);
        await _expenseRepository.SaveChangesAsync();

        _logger.LogInformation("Submitted expense claim {ClaimNo} for {Emp} ({Total} JOD)", claimNumber, empCode, total);
        return (await GetClaimByIdAsync(claim.Id))!;
    }

    public async Task<ExpenseClaimDto> ApproveClaimAsync(long id, string approvedBy)
    {
        var claim = await _expenseRepository.GetClaimByIdAsync(id, includeLines: true);
        if (claim == null)
        {
            throw new HrNotFoundException($"مطالبة المصروفات رقم ({id}) غير موجودة.", "CLAIM_NOT_FOUND");
        }

        if (claim.Status != "SUBMITTED")
        {
            throw new HrValidationException($"لا يمكن اعتماد مطالبة بحالة ({claim.Status}).", "INVALID_STATUS");
        }

        claim.Status = "APPROVED";
        claim.ApprovedBy = string.IsNullOrWhiteSpace(approvedBy) ? "SYSTEM" : approvedBy;
        claim.ApprovalDate = DateTime.UtcNow;
        claim.UpdateUser = claim.ApprovedBy;
        claim.UpdateDate = DateTime.UtcNow;

        _expenseRepository.UpdateClaim(claim);
        await _expenseRepository.SaveChangesAsync();

        _logger.LogInformation("Approved expense claim #{Id} by {User}", id, approvedBy);
        return MapToClaimDto(claim);
    }

    public async Task<ExpenseClaimDto> RejectClaimAsync(long id, string rejectionReason, string rejectedBy)
    {
        var claim = await _expenseRepository.GetClaimByIdAsync(id, includeLines: true);
        if (claim == null)
        {
            throw new HrNotFoundException($"مطالبة المصروفات رقم ({id}) غير موجودة.", "CLAIM_NOT_FOUND");
        }

        if (claim.Status != "SUBMITTED")
        {
            throw new HrValidationException($"لا يمكن رفض مطالبة بحالة ({claim.Status}).", "INVALID_STATUS");
        }

        claim.Status = "REJECTED";
        claim.ApprovedBy = string.IsNullOrWhiteSpace(rejectedBy) ? "SYSTEM" : rejectedBy;
        claim.ApprovalDate = DateTime.UtcNow;
        claim.RejectionReason = rejectionReason;
        claim.UpdateUser = claim.ApprovedBy;
        claim.UpdateDate = DateTime.UtcNow;

        _expenseRepository.UpdateClaim(claim);
        await _expenseRepository.SaveChangesAsync();

        _logger.LogInformation("Rejected expense claim #{Id} by {User}", id, rejectedBy);
        return MapToClaimDto(claim);
    }

    private static ExpenseClaimDto MapToClaimDto(ExpenseClaim c)
    {
        return new ExpenseClaimDto
        {
            Id = c.Id,
            EmployeeCode = c.EmployeeCode,
            EmployeeNameEn = c.Employee?.NameEn ?? string.Empty,
            ClaimNumber = c.ClaimNumber,
            ClaimDate = c.ClaimDate,
            TotalAmount = c.TotalAmount,
            CurrencyCode = c.CurrencyCode,
            ReimbursementMethod = c.ReimbursementMethod,
            Status = c.Status,
            ApprovedBy = c.ApprovedBy,
            ApprovalDate = c.ApprovalDate,
            RejectionReason = c.RejectionReason,
            ReimbursedInPayPeriod = c.ReimbursedInPayPeriod,
            Description = c.Description,
            Lines = c.Lines.Select(l => new ExpenseClaimLineDto
            {
                Id = l.Id,
                ExpenseDate = l.ExpenseDate,
                Category = l.Category,
                Description = l.Description,
                Amount = l.Amount,
                ReceiptFileReference = l.ReceiptFileReference,
                ExpenseGlAccountCode = l.ExpenseGlAccountCode
            }).ToList()
        };
    }
}
