using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using ThinkOnErp.Application.Common;
using ThinkOnErp.Application.DTOs.Inventory.Types;
using ThinkOnErp.Application.Mappings.Inventory;
using ThinkOnErp.Domain.Constants;
using ThinkOnErp.Domain.Interfaces.Inventory;

namespace ThinkOnErp.Application.Services.Inventory;

public sealed class TrxTypeService : ITrxTypeService
{
    private readonly ITrxTypeRepository _repository;
    private readonly ILogger<TrxTypeService> _logger;

    public TrxTypeService(ITrxTypeRepository repository, ILogger<TrxTypeService> logger)
    {
        _repository = repository;
        _logger = logger;
    }

    #region DocType CRUD

    public async Task<ApiResponse<TrxDocTypeDto>> CreateDocTypeAsync(CreateTrxDocTypeDto dto, string username, CancellationToken ct = default)
    {
        var existing = await _repository.GetDocTypeAsync(dto.TypeCode, ct);
        if (existing != null)
            return ApiResponse<TrxDocTypeDto>.CreateFailure($"Document type code {dto.TypeCode} already exists", null, 400);

        var entity = TrxTypeMapper.ToEntity(dto, username);
        var created = await _repository.CreateDocTypeAsync(entity, ct);
        return ApiResponse<TrxDocTypeDto>.CreateSuccess(TrxTypeMapper.ToDto(created), ResponseCodes.DocTypeCreated, 201);
    }

    public async Task<ApiResponse<List<TrxDocTypeDto>>> GetAllDocTypesAsync(CancellationToken ct = default)
    {
        var list = await _repository.GetAllDocTypesAsync(ct);
        var dtos = list.Select(TrxTypeMapper.ToDto).ToList();
        return ApiResponse<List<TrxDocTypeDto>>.CreateSuccess(dtos, ResponseCodes.DocTypesRetrieved);
    }

    public async Task<ApiResponse<TrxDocTypeDto>> GetDocTypeByCodeAsync(int code, CancellationToken ct = default)
    {
        var entity = await _repository.GetDocTypeAsync(code, ct);
        if (entity == null)
            return ApiResponse<TrxDocTypeDto>.CreateFailure($"Document type {code} not found", null, 404);

        return ApiResponse<TrxDocTypeDto>.CreateSuccess(TrxTypeMapper.ToDto(entity), ResponseCodes.DocTypeDetailsRetrieved);
    }

    public async Task<ApiResponse<TrxDocTypeDto>> UpdateDocTypeAsync(int code, UpdateTrxDocTypeDto dto, string username, CancellationToken ct = default)
    {
        var entity = await _repository.GetDocTypeAsync(code, ct);
        if (entity == null)
            return ApiResponse<TrxDocTypeDto>.CreateFailure($"Document type {code} not found", null, 404);

        entity.TypeNameLocal = dto.TypeNameLocal.Trim();
        entity.TypeNameEn = dto.TypeNameEn.Trim();
        entity.ModuleCode = string.IsNullOrWhiteSpace(dto.ModuleCode) ? entity.ModuleCode : dto.ModuleCode.Trim().ToUpperInvariant();
        entity.DocPrefix = dto.DocPrefix.Trim().ToUpperInvariant();
        entity.ResetPolicy = string.IsNullOrWhiteSpace(dto.ResetPolicy) ? entity.ResetPolicy : dto.ResetPolicy.Trim().ToUpperInvariant();
        entity.IsActive = dto.IsActive;
        entity.UpdateUser = username;
        entity.UpdateDate = DateTime.UtcNow;

        await _repository.UpdateDocTypeAsync(entity, ct);
        return ApiResponse<TrxDocTypeDto>.CreateSuccess(TrxTypeMapper.ToDto(entity), ResponseCodes.DocTypeUpdated);
    }

    public async Task<ApiResponse<bool>> DeleteDocTypeAsync(int code, CancellationToken ct = default)
    {
        var entity = await _repository.GetDocTypeAsync(code, ct);
        if (entity == null)
            return ApiResponse<bool>.CreateFailure($"Document type {code} not found", null, 404);

        if (entity.IsSystemReserved)
            return ApiResponse<bool>.CreateFailure($"Cannot delete system-reserved document type {code}", null, 400);

        if (entity.TransactionTypes != null && entity.TransactionTypes.Count > 0)
            return ApiResponse<bool>.CreateFailure($"Cannot delete document type {code} because it contains active transaction types", null, 400);

        await _repository.DeleteDocTypeAsync(entity, ct);
        return ApiResponse<bool>.CreateSuccess(true, ResponseCodes.DocTypeDeleted);
    }

    #endregion

