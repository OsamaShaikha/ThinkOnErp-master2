using System;
using System.Linq;
using ThinkOnErp.Application.DTOs.Inventory.Types;
using ThinkOnErp.Domain.Entities.Inventory;

namespace ThinkOnErp.Application.Mappings.Inventory;

public static class TrxTypeMapper
{
    public static TrxDocTypeDto ToDto(TrxDocType entity)
    {
        return new TrxDocTypeDto
        {
            TypeCode = entity.TypeCode,
            TypeKey = entity.TypeKey,
            TypeNameLocal = entity.TypeNameLocal,
            TypeNameEn = entity.TypeNameEn,
            ModuleCode = entity.ModuleCode,
            DocPrefix = entity.DocPrefix,
            ResetPolicy = entity.ResetPolicy,
            IsSystemReserved = entity.IsSystemReserved,
            IsActive = entity.IsActive,
            TransactionTypes = entity.TransactionTypes?.Select(t => new TrxTransactionTypeSummaryDto
            {
                TrxCode = t.TrxCode,
                TrxKey = t.TrxKey,
                TrxNameLocal = t.TrxNameLocal,
                TrxNameEn = t.TrxNameEn,
                AffectsStock = t.AffectsStock,
                StockDirection = t.StockDirection,
                AffectsGl = t.AffectsGl,
                IsActive = t.IsActive
            }).ToList() ?? new()
        };
    }

    public static TrxDocType ToEntity(CreateTrxDocTypeDto dto, string username)
    {
        return new TrxDocType
        {
            TypeCode = dto.TypeCode,
            TypeKey = dto.TypeKey.Trim().ToUpperInvariant(),
            TypeNameLocal = dto.TypeNameLocal.Trim(),
            TypeNameEn = dto.TypeNameEn.Trim(),
            ModuleCode = string.IsNullOrWhiteSpace(dto.ModuleCode) ? "INVENTORY" : dto.ModuleCode.Trim().ToUpperInvariant(),
            DocPrefix = dto.DocPrefix.Trim().ToUpperInvariant(),
            ResetPolicy = string.IsNullOrWhiteSpace(dto.ResetPolicy) ? "YEARLY" : dto.ResetPolicy.Trim().ToUpperInvariant(),
            IsSystemReserved = false,
            IsActive = dto.IsActive,
            CreationUser = username,
            CreationDate = DateTime.UtcNow
        };
    }

    public static TrxTransactionTypeDto ToDto(TrxTransactionType entity)
    {
        return new TrxTransactionTypeDto
        {
            TrxCode = entity.TrxCode,
            DocTypeCode = entity.DocTypeCode,
            DocTypeNameLocal = entity.DocType?.TypeNameLocal,
            DocTypeNameEn = entity.DocType?.TypeNameEn,
            TrxKey = entity.TrxKey,
            TrxNameLocal = entity.TrxNameLocal,
            TrxNameEn = entity.TrxNameEn,
            AffectsStock = entity.AffectsStock,
            StockDirection = entity.StockDirection,
            RequiresWarehouse = entity.RequiresWarehouse,
            AffectsGl = entity.AffectsGl,
            AffectsPartyBalance = entity.AffectsPartyBalance,
            PostingRuleCode = entity.PostingRuleCode,
            RequiresParty = entity.RequiresParty,
            RequiresPrice = entity.RequiresPrice,
            RequiresCost = entity.RequiresCost,
            IsActive = entity.IsActive
        };
    }

    public static TrxTransactionType ToEntity(CreateTrxTransactionTypeDto dto, string username)
    {
        return new TrxTransactionType
        {
            TrxCode = dto.TrxCode,
            DocTypeCode = dto.DocTypeCode,
            TrxKey = dto.TrxKey.Trim().ToUpperInvariant(),
            TrxNameLocal = dto.TrxNameLocal.Trim(),
            TrxNameEn = dto.TrxNameEn.Trim(),
            AffectsStock = dto.AffectsStock,
            StockDirection = dto.StockDirection,
            RequiresWarehouse = dto.RequiresWarehouse,
            AffectsGl = dto.AffectsGl,
            AffectsPartyBalance = dto.AffectsPartyBalance,
            PostingRuleCode = dto.PostingRuleCode?.Trim().ToUpperInvariant(),
            RequiresParty = dto.RequiresParty,
            RequiresPrice = dto.RequiresPrice,
            RequiresCost = dto.RequiresCost,
            IsActive = dto.IsActive,
            CreationUser = username,
            CreationDate = DateTime.UtcNow
        };
    }
}
