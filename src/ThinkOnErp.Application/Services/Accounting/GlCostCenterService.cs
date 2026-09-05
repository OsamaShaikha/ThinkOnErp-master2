using Microsoft.Extensions.Logging;
using ThinkOnErp.Application.DTOs.Accounting.CostCenters;
using ThinkOnErp.Application.Mappings.Accounting;
using ThinkOnErp.Domain.Entities.Accounting;
using ThinkOnErp.Domain.Exceptions;
using ThinkOnErp.Domain.Interfaces.Accounting;

namespace ThinkOnErp.Application.Services.Accounting;

public sealed class GlCostCenterService : IGlCostCenterService
{
    private readonly IGlCostCenterRepository _repository;
    private readonly ITranslationService? _translationService;
    private readonly ILogger<GlCostCenterService> _logger;

    public GlCostCenterService(
        IGlCostCenterRepository repository,
        ILogger<GlCostCenterService> logger,
        ITranslationService? translationService = null)
    {
        _repository = repository ?? throw new ArgumentNullException(nameof(repository));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _translationService = translationService;
    }

    public async Task<IReadOnlyList<GlCostCenterDto>> GetAllAsync(CancellationToken cancellationToken = default)
    {
        var list = await _repository.GetAllAsync(cancellationToken);
        return list.Select(GlCostCenterMapper.ToDto).ToList();
    }

    public async Task<IReadOnlyList<GlCostCenterTreeDto>> GetTreeAsync(CancellationToken cancellationToken = default)
    {
        var allCenters = await _repository.GetAllAsync(cancellationToken);
        var dtoMap = allCenters.ToDictionary(c => c.CostCenterCode, GlCostCenterMapper.ToTreeDto);
        var roots = new List<GlCostCenterTreeDto>();

        foreach (var center in allCenters)
        {
            var node = dtoMap[center.CostCenterCode];
            if (string.IsNullOrWhiteSpace(center.ParentCostCenterCode) || !dtoMap.TryGetValue(center.ParentCostCenterCode, out var parentNode))
            {
                roots.Add(node);
            }
            else
            {
                parentNode.Children.Add(node);
            }
        }

        return roots;
    }

    public async Task<GlCostCenterDto?> GetByCodeAsync(string costCenterCode, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(costCenterCode);

        var center = await _repository.GetByCodeAsync(costCenterCode, cancellationToken);
        return center == null ? null : GlCostCenterMapper.ToDto(center);
    }

    public async Task<GlCostCenterDto> CreateAsync(CreateGlCostCenterDto dto, string username = "SYSTEM", CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(dto);

        if (await _repository.CodeExistsAsync(dto.CostCenterCode, cancellationToken))
        {
            throw new AccountingException($"Cost center with code '{dto.CostCenterCode}' already exists.", "GL_COST_CENTER_DUPLICATE_CODE");
        }

        int level = 1;
        if (!string.IsNullOrWhiteSpace(dto.ParentCostCenterCode))
        {
            var parent = await _repository.GetByCodeAsync(dto.ParentCostCenterCode, cancellationToken);
            if (parent == null)
            {
                throw new AccountingException($"Parent cost center with code '{dto.ParentCostCenterCode}' was not found.", "GL_COST_CENTER_PARENT_NOT_FOUND");
            }

            level = parent.CostCenterLevel + 1;
        }

        var currentUser = string.IsNullOrWhiteSpace(username) ? "SYSTEM" : username;
        var entity = new GlCostCenter
        {
            CostCenterCode = dto.CostCenterCode.Trim(),
            ParentCostCenterCode = string.IsNullOrWhiteSpace(dto.ParentCostCenterCode) ? null : dto.ParentCostCenterCode.Trim(),
            NameLocal = dto.NameLocal.Trim(),
            NameEn = dto.NameEn.Trim(),
            CostCenterLevel = level,
            CostCenterType = dto.CostCenterType.ToUpperInvariant(),
            IsPostable = dto.CostCenterType.Equals("DETAIL", StringComparison.OrdinalIgnoreCase) && dto.IsPostable,
            IsActive = dto.IsActive,
            CreationUser = currentUser,
            CreationDate = DateTime.UtcNow
        };

        await _repository.AddAsync(entity, cancellationToken);
        await _repository.SaveChangesAsync(cancellationToken);

        // Save translations if provided
        if (_translationService != null && dto.Translations != null && dto.Translations.Any() && long.TryParse(entity.CostCenterCode, out var costCenterId))
        {
            await _translationService.SaveTranslationsAsync("GL_COST_CENTER", costCenterId, dto.Translations, currentUser, cancellationToken);
        }

        _logger.LogInformation("Cost Center {Code} created by {User}", entity.CostCenterCode, currentUser);
        return GlCostCenterMapper.ToDto(entity);
    }

    public async Task<GlCostCenterDto> UpdateAsync(string costCenterCode, UpdateGlCostCenterDto dto, string username = "SYSTEM", CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(costCenterCode);
        ArgumentNullException.ThrowIfNull(dto);

        var entity = await _repository.GetByCodeAsync(costCenterCode, cancellationToken);
        if (entity == null)
        {
            throw new AccountingNotFoundException($"Cost center with code '{costCenterCode}' was not found.", "GL_COST_CENTER_NOT_FOUND");
        }

        var currentUser = string.IsNullOrWhiteSpace(username) ? "SYSTEM" : username;
        entity.NameLocal = dto.NameLocal.Trim();
        entity.NameEn = dto.NameEn.Trim();
        entity.CostCenterType = dto.CostCenterType.ToUpperInvariant();
        entity.IsPostable = dto.CostCenterType.Equals("DETAIL", StringComparison.OrdinalIgnoreCase) && dto.IsPostable;
        entity.IsActive = dto.IsActive;
        entity.UpdateUser = currentUser;
        entity.UpdateDate = DateTime.UtcNow;

        await _repository.SaveChangesAsync(cancellationToken);

        // Save translations if provided
        if (_translationService != null && dto.Translations != null && dto.Translations.Any() && long.TryParse(entity.CostCenterCode, out var costCenterId))
        {
            await _translationService.SaveTranslationsAsync("GL_COST_CENTER", costCenterId, dto.Translations, currentUser, cancellationToken);
        }

        _logger.LogInformation("Cost Center {Code} updated by {User}", entity.CostCenterCode, currentUser);
        return GlCostCenterMapper.ToDto(entity);
    }

    public async Task<GlCostCenterDto> SetActiveStatusAsync(string costCenterCode, bool isActive, string username = "SYSTEM", CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(costCenterCode);

        var entity = await _repository.GetByCodeAsync(costCenterCode, cancellationToken);
        if (entity == null)
        {
            throw new AccountingNotFoundException($"Cost center with code '{costCenterCode}' was not found.", "GL_COST_CENTER_NOT_FOUND");
        }

        var currentUser = string.IsNullOrWhiteSpace(username) ? "SYSTEM" : username;
        entity.IsActive = isActive;
        entity.UpdateUser = currentUser;
        entity.UpdateDate = DateTime.UtcNow;

        await _repository.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("Cost Center {Code} active status changed to {IsActive} by {User}", entity.CostCenterCode, isActive, currentUser);
        return GlCostCenterMapper.ToDto(entity);
    }
}