    #region TrxType CRUD

    public async Task<ApiResponse<TrxTransactionTypeDto>> CreateTrxTypeAsync(CreateTrxTransactionTypeDto dto, string username, CancellationToken ct = default)
    {
        var docType = await _repository.GetDocTypeAsync(dto.DocTypeCode, ct);
        if (docType == null)
            return ApiResponse<TrxTransactionTypeDto>.CreateFailure($"Parent document type {dto.DocTypeCode} not found", null, 400);

        var existing = await _repository.GetTrxTypeAsync(dto.TrxCode, ct);
        if (existing != null)
            return ApiResponse<TrxTransactionTypeDto>.CreateFailure($"Transaction type code {dto.TrxCode} already exists", null, 400);

        var entity = TrxTypeMapper.ToEntity(dto, username);
        var created = await _repository.CreateTrxTypeAsync(entity, ct);
        created.DocType = docType;

        return ApiResponse<TrxTransactionTypeDto>.CreateSuccess(TrxTypeMapper.ToDto(created), ResponseCodes.TrxTypeCreated, 201);
    }

    public async Task<ApiResponse<List<TrxTransactionTypeDto>>> GetAllTrxTypesAsync(CancellationToken ct = default)
    {
        var list = await _repository.GetAllTrxTypesAsync(ct);
        var dtos = list.Select(TrxTypeMapper.ToDto).ToList();
        return ApiResponse<List<TrxTransactionTypeDto>>.CreateSuccess(dtos, ResponseCodes.TrxTypesRetrieved);
    }

    public async Task<ApiResponse<List<TrxTransactionTypeDto>>> GetTrxTypesByDocTypeAsync(int docTypeCode, CancellationToken ct = default)
    {
        var list = await _repository.GetTrxTypesByDocTypeAsync(docTypeCode, ct);
        var dtos = list.Select(TrxTypeMapper.ToDto).ToList();
        return ApiResponse<List<TrxTransactionTypeDto>>.CreateSuccess(dtos, ResponseCodes.TrxTypesRetrieved);
    }

    public async Task<ApiResponse<TrxTransactionTypeDto>> GetTrxTypeByCodeAsync(int code, CancellationToken ct = default)
    {
        var entity = await _repository.GetTrxTypeAsync(code, ct);
        if (entity == null)
            return ApiResponse<TrxTransactionTypeDto>.CreateFailure($"Transaction type {code} not found", null, 404);

        return ApiResponse<TrxTransactionTypeDto>.CreateSuccess(TrxTypeMapper.ToDto(entity), ResponseCodes.TrxTypeDetailsRetrieved);
    }

    public async Task<ApiResponse<TrxTransactionTypeDto>> UpdateTrxTypeAsync(int code, UpdateTrxTransactionTypeDto dto, string username, CancellationToken ct = default)
    {
        var entity = await _repository.GetTrxTypeAsync(code, ct);
        if (entity == null)
            return ApiResponse<TrxTransactionTypeDto>.CreateFailure($"Transaction type {code} not found", null, 404);

        entity.TrxNameLocal = dto.TrxNameLocal.Trim();
        entity.TrxNameEn = dto.TrxNameEn.Trim();
        entity.AffectsStock = dto.AffectsStock;
        entity.StockDirection = dto.StockDirection;
        entity.RequiresWarehouse = dto.RequiresWarehouse;
        entity.AffectsGl = dto.AffectsGl;
        entity.AffectsPartyBalance = dto.AffectsPartyBalance;
        entity.PostingRuleCode = dto.PostingRuleCode?.Trim().ToUpperInvariant();
        entity.RequiresParty = dto.RequiresParty;
        entity.RequiresPrice = dto.RequiresPrice;
        entity.RequiresCost = dto.RequiresCost;
        entity.IsActive = dto.IsActive;
        entity.UpdateUser = username;
        entity.UpdateDate = DateTime.UtcNow;

        await _repository.UpdateTrxTypeAsync(entity, ct);
        return ApiResponse<TrxTransactionTypeDto>.CreateSuccess(TrxTypeMapper.ToDto(entity), ResponseCodes.TrxTypeUpdated);
    }

    public async Task<ApiResponse<bool>> DeleteTrxTypeAsync(int code, CancellationToken ct = default)
    {
        var entity = await _repository.GetTrxTypeAsync(code, ct);
        if (entity == null)
            return ApiResponse<bool>.CreateFailure($"Transaction type {code} not found", null, 404);

        await _repository.DeleteTrxTypeAsync(entity, ct);
        return ApiResponse<bool>.CreateSuccess(true, ResponseCodes.TrxTypeDeleted);
    }

    #endregion
}
