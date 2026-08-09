using System.Globalization;
using ThinkOnErp.Application.DTOs.Accounting;
using ThinkOnErp.Application.Mappings.Accounting;
using ThinkOnErp.Domain.Entities.Accounting;
using ThinkOnErp.Domain.Exceptions;
using ThinkOnErp.Domain.Interfaces.Accounting;

namespace ThinkOnErp.Application.Services.Accounting;

public sealed class GlAccountService : IGlAccountService
{
    private static readonly HashSet<string> AccountTypes = new(StringComparer.Ordinal)
    {
        "HEADER",
        "DETAIL"
    };

    private static readonly HashSet<string> ControlTypes = new(StringComparer.Ordinal)
    {
        "AR",
        "AP",
        "INVENTORY"
    };

    private readonly IGlAccountRepository _repository;
    private readonly ICurrentTenantContext _tenantContext;

    public GlAccountService(
        IGlAccountRepository repository,
        ICurrentTenantContext tenantContext)
    {
        _repository = repository;
        _tenantContext = tenantContext;
    }

    public async Task<IReadOnlyList<GlAccountTreeDto>> GetTreeAsync(
        CancellationToken cancellationToken = default)
    {
        var companyId = _tenantContext.GetRequiredCompanyId();
        var accounts = await _repository.GetAllAsync(companyId, cancellationToken);
        var nodes = accounts.ToDictionary(account => account.Id, GlAccountMapper.ToTreeDto);
        var roots = new List<GlAccountTreeDto>();

        foreach (var account in accounts.OrderBy(account => account.AccountCode, StringComparer.Ordinal))
        {
            var node = nodes[account.Id];
            if (account.ParentAccountId.HasValue &&
                nodes.TryGetValue(account.ParentAccountId.Value, out var parent))
            {
                parent.Children.Add(node);
            }
            else
            {
                roots.Add(node);
            }
        }

        SortTree(roots);
        return roots;
    }

    public async Task<GlAccountDto> GetAccountAsync(
        long accountId,
        CancellationToken cancellationToken = default)
    {
        ValidateAccountId(accountId);

        var companyId = _tenantContext.GetRequiredCompanyId();
        var account = await _repository.GetByIdAsync(companyId, accountId, cancellationToken)
            ?? throw new AccountingNotFoundException(
                "The account does not exist.",
                "GL_ACCOUNT_NOT_FOUND");

        return GlAccountMapper.ToDto(account);
    }

    public async Task<IReadOnlyList<GlAccountDto>> GetPostableAccountsAsync(
        long branchId,
        CancellationToken cancellationToken = default)
    {
        if (branchId <= 0)
        {
            throw new AccountingException("Branch ID must be positive.", "GL_INVALID_BRANCH");
        }

        var companyId = _tenantContext.GetRequiredCompanyId();
        if (!await _repository.BranchBelongsToCompanyAsync(companyId, branchId, cancellationToken))
        {
            throw new AccountingException(
                "The selected branch does not belong to the current company or is inactive.",
                "GL_BRANCH_NOT_FOUND");
        }

        var accounts = await _repository.GetPostableAsync(companyId, branchId, cancellationToken);
        return accounts.Select(GlAccountMapper.ToDto).ToList();
    }

    public async Task<IReadOnlyList<AccountCategoryDto>> GetCategoriesAsync(
        CancellationToken cancellationToken = default)
    {
        _tenantContext.GetRequiredCompanyId();
        var categories = await _repository.GetCategoriesAsync(cancellationToken);

        return categories.Select(category => new AccountCategoryDto
        {
            Id = category.Id,
            CategoryCode = category.CategoryCode,
            NameAr = category.NameAr,
            NameEn = category.NameEn,
            NormalBalance = category.NormalBalance,
            FinancialStatement = category.FinancialStatement,
            DisplayOrder = category.DisplayOrder
        }).ToList();
    }

