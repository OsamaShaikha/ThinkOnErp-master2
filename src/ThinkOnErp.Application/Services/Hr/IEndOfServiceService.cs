using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using ThinkOnErp.Application.DTOs.Hr;

namespace ThinkOnErp.Application.Services.Hr;

public interface IEndOfServiceService
{
    // Monthly Provisions
    Task<List<EndOfServiceProvisionDto>> GetEmployeeProvisionsAsync(string employeeCode);
    Task<RunProvisionAccrualResultDto> RunMonthlyProvisionAccrualAsync(string payPeriod, string currentUser);

    // Final Settlements
    Task<List<FinalSettlementDto>> GetAllSettlementsAsync(string? status = null);
    Task<FinalSettlementDto?> GetSettlementByIdAsync(long id);
    Task<FinalSettlementDto?> GetSettlementByEmployeeAsync(string employeeCode);
    Task<FinalSettlementDto> CalculateFinalSettlementAsync(CalculateFinalSettlementDto dto, string currentUser);
    Task<FinalSettlementDto> ApproveFinalSettlementAsync(long id, string approvedBy);
    Task<FinalSettlementDto> PostFinalSettlementToGlAsync(long id, string currentUser);
}
