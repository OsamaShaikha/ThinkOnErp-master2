using ThinkOnErp.Application.DTOs.Accounting;
using ThinkOnErp.Domain.Entities.Accounting;

namespace ThinkOnErp.Application.Mappings.Accounting;

public static class GlAccountMapper
{
    public static GlAccountDto ToDto(GlAccount account)
    {
        int categoryCode = 0;
        if (!string.IsNullOrEmpty(account.AccountCode) && char.IsDigit(account.AccountCode[0]))
        {
            categoryCode = account.AccountCode[0] - '0';
        }

        return new GlAccountDto
        {
            AccountCode = account.AccountCode,
            OldAccountCode = account.OldAccountCode,
            AccountNameAr = account.AccountNameAr,
            AccountNameEn = account.AccountNameEn,
            ParentAccountCode = account.ParentAccountCode,
            CategoryCode = categoryCode,
            AccountLevel = account.AccountLevel,
            AccountType = account.AccountType,
            NormalBalance = account.NormalBalance,
            IsContra = account.IsContra,
            IsControlAccount = account.IsControlAccount,
            ControlAccountType = account.ControlAccountType,
            IsBranchSpecific = account.IsBranchSpecific,
            IsClearing = account.IsClearing,
            IsActive = account.IsActive,
            IsPostable = account.IsPostable,
            Description = account.Description,
            Notes = account.Notes,
            BranchIds = account.BranchLinks
                .Where(link => link.IsActive)
                .Select(link => link.BranchId)
                .Distinct()
                .OrderBy(branchId => branchId)
                .ToList()
        };
    }

    public static GlAccountTreeDto ToTreeDto(GlAccount account)
    {
        var flat = ToDto(account);
        return new GlAccountTreeDto
        {
            AccountCode = flat.AccountCode,
            OldAccountCode = flat.OldAccountCode,
            AccountNameAr = flat.AccountNameAr,
            AccountNameEn = flat.AccountNameEn,
            ParentAccountCode = flat.ParentAccountCode,
            CategoryCode = flat.CategoryCode,
            AccountLevel = flat.AccountLevel,
            AccountType = flat.AccountType,
            NormalBalance = flat.NormalBalance,
            IsContra = flat.IsContra,
            IsControlAccount = flat.IsControlAccount,
            ControlAccountType = flat.ControlAccountType,
            IsBranchSpecific = flat.IsBranchSpecific,
            IsClearing = flat.IsClearing,
            IsActive = flat.IsActive,
            IsPostable = flat.IsPostable,
            Description = flat.Description,
            Notes = flat.Notes,
            BranchIds = flat.BranchIds
        };
    }
}
