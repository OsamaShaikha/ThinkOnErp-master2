using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using ThinkOnErp.Application.Common;
using ThinkOnErp.Application.DTOs.Accounting.PaymentMethods;
using ThinkOnErp.Domain.Entities.Accounting;
using ThinkOnErp.Domain.Interfaces.Accounting;

namespace ThinkOnErp.Application.Services.Accounting;

public sealed class PaymentMethodService : IPaymentMethodService
{
    private readonly IPaymentMethodRepository _repository;
    private readonly IBankingRepository _bankingRepository;
    private readonly ILogger<PaymentMethodService> _logger;

    public PaymentMethodService(
        IPaymentMethodRepository repository,
        IBankingRepository bankingRepository,
        ILogger<PaymentMethodService> logger)
    {
        _repository = repository;
        _bankingRepository = bankingRepository;
        _logger = logger;
    }

    public async Task<IReadOnlyList<PaymentMethodDto>> GetAllAsync(
        long? branchId,
        string? methodType,
        bool? showInPos,
        bool? showInInvoices,
        bool? activeOnly,
        CancellationToken ct = default)
    {
        var list = await _repository.GetAllAsync(branchId, methodType, showInPos, showInInvoices, activeOnly, ct);
        return list.Select(MapToDto).ToList();
    }

    public async Task<PaymentMethodDto?> GetByIdAsync(long id, CancellationToken ct = default)
    {
        var entity = await _repository.GetByIdAsync(id, ct);
        return entity == null ? null : MapToDto(entity);
    }

    public async Task<PaymentMethodDto?> GetByCodeAsync(long branchId, string code, CancellationToken ct = default)
    {
        var entity = await _repository.GetByCodeAsync(branchId, code, ct);
        return entity == null ? null : MapToDto(entity);
    }

    public async Task<ApiResponse<PaymentMethodDto>> CreateAsync(CreatePaymentMethodDto dto, string username, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(dto.Code))
            return ApiResponse<PaymentMethodDto>.CreateFailure("كود وسيلة الدفع مطلوب (Code is required)", statusCode: 400);

        if (string.IsNullOrWhiteSpace(dto.NameLocal))
            return ApiResponse<PaymentMethodDto>.CreateFailure("اسم وسيلة الدفع بالعربية مطلوب (NameLocal is required)", statusCode: 400);

        if (string.IsNullOrWhiteSpace(dto.GlAccountCode))
            return ApiResponse<PaymentMethodDto>.CreateFailure("حساب الأستاذ العام المرتبط مطلوب (GlAccountCode is required)", statusCode: 400);

        var existing = await _repository.GetByCodeAsync(dto.BranchId, dto.Code.Trim().ToUpperInvariant(), ct);
        if (existing != null)
            return ApiResponse<PaymentMethodDto>.CreateFailure($"وسيلة الدفع بالكود '{dto.Code}' مسجلة مسبقاً في هذا الفرع", statusCode: 400);

        // Validate Bank Account if provided
        if (dto.BankAccountId.HasValue)
        {
            var bank = await _bankingRepository.GetBankAccountByIdAsync(dto.BankAccountId.Value, ct);
            if (bank == null)
                return ApiResponse<PaymentMethodDto>.CreateFailure("الحساب البنكي المحدد غير موجود", statusCode: 400);
        }

        // Validate Cash Register if provided
        if (dto.CashRegisterId.HasValue)
        {
            var reg = await _bankingRepository.GetCashRegisterByIdAsync(dto.CashRegisterId.Value, ct);
            if (reg == null)
                return ApiResponse<PaymentMethodDto>.CreateFailure("صندوق الكاشير / الخزينة المحددة غير موجودة", statusCode: 400);
        }

        var entity = new PaymentMethod
        {
            BranchId = dto.BranchId,
            Code = dto.Code.Trim().ToUpperInvariant(),
            NameLocal = dto.NameLocal.Trim(),
            NameEn = string.IsNullOrWhiteSpace(dto.NameEn) ? dto.NameLocal.Trim() : dto.NameEn.Trim(),
            MethodType = dto.MethodType.Trim().ToUpperInvariant(),
            GlAccountCode = dto.GlAccountCode.Trim(),
            BankAccountId = dto.BankAccountId,
            CashRegisterId = dto.CashRegisterId,
            CommissionPercent = dto.CommissionPercent,
            CommissionFixedAmount = dto.CommissionFixedAmount,
            CommissionGlAccountCode = string.IsNullOrWhiteSpace(dto.CommissionGlAccountCode) ? null : dto.CommissionGlAccountCode.Trim(),
            RequiresReference = dto.RequiresReference,
            RequiresDueDate = dto.RequiresDueDate,
            AutoPostGl = dto.AutoPostGl,
            ShowInPos = dto.ShowInPos,
            ShowInInvoices = dto.ShowInInvoices,
            ShowInVouchers = dto.ShowInVouchers,
            IsActive = dto.IsActive,
            DisplayOrder = dto.DisplayOrder,
            CreationUser = username,
            CreationDate = DateTime.UtcNow
        };

