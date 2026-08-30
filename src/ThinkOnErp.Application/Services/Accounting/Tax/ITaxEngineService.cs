using ThinkOnErp.Application.DTOs.Accounting.Tax;

namespace ThinkOnErp.Application.Services.Accounting.Tax;

public interface ITaxEngineService
{
    // Tax Calculation Engine
    Task<TaxCalculationResultDto> CalculateTaxAsync(TaxCalculationRequestDto request, CancellationToken cancellationToken = default);

    // Tax Categories CRUD
    Task<IReadOnlyList<TaxCategoryDto>> GetCategoriesAsync(bool includeInactive = false, CancellationToken cancellationToken = default);
    Task<TaxCategoryDto?> GetCategoryByIdAsync(long id, CancellationToken cancellationToken = default);
    Task<TaxCategoryDto> CreateCategoryAsync(CreateTaxCategoryDto dto, string username, CancellationToken cancellationToken = default);
    Task<TaxCategoryDto> UpdateCategoryAsync(long id, UpdateTaxCategoryDto dto, string username, CancellationToken cancellationToken = default);
    Task DeleteCategoryAsync(long id, CancellationToken cancellationToken = default);

    // Tax Rates CRUD
    Task<IReadOnlyList<TaxRateDto>> GetRatesAsync(bool includeInactive = false, CancellationToken cancellationToken = default);
    Task<TaxRateDto?> GetRateByIdAsync(long id, CancellationToken cancellationToken = default);
    Task<TaxRateDto?> GetRateByCodeAsync(string code, CancellationToken cancellationToken = default);
    Task<TaxRateDto> CreateRateAsync(CreateTaxRateDto dto, string username, CancellationToken cancellationToken = default);
    Task<TaxRateDto> UpdateRateAsync(long id, UpdateTaxRateDto dto, string username, CancellationToken cancellationToken = default);
    Task DeleteRateAsync(long id, CancellationToken cancellationToken = default);

    // Tax Groups CRUD
    Task<IReadOnlyList<TaxGroupDto>> GetGroupsAsync(bool includeInactive = false, CancellationToken cancellationToken = default);
    Task<TaxGroupDto?> GetGroupByIdAsync(long id, CancellationToken cancellationToken = default);
    Task<TaxGroupDto> CreateGroupAsync(CreateTaxGroupDto dto, string username, CancellationToken cancellationToken = default);
    Task DeleteGroupAsync(long id, CancellationToken cancellationToken = default);

    // Multi-Country VAT Return & Declaration Framework
    Task<IReadOnlyList<TaxDeclarationTemplateDto>> GetAvailableDeclarationTemplatesAsync(CancellationToken cancellationToken = default);
    Task<TaxDeclarationTemplateDto?> GetDeclarationTemplateAsync(string templateCode, CancellationToken cancellationToken = default);
    Task<VatDeclarationDto> GetVatDeclarationAsync(VatDeclarationFilterDto filter, CancellationToken cancellationToken = default);

    // ZATCA QR Code Generator
    ZatcaQrResultDto GenerateZatcaQrCode(ZatcaQrRequestDto request);
}
