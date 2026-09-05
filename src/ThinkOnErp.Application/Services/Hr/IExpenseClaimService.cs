using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using ThinkOnErp.Application.DTOs.Hr;

namespace ThinkOnErp.Application.Services.Hr;

public interface IExpenseClaimService
{
    Task<List<ExpenseClaimDto>> GetAllClaimsAsync(string? employeeCode = null, string? status = null, DateTime? fromDate = null, DateTime? toDate = null);
    Task<ExpenseClaimDto?> GetClaimByIdAsync(long id);
    Task<ExpenseClaimDto> SubmitClaimAsync(SubmitExpenseClaimDto dto, string currentUser);
    Task<ExpenseClaimDto> ApproveClaimAsync(long id, string approvedBy);
    Task<ExpenseClaimDto> RejectClaimAsync(long id, string rejectionReason, string rejectedBy);
}
