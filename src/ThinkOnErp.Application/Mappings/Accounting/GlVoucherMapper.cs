using ThinkOnErp.Application.DTOs.Accounting.Vouchers;
using ThinkOnErp.Domain.Entities.Accounting;

namespace ThinkOnErp.Application.Mappings.Accounting;

public static class GlVoucherMapper
{
    public static GlVoucherTypeDto ToDto(GlVoucherType entity)
    {
        return new GlVoucherTypeDto
        {
            Id = entity.Id,
            TypeCode = entity.TypeCode,
            TypeKey = entity.TypeKey,
            NameAr = entity.NameAr,
            NameEn = entity.NameEn,
            Prefix = entity.Prefix,
            Category = entity.Category,
            SerialResetPolicy = entity.SerialResetPolicy,
            RequiresReview = entity.RequiresReview,
            AllowManualEntry = entity.AllowManualEntry,
            IsSystem = entity.IsSystem,
            DisplayOrder = entity.DisplayOrder,
            IsActive = entity.IsActive,
            Description = entity.Description
        };
    }

    public static GlVoucherHeaderDto ToDto(GlVoucherHeader entity, GlVoucherType? voucherType = null)
    {
        var prefix = voucherType?.Prefix ?? "V";
        var statusName = entity.Status switch
        {
            1 => "Draft / مسودة",
            2 => "Reviewed / تمت المراجعة",
            3 => "Posted / مرحل",
            4 => "Reversed / معكوس",
            _ => "Unknown"
        };

        return new GlVoucherHeaderDto
        {
            Id = entity.Id,
            BranchId = entity.BranchId,
            FiscalYearId = entity.FiscalYearId,
            VoucherYear = entity.VoucherYear,
            VoucherMonth = entity.VoucherMonth,
            VoucherType = entity.VoucherType,
            VoucherTypeNameAr = voucherType?.NameAr,
            VoucherTypeNameEn = voucherType?.NameEn,
            VoucherNo = entity.VoucherNo,
            FullVoucherNumber = $"{prefix}-{entity.VoucherYear}-{entity.VoucherMonth:D2}-{entity.VoucherNo:D5}",
            VoucherDate = entity.VoucherDate,
            Description = entity.Description,
            TotalAmount = entity.TotalAmount,
            TotalLocalDebit = entity.TotalLocalDebit,
            TotalLocalCredit = entity.TotalLocalCredit,
            Status = entity.Status,
            StatusName = statusName,
            IsAutoRecord = entity.IsAutoRecord,
            SourceSystemCode = entity.SourceSystemCode,
            SourceRefId = entity.SourceRefId,
            IsStandby = entity.IsStandby,
            IsReviewed = entity.IsReviewed,
            ReviewUser = entity.ReviewUser,
            ReviewDate = entity.ReviewDate,
            PostUser = entity.PostUser,
            PostDate = entity.PostDate,
            UnpostUser = entity.UnpostUser,
            UnpostDate = entity.UnpostDate,
            IsReversed = entity.IsReversed,
            ReverseUser = entity.ReverseUser,
            ReverseDate = entity.ReverseDate,
            CreationUser = entity.CreationUser,
            CreationDate = entity.CreationDate,
            Details = entity.Details.Select(ToDto).ToList()
        };
    }

    public static GlVoucherDetailDto ToDto(GlVoucherDetail entity)
    {
        return new GlVoucherDetailDto
        {
            Id = entity.Id,
            VoucherId = entity.VoucherId,
            LineSer = entity.LineSer,
            AccountCode = entity.AccountCode,
            AccountNameAr = entity.Account?.AccountNameAr,
            AccountNameEn = entity.Account?.AccountNameEn,
            Debit = entity.Debit,
            Credit = entity.Credit,
            LocalDebit = entity.LocalDebit,
            LocalCredit = entity.LocalCredit,
            BaseDebit = entity.BaseDebit,
            BaseCredit = entity.BaseCredit,
            Description = entity.Description,
            CurrencyId = entity.CurrencyId,
            ExchangeRate = entity.ExchangeRate,
            CostCenterCode = entity.CostCenterCode,
            CostCenterMgrCode = entity.CostCenterMgrCode,
            CostCenterMnrCode = entity.CostCenterMnrCode,
            CostCenterNameAr = entity.CostCenter?.NameAr,
            CostCenterNameEn = entity.CostCenter?.NameEn,
            IsSettlement = entity.IsSettlement
        };
    }
}
