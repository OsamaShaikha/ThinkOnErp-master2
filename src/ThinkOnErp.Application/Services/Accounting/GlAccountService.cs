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
        var nodes = accounts.ToDictionary(account => account.AccountCode, GlAccountMapper.ToTreeDto, StringComparer.Ordinal);
        var roots = new List<GlAccountTreeDto>();

        foreach (var account in accounts.OrderBy(account => account.AccountCode, StringComparer.Ordinal))
        {
            var node = nodes[account.AccountCode];
            if (!string.IsNullOrEmpty(account.ParentAccountCode) &&
                nodes.TryGetValue(account.ParentAccountCode, out var parent))
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

    public async Task<IReadOnlyList<GlAccountCategoryDto>> GetCategoriesAsync(
        CancellationToken cancellationToken = default)
    {
        var companyId = _tenantContext.GetRequiredCompanyId();
        var accounts = await _repository.GetAllAsync(companyId, cancellationToken);

        var level1Accounts = accounts
            .Where(account => account.AccountLevel == 1)
            .OrderBy(account => account.AccountCode, StringComparer.Ordinal)
            .ToList();

        var level2Accounts = accounts
            .Where(account => account.AccountLevel == 2)
            .OrderBy(account => account.AccountCode, StringComparer.Ordinal)
            .ToList();

        var result = new List<GlAccountCategoryDto>();

        foreach (var l1 in level1Accounts)
        {
            var categoryDto = new GlAccountCategoryDto
            {
                AccountCode = l1.AccountCode,
                AccountNameLocal = l1.AccountNameLocal,
                AccountNameEn = l1.AccountNameEn,
                AccountLevel = l1.AccountLevel,
                AccountType = l1.AccountType,
                NormalBalance = l1.NormalBalance,
                ParentAccountCode = l1.ParentAccountCode,
                IsActive = l1.IsActive,
                SubCategories = level2Accounts
                    .Where(l2 => string.Equals(l2.ParentAccountCode, l1.AccountCode, StringComparison.Ordinal))
                    .Select(l2 => new GlAccountCategoryDto
                    {
                        AccountCode = l2.AccountCode,
                        AccountNameLocal = l2.AccountNameLocal,
                        AccountNameEn = l2.AccountNameEn,
                        AccountLevel = l2.AccountLevel,
                        AccountType = l2.AccountType,
                        NormalBalance = l2.NormalBalance,
                        ParentAccountCode = l2.ParentAccountCode,
                        IsActive = l2.IsActive
                    })
                    .ToList()
            };

            result.Add(categoryDto);
        }

        return result;
    }

    public async Task<GlAccountDto> GetAccountByCodeAsync(
        string accountCode,
        CancellationToken cancellationToken = default)
    {
        ValidateAccountCode(accountCode);

        var companyId = _tenantContext.GetRequiredCompanyId();
        var account = await _repository.GetByCodeAsync(companyId, accountCode, cancellationToken)
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

    public async Task<string> GetNextChildCodeAsync(
        string parentAccountCode,
        CancellationToken cancellationToken = default)
    {
        ValidateAccountCode(parentAccountCode);

        var companyId = _tenantContext.GetRequiredCompanyId();
        var parent = await _repository.GetByCodeAsync(companyId, parentAccountCode, cancellationToken)
            ?? throw new AccountingNotFoundException(
                "The parent account does not exist.",
                "GL_PARENT_NOT_FOUND");

        if (!string.Equals(parent.AccountType, "HEADER", StringComparison.Ordinal))
        {
            throw new AccountingException("A DETAIL account cannot have children.", "GL_PARENT_NOT_HEADER");
        }

        return await _repository.GetNextChildCodeAsync(companyId, parentAccountCode, cancellationToken);
    }

    public async Task<GlAccountDto> CreateAccountAsync(
        CreateGlAccountDto request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        var companyId = _tenantContext.GetRequiredCompanyId();
        var parentCode = NullIfWhiteSpace(request.ParentAccountCode);
        var accountNameLocal = RequiredText(request.AccountNameLocal, nameof(request.AccountNameLocal), 200);
        var accountNameEn = RequiredText(request.AccountNameEn, nameof(request.AccountNameEn), 200);
        var accountType = RequiredText(request.AccountType, nameof(request.AccountType), 10).ToUpperInvariant();
        var controlType = NullIfWhiteSpace(request.ControlAccountType)?.ToUpperInvariant();

        if (!AccountTypes.Contains(accountType))
        {
            throw new AccountingException("Account type must be HEADER or DETAIL.", "GL_INVALID_TYPE");
        }

        GlAccount? parent = null;
        var accountLevel = 1;
        string accountCode;

        if (!string.IsNullOrEmpty(parentCode))
        {
            parent = await _repository.GetByCodeAsync(companyId, parentCode, cancellationToken)
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

            if (string.IsNullOrWhiteSpace(request.AccountCode))
            {
                accountCode = await _repository.GetNextChildCodeAsync(companyId, parentCode, cancellationToken);
            }
            else
            {
                accountCode = request.AccountCode.Trim();
                if (!accountCode.StartsWith(parent.AccountCode, StringComparison.Ordinal) ||
                    accountCode.Length <= parent.AccountCode.Length)
                {
                    throw new AccountingException(
                        "Account code must extend the parent account code.",
                        "GL_INVALID_PARENT_PREFIX");
                }
            }
        }
        else
        {
            if (string.IsNullOrWhiteSpace(request.AccountCode))
            {
                throw new AccountingException("Root account code is required when parent is not specified.", "GL_REQUIRED_FIELD");
            }
            accountCode = request.AccountCode.Trim();
        }

        if (!accountCode.All(char.IsAsciiDigit))
        {
            throw new AccountingException("Account code must contain digits only.", "GL_INVALID_CODE");
        }

        if (await _repository.AccountCodeExistsAsync(companyId, accountCode, cancellationToken))
        {
            throw new AccountingException(
                $"Account code '{accountCode}' already exists for the current company.",
                "GL_DUPLICATE_CODE");
        }

        var normalBalance = request.NormalBalance?.Trim().ToUpperInvariant() ?? parent?.NormalBalance ?? "D";
        if (normalBalance is not ("D" or "C"))
        {
            throw new AccountingException("Normal balance must be D or C.", "GL_INVALID_BALANCE");
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

        ValidateControlAccount(request.IsControlAccount, controlType, accountType);
        ValidateDetailOnlyFlags(request.IsBranchSpecific, request.IsClearing, accountType);

        var branchIds = await ValidateBranchSelectionAsync(
            companyId,
            request.IsBranchSpecific,
            request.BranchIds,
            cancellationToken);

        var account = new GlAccount
        {
            AccountCode = accountCode,
            OldAccountCode = OptionalText(request.OldAccountCode, nameof(request.OldAccountCode), 50),
            AccountNameLocal = accountNameLocal,
            AccountNameEn = accountNameEn,
            ParentAccountCode = parent?.AccountCode,
            ParentAccount = parent,
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
                AccountCode = accountCode,
                BranchId = branchId,
                IsActive = true
            });
        }

        await _repository.AddAsync(account, cancellationToken);
        await _repository.SaveChangesAsync(cancellationToken);
        return GlAccountMapper.ToDto(account);
    }

    public async Task<GlAccountDto> UpdateAccountAsync(
        string accountCode,
        UpdateGlAccountDto request,
        CancellationToken cancellationToken = default)
    {
        ValidateAccountCode(accountCode);
        ArgumentNullException.ThrowIfNull(request);

        var companyId = _tenantContext.GetRequiredCompanyId();
        var account = await _repository.GetByCodeAsync(companyId, accountCode, cancellationToken)
            ?? throw new AccountingNotFoundException(
                "The account does not exist.",
                "GL_ACCOUNT_NOT_FOUND");

        var accountNameLocal = RequiredText(request.AccountNameLocal, nameof(request.AccountNameLocal), 200);
        var accountNameEn = RequiredText(request.AccountNameEn, nameof(request.AccountNameEn), 200);
        var controlType = NullIfWhiteSpace(request.ControlAccountType)?.ToUpperInvariant();

        ValidateControlAccount(request.IsControlAccount, controlType, account.AccountType);
        ValidateDetailOnlyFlags(request.IsBranchSpecific, request.IsClearing, account.AccountType);

        var branchIds = await ValidateBranchSelectionAsync(
            companyId,
            request.IsBranchSpecific,
            request.BranchIds,
            cancellationToken);

        account.OldAccountCode = OptionalText(request.OldAccountCode, nameof(request.OldAccountCode), 50);
        account.AccountNameLocal = accountNameLocal;
        account.AccountNameEn = accountNameEn;
        account.IsContra = request.IsContra;
        account.IsControlAccount = request.IsControlAccount;
        account.ControlAccountType = controlType;
        account.IsBranchSpecific = request.IsBranchSpecific;
        account.IsClearing = request.IsClearing;
        account.Description = OptionalText(request.Description, nameof(request.Description), 1000);
        account.Notes = OptionalText(request.Notes, nameof(request.Notes), 2000);

        SynchronizeBranchLinks(account, companyId, branchIds);

        await _repository.SaveChangesAsync(cancellationToken);
        return GlAccountMapper.ToDto(account);
    }

    public async Task UpdateAccountStatusAsync(
        string accountCode,
        bool isActive,
        CancellationToken cancellationToken = default)
    {
        ValidateAccountCode(accountCode);

        var companyId = _tenantContext.GetRequiredCompanyId();
        var account = await _repository.GetByCodeAsync(companyId, accountCode, cancellationToken)
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
        string accountCode,
        CancellationToken cancellationToken = default)
    {
        ValidateAccountCode(accountCode);

        var companyId = _tenantContext.GetRequiredCompanyId();
        var account = await _repository.GetByCodeAsync(companyId, accountCode, cancellationToken)
            ?? throw new AccountingNotFoundException(
                "The account does not exist.",
                "GL_ACCOUNT_NOT_FOUND");

        if (string.IsNullOrEmpty(account.ParentAccountCode))
        {
            throw new AccountingConflictException(
                "Root category accounts cannot be deleted.",
                "GL_ROOT_ACCOUNT_DELETE_FORBIDDEN");
        }

        if (await _repository.HasChildrenAsync(companyId, accountCode, cancellationToken))
        {
            throw new AccountingConflictException(
                "An account with child accounts cannot be deleted.",
                "GL_ACCOUNT_HAS_CHILDREN");
        }

        var deletedAccount = GlAccountMapper.ToDto(account);
        await _repository.DeleteAsync(account, cancellationToken);
        return deletedAccount;
    }

    private static void ValidateAccountCode(string accountCode)
    {
        if (string.IsNullOrWhiteSpace(accountCode))
        {
            throw new AccountingException("Account code is required.", "GL_INVALID_CODE");
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
        long companyId,
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
                AccountCode = account.AccountCode,
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

    public async Task<IReadOnlyList<GlAccountStructureConfigDto>> GetStructureConfigsAsync(CancellationToken cancellationToken = default)
    {
        var entities = await _repository.GetStructureConfigsAsync(cancellationToken);
        int cumulative = 0;

        return entities.Select(e =>
        {
            cumulative += e.DigitLength;
            return new GlAccountStructureConfigDto
            {
                LevelNumber = e.LevelNumber,
                DigitLength = e.DigitLength,
                TotalCumulativeLength = cumulative,
                LevelNameLocal = e.LevelNameLocal,
                LevelNameEn = e.LevelNameEn,
                Description = e.Description,
                IsActive = e.IsActive
            };
        }).ToList();
    }

    public async Task<IReadOnlyList<GlAccountStructureConfigDto>> UpdateStructureConfigsAsync(
        IEnumerable<UpdateGlAccountStructureConfigDto> request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        var entities = request.Select(r => new ThinkOnErp.Domain.Entities.Accounting.GlAccountStructureConfig
        {
            LevelNumber = r.LevelNumber,
            DigitLength = r.DigitLength > 0 ? r.DigitLength : 1,
            LevelNameLocal = r.LevelNameLocal ?? $"المستوى {r.LevelNumber}",
            LevelNameEn = r.LevelNameEn ?? $"Level {r.LevelNumber}",
            Description = r.Description,
            IsActive = r.IsActive
        }).ToList();

        await _repository.SaveStructureConfigsAsync(entities, cancellationToken);
        return await GetStructureConfigsAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<GlAccountTreeDto>> GenerateDefaultTreePreviewAsync(
        CancellationToken cancellationToken = default)
    {
        var companyId = _tenantContext.GetRequiredCompanyId();
        var accounts = await BuildGlAccountsFromTemplateAsync(companyId, cancellationToken);

        var dtos = accounts.Select(GlAccountMapper.ToTreeDto).ToList();
        var dtoDict = dtos.ToDictionary(d => d.AccountCode, StringComparer.Ordinal);
        var roots = new List<GlAccountTreeDto>();

        foreach (var dto in dtos)
        {
            if (string.IsNullOrEmpty(dto.ParentAccountCode))
            {
                roots.Add(dto);
            }
            else if (dtoDict.TryGetValue(dto.ParentAccountCode, out var parent))
            {
                parent.Children.Add(dto);
            }
        }

        SortTree(roots);
        return roots;
    }

    public async Task<IReadOnlyList<GlAccountDto>> SeedDefaultTreeAsync(
        long defaultBranchId,
        CancellationToken cancellationToken = default)
    {
        var companyId = _tenantContext.GetRequiredCompanyId();
        var accounts = await BuildGlAccountsFromTemplateAsync(companyId, cancellationToken);

        if (defaultBranchId > 0)
        {
            foreach (var account in accounts)
            {
                if (account.AccountType == "DETAIL")
                {
                    account.BranchLinks.Add(new GlAccountBranch
                    {
                        AccountCode = account.AccountCode,
                        BranchId = defaultBranchId,
                        IsActive = true
                    });
                }
            }
        }

        await _repository.ImportAsync(accounts, cancellationToken);
        return accounts.Select(GlAccountMapper.ToDto).ToList();
    }

    private async Task<List<GlAccount>> BuildGlAccountsFromTemplateAsync(
        long companyId,
        CancellationToken cancellationToken)
    {
        var configs = await _repository.GetStructureConfigsAsync(cancellationToken);
        var digitLengths = new Dictionary<int, int>();
        foreach (var cfg in configs)
        {
            digitLengths[cfg.LevelNumber] = cfg.DigitLength;
        }

        int GetDigitLength(int level) =>
            digitLengths.TryGetValue(level, out var len) ? len : (level == 5 ? 2 : 1);

        var templateRoots = GetStandardCoaTemplate();
        var accounts = new List<GlAccount>();

        void ProcessNode(DefaultTemplateAccount node, string parentCode)
        {
            int len = GetDigitLength(node.Level);
            string codeSuffix = node.SeqIndex.ToString().PadLeft(len, '0');
            string currentCode = string.IsNullOrEmpty(parentCode) ? codeSuffix : (parentCode + codeSuffix);

            var account = new GlAccount
            {
                AccountCode = currentCode,
                AccountNameLocal = node.NameLocal,
                AccountNameEn = node.NameEn,
                ParentAccountCode = string.IsNullOrEmpty(parentCode) ? null : parentCode,
                AccountLevel = node.Level,
                AccountType = node.AccountType,
                NormalBalance = node.NormalBalance,
                IsContra = node.IsContra,
                IsControlAccount = node.IsControlAccount,
                ControlAccountType = node.ControlAccountType,
                IsBranchSpecific = false,
                IsClearing = false,
                IsActive = true,
                Description = $"حساب افتراضي مولد للمستوى {node.Level}"
            };

            accounts.Add(account);

            if (node.Children != null)
            {
                foreach (var child in node.Children)
                {
                    ProcessNode(child, currentCode);
                }
            }
        }

        foreach (var root in templateRoots)
        {
            ProcessNode(root, string.Empty);
        }

        return accounts;
    }

    private sealed record DefaultTemplateAccount(
        int Level,
        int SeqIndex,
        string NameLocal,
        string NameEn,
        string AccountType,
        string NormalBalance,
        bool IsContra = false,
        bool IsControlAccount = false,
        string? ControlAccountType = null,
        List<DefaultTemplateAccount>? Children = null
    );

    private static List<DefaultTemplateAccount> GetStandardCoaTemplate() => new()
    {
        // 1. Assets
        new(1, 1, "الأصول", "Assets", "HEADER", "D", Children: new()
        {
            new(2, 1, "الأصول المتداولة", "Current Assets", "HEADER", "D", Children: new()
            {
                new(3, 1, "النقدية وما في حكمها", "Cash & Cash Equivalents", "HEADER", "D", Children: new()
                {
                    new(4, 1, "البنوك والمصارف", "Banks & Financial Institutions", "HEADER", "D", Children: new()
                    {
                        new(5, 1, "بنك الاتحاد - الحساب الجاري الرئيسي", "Union Bank - Main Account", "DETAIL", "D"),
                        new(5, 2, "الصندوق الرئيسي - الخزينة", "Main Treasury Petty Cash", "DETAIL", "D")
                    }),
                    new(4, 2, "ذمم العملاء والمدينون", "Accounts Receivable", "HEADER", "D", Children: new()
                    {
                        new(5, 1, "حساب العملاء التجاريون المحليون", "Trade Customers Control Account", "DETAIL", "D", IsControlAccount: true, ControlAccountType: "AR")
                    })
                }),
                new(3, 2, "المخزون والبضاعة", "Inventories", "HEADER", "D", Children: new()
                {
                    new(4, 1, "مخزون البضائع المشتراة", "Goods Purchased for Resale", "HEADER", "D", Children: new()
                    {
                        new(5, 1, "مخزون المستودع الرئيسي", "Main Warehouse Inventory", "DETAIL", "D", IsControlAccount: true, ControlAccountType: "INVENTORY")
                    })
                })
            }),
            new(2, 2, "الأصول غير المتداولة", "Non-Current Assets", "HEADER", "D", Children: new()
            {
                new(3, 1, "الأصول الثابتة والممتلكات", "Property, Plant & Equipment", "HEADER", "D", Children: new()
                {
                    new(4, 1, "المباني والمعدات والسيارات", "Buildings & Vehicles", "HEADER", "D", Children: new()
                    {
                        new(5, 1, "آلات ومعدات وسيارة النقل", "Machinery, Furniture & Vehicles", "DETAIL", "D")
                    })
                })
            })
        }),

        // 2. Liabilities
        new(1, 2, "الخصوم والالتزامات", "Liabilities", "HEADER", "C", Children: new()
        {
            new(2, 1, "الالتزامات المتداولة", "Current Liabilities", "HEADER", "C", Children: new()
            {
                new(3, 1, "ذمم الموردين والدائنون", "Accounts Payable", "HEADER", "C", Children: new()
                {
                    new(4, 1, "ذمم الموردين التجاريين", "Trade Payables", "HEADER", "C", Children: new()
                    {
                        new(5, 1, "حساب الموردون التجاريون المحليون", "Trade Suppliers Control Account", "DETAIL", "C", IsControlAccount: true, ControlAccountType: "AP")
                    })
                }),
                new(3, 2, "المصاريف المستحقة والضرائب", "Accrued Expenses & Taxes", "HEADER", "C", Children: new()
                {
                    new(4, 1, "الضرائب المستحقة والواجبة الدفع", "Taxes Payable", "HEADER", "C", Children: new()
                    {
                        new(5, 1, "حساب ضريبة القيمة المضافة / المبيعات", "Value Added Tax (VAT) Payable", "DETAIL", "C")
                    })
                })
            })
        }),

        // 3. Equity
        new(1, 3, "حقوق الملكية", "Equity", "HEADER", "C", Children: new()
        {
            new(2, 1, "رأس المال والاحتياطيات", "Capital & Reserves", "HEADER", "C", Children: new()
            {
                new(3, 1, "رأس المال المدفوع", "Paid-in Capital", "HEADER", "C", Children: new()
                {
                    new(4, 1, "رأس مال الشركة", "Share Capital", "HEADER", "C", Children: new()
                    {
                        new(5, 1, "حساب رأس المال المكتتب به", "Owners Capital Account", "DETAIL", "C")
                    })
                }),
                new(3, 2, "الأرباح المدورة", "Retained Earnings", "HEADER", "C", Children: new()
                {
                    new(4, 1, "الأرباح والخسائر التراكمية", "Accumulated Profits", "HEADER", "C", Children: new()
                    {
                        new(5, 1, "حساب الأرباح (الخسائر) المدورة", "Retained Earnings Account", "DETAIL", "C")
                    })
                })
            })
        }),

        // 4. Revenue
        new(1, 4, "الإيرادات والمبيعات", "Revenues", "HEADER", "C", Children: new()
        {
            new(2, 1, "الإيرادات التشغيلية", "Operating Revenues", "HEADER", "C", Children: new()
            {
                new(3, 1, "مبيعات البضائع والخدمات", "Sales Revenue", "HEADER", "C", Children: new()
                {
                    new(4, 1, "إيراد المبيعات التجارية", "Commercial Sales", "HEADER", "C", Children: new()
                    {
                        new(5, 1, "إيراد مبيعات المنتجات والخدمات الرئيسية", "Main Product & Service Sales", "DETAIL", "C")
                    })
                })
            })
        }),

        // 5. Expenses
        new(1, 5, "المصاريف والنفقات", "Expenses", "HEADER", "D", Children: new()
        {
            new(2, 1, "المصاريف التشغيلية والإدارية", "Operating & Administrative Expenses", "HEADER", "D", Children: new()
            {
                new(3, 1, "المصاريف العمومية والإدارية", "General & Administrative Expenses", "HEADER", "D", Children: new()
                {
                    new(4, 1, "الأجور والرواتب", "Salaries & Wages", "HEADER", "D", Children: new()
                    {
                        new(5, 1, "رواتب وأجور الموظفين الأساسية", "Basic Salaries & Allowances", "DETAIL", "D")
                    }),
                    new(4, 2, "الإيجارات والمنافع العامة", "Rent & Utilities", "HEADER", "D", Children: new()
                    {
                        new(5, 1, "مصاريف إيجار المكاتب والفروع", "Office Rent Expense", "DETAIL", "D")
                    })
                })
            })
        })
    };

    private static void SortTree(List<GlAccountTreeDto> nodes)
    {
        nodes.Sort((left, right) => string.CompareOrdinal(left.AccountCode, right.AccountCode));
        foreach (var node in nodes)
        {
            SortTree(node.Children);
        }
    }
}

