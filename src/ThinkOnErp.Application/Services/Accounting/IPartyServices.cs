using ThinkOnErp.Application.DTOs.Accounting.Parties;

namespace ThinkOnErp.Application.Services.Accounting;

public interface ICustomerService
{
    Task<IReadOnlyList<CustomerDto>> GetAllAsync(PartyFilterDto filter, CancellationToken cancellationToken = default);
    Task<CustomerDto> GetByCodeAsync(string code, CancellationToken cancellationToken = default);
    Task<CustomerDto> CreateAsync(CreateCustomerDto dto, string username, CancellationToken cancellationToken = default);
    Task<CustomerDto> UpdateAsync(string code, UpdateCustomerDto dto, string username, CancellationToken cancellationToken = default);
    Task<bool> SetStatusAsync(string code, bool isActive, string username, CancellationToken cancellationToken = default);
}

public interface IVendorService
{
    Task<IReadOnlyList<VendorDto>> GetAllAsync(PartyFilterDto filter, CancellationToken cancellationToken = default);
    Task<VendorDto> GetByCodeAsync(string code, CancellationToken cancellationToken = default);
    Task<VendorDto> CreateAsync(CreateVendorDto dto, string username, CancellationToken cancellationToken = default);
    Task<VendorDto> UpdateAsync(string code, UpdateVendorDto dto, string username, CancellationToken cancellationToken = default);
    Task<bool> SetStatusAsync(string code, bool isActive, string username, CancellationToken cancellationToken = default);
}
