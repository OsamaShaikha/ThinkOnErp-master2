using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using ThinkOnErp.Application.DTOs.Translations;
using ThinkOnErp.Domain.Interfaces;
using ThinkOnErp.Domain.Interfaces.Accounting;

namespace ThinkOnErp.Application.Services;

public sealed class TranslationService : ITranslationService
{
    private readonly ITranslationRepository _repository;
    private readonly ICurrentTenantContext _tenantContext;
    private readonly ILogger<TranslationService> _logger;

    public TranslationService(
        ITranslationRepository repository,
        ICurrentTenantContext tenantContext,
        ILogger<TranslationService> logger)
    {
        _repository = repository ?? throw new ArgumentNullException(nameof(repository));
        _tenantContext = tenantContext ?? throw new ArgumentNullException(nameof(tenantContext));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task SaveTranslationsAsync(
        string entityType, 
        long entityId, 
        IEnumerable<EntityTranslationDto> translations, 
        string username, 
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(entityType) || entityId <= 0 || translations == null)
            return;

        var dtos = translations
            .Where(t => !string.IsNullOrWhiteSpace(t.Lang) && !string.IsNullOrWhiteSpace(t.Value))
            .Select(t => (
                LangCode: t.Lang.Trim().ToLowerInvariant(), 
                FieldName: string.IsNullOrWhiteSpace(t.Field) ? "Name" : t.Field.Trim(), 
                TranslationText: t.Value.Trim()
            ));

        await _repository.SaveTranslationsAsync(entityType.ToUpperInvariant(), entityId, dtos, username, cancellationToken);
    }

    public async Task<EntityTranslationsResponseDto> GetTranslationsAsync(
        string entityType, 
        long entityId, 
        CancellationToken cancellationToken = default)
    {
        var normalizedType = entityType.ToUpperInvariant();
        var records = await _repository.GetTranslationsAsync(normalizedType, entityId, cancellationToken);

        var result = new EntityTranslationsResponseDto
        {
            EntityType = normalizedType,
            EntityId = entityId,
            Translations = new Dictionary<string, Dictionary<string, string>>()
        };

        foreach (var rec in records)
        {
            if (!result.Translations.ContainsKey(rec.FieldName))
            {
                result.Translations[rec.FieldName] = new Dictionary<string, string>();
            }
            result.Translations[rec.FieldName][rec.LangCode] = rec.TranslationText;
        }

        return result;
    }

    public async Task<string> ResolveDisplayNameAsync(
        string entityType, 
        long entityId, 
        string fieldName, 
        string defaultText, 
        string? requestedLang = null, 
        CancellationToken cancellationToken = default)
    {
        var lang = ResolveTargetLanguage(requestedLang);
        if (string.IsNullOrWhiteSpace(lang) || lang == "en")
        {
            return defaultText;
        }

        var translation = await _repository.GetTranslationAsync(
            entityType.ToUpperInvariant(), 
            entityId, 
            fieldName, 
            lang, 
            cancellationToken);

        return !string.IsNullOrWhiteSpace(translation) ? translation : defaultText;
    }

    public async Task PopulateDisplayNamesAsync<T>(
        string entityType, 
        IEnumerable<T> items, 
        Func<T, long> idSelector, 
        Func<T, string> defaultSelector, 
        Action<T, string> setter, 
        string fieldName = "Name", 
        string? requestedLang = null, 
        CancellationToken cancellationToken = default)
    {
        var list = items.ToList();
        if (!list.Any()) return;

        var lang = ResolveTargetLanguage(requestedLang);
        
        // If English or no translation requested, use default text
        if (string.IsNullOrWhiteSpace(lang) || lang == "en")
        {
            foreach (var item in list)
            {
                setter(item, defaultSelector(item));
            }
            return;
        }

        var ids = list.Select(idSelector).Distinct();
        var translations = await _repository.GetTranslationsForEntitiesAsync(
            entityType.ToUpperInvariant(), 
            ids, 
            fieldName, 
            lang, 
            cancellationToken);

        foreach (var item in list)
        {
            var id = idSelector(item);
            if (translations.TryGetValue(id, out var text) && !string.IsNullOrWhiteSpace(text))
            {
                setter(item, text);
            }
            else
            {
                setter(item, defaultSelector(item));
            }
        }
    }

    private string ResolveTargetLanguage(string? explicitLang)
    {
        if (!string.IsNullOrWhiteSpace(explicitLang))
            return explicitLang.Trim().ToLowerInvariant();

        try
        {
            var lang = _tenantContext.GetLanguageCode();
            return !string.IsNullOrWhiteSpace(lang) ? lang.Trim().ToLowerInvariant() : "en";
        }
        catch
        {
            return "en";
        }
    }
}