    public async Task<GlAccountDto> CreateAccountAsync(
        CreateGlAccountDto request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        var companyId = _tenantContext.GetRequiredCompanyId();
        var accountCode = RequiredText(request.AccountCode, nameof(request.AccountCode), 50);
        var accountNameAr = RequiredText(request.AccountNameAr, nameof(request.AccountNameAr), 200);
        var accountNameEn = RequiredText(request.AccountNameEn, nameof(request.AccountNameEn), 200);
        var accountType = RequiredText(request.AccountType, nameof(request.AccountType), 10).ToUpperInvariant();
        var normalBalance = RequiredText(request.NormalBalance, nameof(request.NormalBalance), 1).ToUpperInvariant();
        var controlType = NullIfWhiteSpace(request.ControlAccountType)?.ToUpperInvariant();

        if (!accountCode.All(char.IsAsciiDigit))
        {
            throw new AccountingException("Account code must contain digits only.", "GL_INVALID_CODE");
        }

        if (!AccountTypes.Contains(accountType))
        {
            throw new AccountingException("Account type must be HEADER or DETAIL.", "GL_INVALID_TYPE");
        }

        if (normalBalance is not ("D" or "C"))
        {
            throw new AccountingException("Normal balance must be D or C.", "GL_INVALID_BALANCE");
        }

        if (await _repository.AccountCodeExistsAsync(companyId, accountCode, cancellationToken))
        {
            throw new AccountingException(
                $"Account code '{accountCode}' already exists for the current company.",
                "GL_DUPLICATE_CODE");
        }

        var categories = await _repository.GetCategoriesAsync(cancellationToken);
        var category = categories.SingleOrDefault(item => item.Id == request.CategoryId)
            ?? throw new AccountingException("The selected account category does not exist.", "GL_CATEGORY_NOT_FOUND");

        GlAccount? parent = null;
        var accountLevel = 1;
        if (request.ParentAccountId.HasValue)
        {
            parent = await _repository.GetByIdAsync(
                companyId,
                request.ParentAccountId.Value,
                cancellationToken)
                ?? throw new AccountingException("The parent account does not exist.", "GL_PARENT_NOT_FOUND");

            if (!string.Equals(parent.AccountType, "HEADER", StringComparison.Ordinal))
            {
                throw new AccountingException("A DETAIL account cannot be used as a parent.", "GL_PARENT_NOT_HEADER");
            }

            accountLevel = parent.AccountLevel + 1;
            if (accountLevel > 5)
            {
                throw new AccountingException("The chart of accounts supports at most five levels.", "GL_LEVEL_LIMIT");
            }

            if (!accountCode.StartsWith(parent.AccountCode, StringComparison.Ordinal) ||
                accountCode.Length <= parent.AccountCode.Length)
            {
                throw new AccountingException(
                    "Account code must extend the parent account code.",
                    "GL_INVALID_PARENT_PREFIX");
            }

            if (parent.CategoryId != category.Id)
            {
                throw new AccountingException(
                    "The account category must match its parent category.",
                    "GL_PARENT_CATEGORY_MISMATCH");
            }
        }
        else if (!string.Equals(
                     accountCode,
                     category.CategoryCode.ToString(CultureInfo.InvariantCulture),
                     StringComparison.Ordinal))
        {
            throw new AccountingException(
                "A root account code must equal its category code.",
                "GL_INVALID_ROOT_CODE");
        }

        var expectedLength = accountLevel == 5 ? 6 : accountLevel;
        if (accountCode.Length != expectedLength)
        {
            throw new AccountingException(
                $"A level {accountLevel} account code must contain {expectedLength} digits.",
                "GL_INVALID_CODE_LENGTH");
        }

        if (accountLevel == 5 && accountType != "DETAIL")
        {
            throw new AccountingException("A level-5 account must be DETAIL.", "GL_LEVEL5_NOT_DETAIL");
        }

        if (!string.Equals(normalBalance, category.NormalBalance, StringComparison.Ordinal))
        {
            throw new AccountingException(
                "Normal balance must match the selected category; use IsContra for inverse presentation.",
                "GL_CATEGORY_BALANCE_MISMATCH");
        }

        ValidateControlAccount(request.IsControlAccount, controlType, accountType);
        ValidateDetailOnlyFlags(request.IsBranchSpecific, request.IsClearing, accountType);

        var branchIds = await ValidateBranchSelectionAsync(
            companyId,
            request.IsBranchSpecific,
            request.BranchIds,
            cancellationToken);

        var account = new GlAccount
        {
            CompanyId = companyId,
            AccountCode = accountCode,
            AccountNameAr = accountNameAr,
            AccountNameEn = accountNameEn,
            ParentAccountId = parent?.Id,
            ParentAccount = parent,
            CategoryId = category.Id,
            AccountLevel = accountLevel,
            AccountType = accountType,
            NormalBalance = normalBalance,
            IsContra = request.IsContra,
            IsControlAccount = request.IsControlAccount,
            ControlAccountType = controlType,
            IsBranchSpecific = request.IsBranchSpecific,
            IsClearing = request.IsClearing,
            IsActive = true,
            Description = OptionalText(request.Description, nameof(request.Description), 1000),
            Notes = OptionalText(request.Notes, nameof(request.Notes), 2000)
        };

        foreach (var branchId in branchIds)
        {
            account.BranchLinks.Add(new GlAccountBranch
            {
                BranchId = branchId,
                IsActive = true
            });
        }

        await _repository.AddAsync(account, cancellationToken);
        await _repository.SaveChangesAsync(cancellationToken);
        var result = GlAccountMapper.ToDto(account);
        result.CategoryCode = category.CategoryCode;
        result.CategoryNameAr = category.NameAr;
        result.CategoryNameEn = category.NameEn;
        return result;
    }

