using ThinkOnErp.Application.DTOs.Accounting;
using ThinkOnErp.Domain.Entities.Accounting;

namespace ThinkOnErp.Application.Mappings.Accounting;

public static class GlAccountMapper
{
    public static GlAccountDto ToDto(GlAccount account) => new()
    {
        Id = account.Id,
        AccountCode = account.AccountCode,
        AccountNameAr = account.AccountNameAr,
        AccountNameEn = account.AccountNameEn,
        ParentAccountId = account.ParentAccountId,
        CategoryId = account.CategoryId,
        CategoryCode = account.Category?.CategoryCode ?? 0,
        CategoryNameAr = account.Category?.NameAr ?? string.Empty,
        CategoryNameEn = account.Category?.NameEn ?? string.Empty,
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

    public static GlAccountTreeDto ToTreeDto(GlAccount account)
    {
        var flat = ToDto(account);
        return new GlAccountTreeDto
        {
            Id = flat.Id,
            AccountCode = flat.AccountCode,
            AccountNameAr = flat.AccountNameAr,
            AccountNameEn = flat.AccountNameEn,
            ParentAccountId = flat.ParentAccountId,
            CategoryId = flat.CategoryId,
            CategoryCode = flat.CategoryCode,
            CategoryNameAr = flat.CategoryNameAr,
            CategoryNameEn = flat.CategoryNameEn,
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
