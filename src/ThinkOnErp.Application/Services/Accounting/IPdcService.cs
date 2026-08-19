using ThinkOnErp.Application.DTOs.Accounting.Pdc;

namespace ThinkOnErp.Application.Services.Accounting;

public interface IPdcService
{
    Task<PdcRegisterDto> CreatePdcAsync(CreatePdcDto dto, string username, CancellationToken cancellationToken = default);
    Task<PdcRegisterDto> GetByIdAsync(long id, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<PdcRegisterDto>> GetChequesAsync(PdcFilterDto filter, CancellationToken cancellationToken = default);
    Task<PdcRegisterDto> DepositChequeAsync(long id, DepositPdcDto dto, string username, CancellationToken cancellationToken = default);
    Task<PdcRegisterDto> ClearChequeAsync(long id, ClearPdcDto dto, string username, CancellationToken cancellationToken = default);
    Task<PdcRegisterDto> BounceChequeAsync(long id, BouncePdcDto dto, string username, CancellationToken cancellationToken = default);
    Task<PdcRegisterDto> CancelChequeAsync(long id, string username, string? reason, CancellationToken cancellationToken = default);
    Task<UpcomingMaturitySummaryDto> GetUpcomingMaturitiesAsync(long? branchId, int daysAhead, CancellationToken cancellationToken = default);
}