    public async Task<GlAccountDto> UpdateAccountAsync(
        long accountId,
        UpdateGlAccountDto request,
        CancellationToken cancellationToken = default)
    {
        ValidateAccountId(accountId);
        ArgumentNullException.ThrowIfNull(request);

        var companyId = _tenantContext.GetRequiredCompanyId();
        var account = await _repository.GetByIdAsync(companyId, accountId, cancellationToken)
            ?? throw new AccountingNotFoundException(
                "The account does not exist.",
                "GL_ACCOUNT_NOT_FOUND");

        var accountNameAr = RequiredText(
            request.AccountNameAr,
            nameof(request.AccountNameAr),
            200);
        var accountNameEn = RequiredText(
            request.AccountNameEn,
            nameof(request.AccountNameEn),
            200);
        var controlType = NullIfWhiteSpace(request.ControlAccountType)?.ToUpperInvariant();

        ValidateControlAccount(
            request.IsControlAccount,
            controlType,
            account.AccountType);
        ValidateDetailOnlyFlags(
            request.IsBranchSpecific,
            request.IsClearing,
            account.AccountType);

        var branchIds = await ValidateBranchSelectionAsync(
            companyId,
            request.IsBranchSpecific,
            request.BranchIds,
            cancellationToken);

        account.AccountNameAr = accountNameAr;
        account.AccountNameEn = accountNameEn;
        account.IsContra = request.IsContra;
        account.IsControlAccount = request.IsControlAccount;
        account.ControlAccountType = controlType;
        account.IsBranchSpecific = request.IsBranchSpecific;
        account.IsClearing = request.IsClearing;
        account.Description = OptionalText(
            request.Description,
            nameof(request.Description),
            1000);
        account.Notes = OptionalText(request.Notes, nameof(request.Notes), 2000);

        SynchronizeBranchLinks(account, branchIds);

        await _repository.SaveChangesAsync(cancellationToken);
        return GlAccountMapper.ToDto(account);
    }

    public async Task UpdateAccountStatusAsync(
        long accountId,
        bool isActive,
        CancellationToken cancellationToken = default)
    {
        ValidateAccountId(accountId);

        var companyId = _tenantContext.GetRequiredCompanyId();
        var account = await _repository.GetByIdAsync(companyId, accountId, cancellationToken)
            ?? throw new AccountingNotFoundException(
                "The account does not exist.",
                "GL_ACCOUNT_NOT_FOUND");

        if (account.IsActive == isActive)
        {
            return;
        }

        account.IsActive = isActive;
        await _repository.SaveChangesAsync(cancellationToken);
    }

    public async Task<GlAccountDto> DeleteAccountAsync(
        long accountId,
        CancellationToken cancellationToken = default)
    {
        ValidateAccountId(accountId);

        var companyId = _tenantContext.GetRequiredCompanyId();
        var account = await _repository.GetByIdAsync(companyId, accountId, cancellationToken)
            ?? throw new AccountingNotFoundException(
                "The account does not exist.",
                "GL_ACCOUNT_NOT_FOUND");

        if (!account.ParentAccountId.HasValue)
        {
            throw new AccountingConflictException(
                "Root category accounts cannot be deleted.",
                "GL_ROOT_ACCOUNT_DELETE_FORBIDDEN");
        }

        if (await _repository.HasChildrenAsync(companyId, accountId, cancellationToken))
        {
            throw new AccountingConflictException(
                "An account with child accounts cannot be deleted.",
                "GL_ACCOUNT_HAS_CHILDREN");
        }

        var deletedAccount = GlAccountMapper.ToDto(account);
        await _repository.DeleteAsync(account, cancellationToken);
        return deletedAccount;
    }