        await _repository.AddAsync(entity, ct);
        _logger.LogInformation("Payment method '{Code}' created for branch {BranchId} by {User}", entity.Code, entity.BranchId, username);

        var created = await _repository.GetByIdAsync(entity.Id, ct);
        return ApiResponse<PaymentMethodDto>.CreateSuccess(MapToDto(created ?? entity), "تم إضافة وسيلة الدفع بنجاح", 201);
    }

    public async Task<ApiResponse<PaymentMethodDto>> UpdateAsync(long id, UpdatePaymentMethodDto dto, string username, CancellationToken ct = default)
    {
        var entity = await _repository.GetByIdAsync(id, ct);
        if (entity == null)
            return ApiResponse<PaymentMethodDto>.CreateFailure("وسيلة الدفع المحددة غير موجودة", statusCode: 404);

        if (!string.IsNullOrWhiteSpace(dto.NameLocal))
            entity.NameLocal = dto.NameLocal.Trim();

        if (!string.IsNullOrWhiteSpace(dto.NameEn))
            entity.NameEn = dto.NameEn.Trim();

        if (!string.IsNullOrWhiteSpace(dto.MethodType))
            entity.MethodType = dto.MethodType.Trim().ToUpperInvariant();

        if (!string.IsNullOrWhiteSpace(dto.GlAccountCode))
            entity.GlAccountCode = dto.GlAccountCode.Trim();

        if (dto.BankAccountId.HasValue)
        {
            if (dto.BankAccountId.Value > 0)
            {
                var bank = await _bankingRepository.GetBankAccountByIdAsync(dto.BankAccountId.Value, ct);
                if (bank == null)
                    return ApiResponse<PaymentMethodDto>.CreateFailure("الحساب البنكي المحدد غير موجود", statusCode: 400);
                entity.BankAccountId = dto.BankAccountId.Value;
            }
            else
            {
                entity.BankAccountId = null;
            }
        }

        if (dto.CashRegisterId.HasValue)
        {
            if (dto.CashRegisterId.Value > 0)
            {
                var reg = await _bankingRepository.GetCashRegisterByIdAsync(dto.CashRegisterId.Value, ct);
                if (reg == null)
                    return ApiResponse<PaymentMethodDto>.CreateFailure("صندوق الكاشير / الخزينة المحددة غير موجودة", statusCode: 400);
                entity.CashRegisterId = dto.CashRegisterId.Value;
            }
            else
            {
                entity.CashRegisterId = null;
            }
        }

        if (dto.CommissionPercent.HasValue)
            entity.CommissionPercent = dto.CommissionPercent.Value;

        if (dto.CommissionFixedAmount.HasValue)
            entity.CommissionFixedAmount = dto.CommissionFixedAmount.Value;

        if (dto.CommissionGlAccountCode != null)
            entity.CommissionGlAccountCode = string.IsNullOrWhiteSpace(dto.CommissionGlAccountCode) ? null : dto.CommissionGlAccountCode.Trim();

        if (dto.RequiresReference.HasValue)
            entity.RequiresReference = dto.RequiresReference.Value;

        if (dto.RequiresDueDate.HasValue)
            entity.RequiresDueDate = dto.RequiresDueDate.Value;

        if (dto.AutoPostGl.HasValue)
            entity.AutoPostGl = dto.AutoPostGl.Value;

        if (dto.ShowInPos.HasValue)
            entity.ShowInPos = dto.ShowInPos.Value;

        if (dto.ShowInInvoices.HasValue)
            entity.ShowInInvoices = dto.ShowInInvoices.Value;

        if (dto.ShowInVouchers.HasValue)
            entity.ShowInVouchers = dto.ShowInVouchers.Value;

        if (dto.IsActive.HasValue)
            entity.IsActive = dto.IsActive.Value;

        if (dto.DisplayOrder.HasValue)
            entity.DisplayOrder = dto.DisplayOrder.Value;

        entity.UpdateUser = username;
        entity.UpdateDate = DateTime.UtcNow;

        await _repository.UpdateAsync(entity, ct);
        _logger.LogInformation("Payment method '{Id}' ({Code}) updated by {User}", id, entity.Code, username);

        var updated = await _repository.GetByIdAsync(id, ct);
        return ApiResponse<PaymentMethodDto>.CreateSuccess(MapToDto(updated ?? entity), "تم تحديث وسيلة الدفع بنجاح", 200);
    }

    public async Task<ApiResponse<bool>> DeleteAsync(long id, CancellationToken ct = default)
    {
        var entity = await _repository.GetByIdAsync(id, ct);
        if (entity == null)
            return ApiResponse<bool>.CreateFailure("وسيلة الدفع المحددة غير موجودة", statusCode: 404);

        // Soft-delete by setting IsActive = false
        entity.IsActive = false;
        entity.UpdateDate = DateTime.UtcNow;
        await _repository.UpdateAsync(entity, ct);

        _logger.LogInformation("Payment method '{Id}' deactivated", id);
        return ApiResponse<bool>.CreateSuccess(true, "تم تعطيل وسيلة الدفع بنجاح", 200);
    }

    public async Task<ApiResponse<int>> SeedDefaultPaymentMethodsAsync(long branchId, string username, CancellationToken ct = default)
    {
        var defaults = new (string Code, string NameAr, string NameEn, string Type, string GlAcc, decimal CommPct, bool ReqRef, bool ReqDue, bool InPos, bool InInv, bool InVouch, int Order)[]
        {
            ("CASH", "نقدي - الصندوق الرئيسي", "Cash - Main Register", "CASH", "111101", 0, false, false, true, true, true, 1),
            ("MADA", "مدى - شبكة نقاط البيع", "Mada - POS Terminal", "CARD", "111201", 0.8m, true, false, true, true, true, 2),
            ("VISA", "فيزا / ماستركارد", "Visa / MasterCard", "CARD", "111201", 1.5m, true, false, true, true, true, 3),
            ("BANK_TRANSFER", "تحويل بنكي", "Bank Transfer", "BANK", "111201", 0, true, false, true, true, true, 4),
            ("CHEQUE", "شيكات برسم التحصيل", "Cheques Under Collection", "CHEQUE", "111301", 0, true, true, false, true, true, 5),
        };

        int seededCount = 0;
        foreach (var def in defaults)
        {
            var existing = await _repository.GetByCodeAsync(branchId, def.Code, ct);
            if (existing == null)
            {
                var entity = new PaymentMethod
                {
                    BranchId = branchId,
                    Code = def.Code,
                    NameLocal = def.NameAr,
                    NameEn = def.NameEn,
                    MethodType = def.Type,
                    GlAccountCode = def.GlAcc,
                    CommissionPercent = def.CommPct,
                    RequiresReference = def.ReqRef,
                    RequiresDueDate = def.ReqDue,
                    AutoPostGl = true,
                    ShowInPos = def.InPos,
                    ShowInInvoices = def.InInv,
                    ShowInVouchers = def.InVouch,
                    IsActive = true,
                    DisplayOrder = def.Order,
                    CreationUser = username,
                    CreationDate = DateTime.UtcNow
                };
                await _repository.AddAsync(entity, ct);
                seededCount++;
            }
        }

        return ApiResponse<int>.CreateSuccess(seededCount, $"تم تهيئة {seededCount} وسيلة دفع قياسية للفرع بنجاح", 200);
    }

    private static PaymentMethodDto MapToDto(PaymentMethod m)
    {
        return new PaymentMethodDto
        {
            Id = m.Id,
            BranchId = m.BranchId,
            Code = m.Code,
            NameLocal = m.NameLocal,
            NameEn = m.NameEn,
            MethodType = m.MethodType,
            GlAccountCode = m.GlAccountCode,
            GlAccountName = m.GlAccount?.AccountNameLocal ?? m.GlAccount?.AccountNameEn,
            BankAccountId = m.BankAccountId,
            BankAccountName = m.BankAccount?.AccountNameLocal ?? m.BankAccount?.BankName,
            CashRegisterId = m.CashRegisterId,
            CashRegisterName = m.CashRegister?.NameLocal ?? m.CashRegister?.NameEn,
            CommissionPercent = m.CommissionPercent,
            CommissionFixedAmount = m.CommissionFixedAmount,
            CommissionGlAccountCode = m.CommissionGlAccountCode,
            CommissionGlAccountName = m.CommissionGlAccount?.AccountNameLocal ?? m.CommissionGlAccount?.AccountNameEn,
            RequiresReference = m.RequiresReference,
            RequiresDueDate = m.RequiresDueDate,
            AutoPostGl = m.AutoPostGl,
            ShowInPos = m.ShowInPos,
            ShowInInvoices = m.ShowInInvoices,
            ShowInVouchers = m.ShowInVouchers,
            IsActive = m.IsActive,
            DisplayOrder = m.DisplayOrder,
            CreationUser = m.CreationUser,
            CreationDate = m.CreationDate,
            UpdateUser = m.UpdateUser,
            UpdateDate = m.UpdateDate
        };
    }
}
