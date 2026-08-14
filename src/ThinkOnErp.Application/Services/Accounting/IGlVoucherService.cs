using ThinkOnErp.Application.DTOs.Accounting.Vouchers;

namespace ThinkOnErp.Application.Services.Accounting;

public interface IGlVoucherService
{
    Task<IReadOnlyList<GlVoucherTypeDto>> GetVoucherTypesAsync(CancellationToken cancellationToken = default);
    Task<GlVoucherTypeDto?> GetVoucherTypeByCodeAsync(int typeCode, CancellationToken cancellationToken = default);

    Task<PagedResultDto<GlVoucherHeaderDto>> GetPagedVouchersAsync(
        GlVoucherFilterDto filter,
        CancellationToken cancellationToken = default);

    Task<GlVoucherHeaderDto?> GetByIdAsync(long id, CancellationToken cancellationToken = default);

    Task<GlVoucherHeaderDto> CreateVoucherAsync(
        CreateGlVoucherDto dto,
        string username,
        CancellationToken cancellationToken = default);

    Task<GlVoucherHeaderDto> ReviewVoucherAsync(
        long id,
        string username,
        CancellationToken cancellationToken = default);

    Task<GlVoucherHeaderDto> PostVoucherAsync(
        long id,
        string username,
        CancellationToken cancellationToken = default);

    Task<GlVoucherHeaderDto> ReverseVoucherAsync(
        long id,
        string username,
        string? reversalReason,
        CancellationToken cancellationToken = default);
}