    private static void ValidateAccountId(long accountId)
    {
        if (accountId <= 0)
        {
            throw new AccountingException("Account ID must be positive.", "GL_INVALID_ID");
        }
    }

    private async Task<List<long>> ValidateBranchSelectionAsync(
        long companyId,
        bool isBranchSpecific,
        IReadOnlyCollection<long>? requestedBranchIds,
        CancellationToken cancellationToken)
    {
        var requested = requestedBranchIds ?? Array.Empty<long>();
        if (requested.Any(branchId => branchId <= 0))
        {
            throw new AccountingException(
                "Branch IDs must be positive.",
                "GL_INVALID_BRANCH");
        }

        var branchIds = requested
            .Distinct()
            .OrderBy(branchId => branchId)
            .ToList();

        if (isBranchSpecific && branchIds.Count == 0)
        {
            throw new AccountingException(
                "A branch-specific account requires at least one branch.",
                "GL_BRANCH_REQUIRED");
        }

        if (!isBranchSpecific && branchIds.Count > 0)
        {
            throw new AccountingException(
                "Branch links are only allowed for branch-specific accounts.",
                "GL_UNEXPECTED_BRANCH_LINK");
        }

        foreach (var branchId in branchIds)
        {
            if (!await _repository.BranchBelongsToCompanyAsync(
                    companyId,
                    branchId,
                    cancellationToken))
            {
                throw new AccountingException(
                    $"Branch '{branchId}' does not belong to the current company or is inactive.",
                    "GL_BRANCH_NOT_FOUND");
            }
        }

        return branchIds;
    }

    private static void SynchronizeBranchLinks(
        GlAccount account,
        IReadOnlyCollection<long> branchIds)
    {
        var missingBranchIds = branchIds.ToHashSet();

        foreach (var link in account.BranchLinks)
        {
            link.IsActive = missingBranchIds.Remove(link.BranchId);
        }

        foreach (var branchId in missingBranchIds.OrderBy(branchId => branchId))
        {
            account.BranchLinks.Add(new GlAccountBranch
            {
                GlAccountId = account.Id,
                BranchId = branchId,
                IsActive = true,
                GlAccount = account
            });
        }
    }

    private static string RequiredText(string? value, string fieldName, int maxLength)
    {
        var normalized = value?.Trim();
        if (string.IsNullOrEmpty(normalized))
        {
            throw new AccountingException($"{fieldName} is required.", "GL_REQUIRED_FIELD");
        }

        if (normalized.Length > maxLength)
        {
            throw new AccountingException(
                $"{fieldName} cannot exceed {maxLength} characters.",
                "GL_FIELD_TOO_LONG");
        }

        return normalized;
    }

    private static string? OptionalText(string? value, string fieldName, int maxLength)
    {
        var normalized = NullIfWhiteSpace(value);
        if (normalized?.Length > maxLength)
        {
            throw new AccountingException(
                $"{fieldName} cannot exceed {maxLength} characters.",
                "GL_FIELD_TOO_LONG");
        }

        return normalized;
    }

    private static string? NullIfWhiteSpace(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    private static void ValidateControlAccount(bool isControlAccount, string? controlType, string accountType)
    {
        if (!isControlAccount && controlType != null)
        {
            throw new AccountingException(
                "ControlAccountType must be empty when IsControlAccount is false.",
                "GL_UNEXPECTED_CONTROL_TYPE");
        }

        if (isControlAccount && (controlType == null || !ControlTypes.Contains(controlType)))
        {
            throw new AccountingException(
                "A control account requires AR, AP, or INVENTORY.",
                "GL_INVALID_CONTROL_TYPE");
        }

        if (isControlAccount && accountType != "DETAIL")
        {
            throw new AccountingException("A control account must be DETAIL.", "GL_CONTROL_NOT_DETAIL");
        }
    }

    private static void ValidateDetailOnlyFlags(bool isBranchSpecific, bool isClearing, string accountType)
    {
        if ((isBranchSpecific || isClearing) && accountType != "DETAIL")
        {
            throw new AccountingException(
                "Only DETAIL accounts can be branch-specific or clearing accounts.",
                "GL_DETAIL_FLAG_ON_HEADER");
        }
    }

    private static void SortTree(List<GlAccountTreeDto> nodes)
    {
        nodes.Sort((left, right) => string.CompareOrdinal(left.AccountCode, right.AccountCode));
        foreach (var node in nodes)
        {
            SortTree(node.Children);
        }
    }
}
