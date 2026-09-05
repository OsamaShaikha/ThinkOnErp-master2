using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using ThinkOnErp.Domain.Entities;
using ThinkOnErp.Domain.Interfaces;
using ThinkOnErp.Infrastructure.Data;

namespace ThinkOnErp.Infrastructure.Repositories;

public sealed class TranslationRepository : ITranslationRepository
{
    private readonly OracleDbContext _context;

    public TranslationRepository(OracleDbContext context)
    {
        _context = context ?? throw new ArgumentNullException(nameof(context));
    }

    public async Task<IReadOnlyList<SysEntityTranslation>> GetTranslationsAsync(
        string entityType, 
        long entityId, 
        CancellationToken cancellationToken = default)
    {
        return await _context.EntityTranslations
            .AsNoTracking()
            .Where(t => t.EntityType == entityType && t.EntityId == entityId)
            .ToListAsync(cancellationToken);
    }

    public async Task<string?> GetTranslationAsync(
        string entityType, 
        long entityId, 
        string fieldName, 
        string langCode, 
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(langCode)) return null;

        var normalizedLang = langCode.Trim().ToLowerInvariant();

        return await _context.EntityTranslations
            .AsNoTracking()
            .Where(t => t.EntityType == entityType 
                     && t.EntityId == entityId 
                     && t.FieldName == fieldName 
                     && t.LangCode == normalizedLang)
            .Select(t => t.TranslationText)
            .FirstOrDefaultAsync(cancellationToken);
    }

    public async Task<Dictionary<long, string>> GetTranslationsForEntitiesAsync(
        string entityType, 
        IEnumerable<long> entityIds, 
        string fieldName, 
        string langCode, 
        CancellationToken cancellationToken = default)
    {
        var idList = entityIds.Distinct().ToList();
        if (!idList.Any() || string.IsNullOrWhiteSpace(langCode))
            return new Dictionary<long, string>();

        var normalizedLang = langCode.Trim().ToLowerInvariant();

        var list = await _context.EntityTranslations
            .AsNoTracking()
            .Where(t => t.EntityType == entityType 
                     && idList.Contains(t.EntityId) 
                     && t.FieldName == fieldName 
                     && t.LangCode == normalizedLang)
            .Select(t => new { t.EntityId, t.TranslationText })
            .ToListAsync(cancellationToken);

        return list.ToDictionary(k => k.EntityId, v => v.TranslationText);
    }

    public async Task SaveTranslationsAsync(
        string entityType, 
        long entityId, 
        IEnumerable<(string LangCode, string FieldName, string TranslationText)> translations, 
        string username, 
        CancellationToken cancellationToken = default)
    {
        var transList = translations.ToList();
        if (!transList.Any()) return;

        var existing = await _context.EntityTranslations
            .Where(t => t.EntityType == entityType && t.EntityId == entityId)
            .ToListAsync(cancellationToken);

        var now = DateTime.UtcNow;

        foreach (var item in transList)
        {
            if (string.IsNullOrWhiteSpace(item.LangCode) || string.IsNullOrWhiteSpace(item.TranslationText))
                continue;

            var normalizedLang = item.LangCode.Trim().ToLowerInvariant();
            var field = string.IsNullOrWhiteSpace(item.FieldName) ? "Name" : item.FieldName.Trim();

            var match = existing.FirstOrDefault(t => t.LangCode == normalizedLang && t.FieldName == field);
            if (match != null)
            {
                match.TranslationText = item.TranslationText.Trim();
                match.UpdateUser = username;
                match.UpdateDate = now;
            }
            else
            {
                _context.EntityTranslations.Add(new SysEntityTranslation
                {
                    EntityType = entityType,
                    EntityId = entityId,
                    FieldName = field,
                    LangCode = normalizedLang,
                    TranslationText = item.TranslationText.Trim(),
                    CreationUser = username,
                    CreationDate = now
                });
            }
        }

        await _context.SaveChangesAsync(cancellationToken);
    }

    public async Task DeleteTranslationsAsync(
        string entityType, 
        long entityId, 
        CancellationToken cancellationToken = default)
    {
        var existing = await _context.EntityTranslations
            .Where(t => t.EntityType == entityType && t.EntityId == entityId)
            .ToListAsync(cancellationToken);

        if (existing.Any())
        {
            _context.EntityTranslations.RemoveRange(existing);
            await _context.SaveChangesAsync(cancellationToken);
        }
    }
}
